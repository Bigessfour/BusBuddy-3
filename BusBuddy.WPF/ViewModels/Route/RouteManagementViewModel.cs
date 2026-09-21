using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Models;
using Serilog;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Threading;
using System.IO;
using Serilog.Context;
using BusBuddy.WPF;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.ViewModels.Map;

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Phase 2 Route Management ViewModel
    /// Enhanced route planning and management functionality
    /// </summary>
    public partial class RouteManagementViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<RouteManagementViewModel>();
        /// <summary>
        /// Backing collection of routes displayed in the grid. Bound to <see cref="RoutesView"/> for filtering.
        /// </summary>
        public ObservableCollection<BusBuddy.Core.Models.Route> Routes { get; set; } = new();

        /// <summary>
        /// CollectionView wrapper that provides filtering and view operations for <see cref="Routes"/>.
        /// </summary>
        public ICollectionView RoutesView { get; private set; } = null!;

        // Entity Framework context for data access
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IRouteService _routeService;
        private readonly IRouteOptimizationService? _routeOptimization;
        private readonly IRouteDeterminationService? _routeDetermination;
        private readonly IDestinationService? _destinations;
        private readonly MapViewModel? _map;
        private IScheduleService? _scheduleService;
        private RouteExportService? _exportService;
        private IOperationalReportService? _reportService;

        private IAsyncRelayCommand _openAssignmentRelay = null!;
        private IAsyncRelayCommand _addRouteRelay = null!;
        private IAsyncRelayCommand _editRouteRelay = null!;
        private IAsyncRelayCommand _deleteRouteRelay = null!;
        private IAsyncRelayCommand _generateScheduleRelay = null!;
        private IAsyncRelayCommand _generateRoutesRelay = null!;
        private IAsyncRelayCommand _generateTransferRoutesRelay = null!;
        private IAsyncRelayCommand _assignVehicleRelay = null!;
        private IAsyncRelayCommand _assignDriverRelay = null!;
        private IAsyncRelayCommand _exportCsvRelay = null!;
        private IAsyncRelayCommand _exportReportRelay = null!;
        private IAsyncRelayCommand _printScheduleRelay = null!;
        private IAsyncRelayCommand _refreshRelay = null!;
        private IAsyncRelayCommand _refreshDrivePathRelay = null!;
        private IAsyncRelayCommand _optimizeStopOrderRelay = null!;
        private IAsyncRelayCommand _copyRouteRelay = null!;

        private readonly SemaphoreSlim _loadGate = new(1, 1);

        private bool _isRefreshing;
        private bool _isBusy;

        /// <summary>True while routes are being loaded from the service.</summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (_isRefreshing == value) return;
                _isRefreshing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLoading));
                RefreshSelectionDependentCommands();
            }
        }

        /// <summary>True while a route mutation (save, delete, assign, generate) is in progress.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLoading));
                RefreshSelectionDependentCommands();
            }
        }

        /// <summary>Combined busy state for status UI bindings.</summary>
        public bool IsLoading => IsBusy || IsRefreshing;

        /// <summary>
        /// School destinations for the School combo column (Destination.Name stored on Route.School).
        /// </summary>
        public ObservableCollection<Destination> AvailableSchools { get; } = new();

        /// <summary>
        /// Buses available for assignment (Active / In Service) — loaded lazily when first needed.
        /// </summary>
        public ObservableCollection<BusBuddy.Core.Models.Bus> AvailableBuses { get; } = new();

        /// <summary>Drivers available for hop-4 assignment.</summary>
        public ObservableCollection<BusBuddy.Core.Models.Driver> AvailableDrivers { get; } = new();

        private int? _selectedBusId;
        /// <summary>BusId selected in the assignment combo (SelectedValuePath binding).</summary>
        public int? SelectedBusId
        {
            get => _selectedBusId;
            set
            {
                if (_selectedBusId == value)
                {
                    return;
                }

                _selectedBusId = value;
                _selectedBus = value is int id
                    ? AvailableBuses.FirstOrDefault(b => b.BusId == id)
                    : null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBus));
                RefreshSelectionDependentCommands();
            }
        }

        private BusBuddy.Core.Models.Bus? _selectedBus;
        /// <summary>
        /// Currently selected bus to assign to the selected route.
        /// </summary>
        public BusBuddy.Core.Models.Bus? SelectedBus
        {
            get => _selectedBus;
            set
            {
                if (ReferenceEquals(_selectedBus, value))
                {
                    return;
                }

                _selectedBus = value;
                var newId = value?.BusId;
                if (_selectedBusId != newId)
                {
                    _selectedBusId = newId;
                    OnPropertyChanged(nameof(SelectedBusId));
                }

                OnPropertyChanged();
                RefreshSelectionDependentCommands();
            }
        }

        private RouteTimeSlot _selectedTimeSlot = RouteTimeSlot.AM;
        /// <summary>
        /// Selected time slot (AM/PM/Both) for vehicle / driver assignment.
        /// </summary>
        public RouteTimeSlot SelectedTimeSlot
        {
            get => _selectedTimeSlot;
            set
            {
                if (_selectedTimeSlot == value)
                {
                    return;
                }

                _selectedTimeSlot = value;
                OnPropertyChanged();
                SyncAssignmentFromSelectedRoute();
                RefreshSelectionDependentCommands();
            }
        }

        private int? _selectedDriverId;
        /// <summary>DriverId selected in the assignment combo.</summary>
        public int? SelectedDriverId
        {
            get => _selectedDriverId;
            set
            {
                if (_selectedDriverId == value)
                {
                    return;
                }

                _selectedDriverId = value;
                _selectedDriver = value is int id
                    ? AvailableDrivers.FirstOrDefault(d => d.DriverId == id)
                    : null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDriver));
                RefreshSelectionDependentCommands();
            }
        }

        private BusBuddy.Core.Models.Driver? _selectedDriver;
        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (ReferenceEquals(_selectedDriver, value))
                {
                    return;
                }

                _selectedDriver = value;
                var newId = value?.DriverId;
                if (_selectedDriverId != newId)
                {
                    _selectedDriverId = newId;
                    OnPropertyChanged(nameof(SelectedDriverId));
                }

                OnPropertyChanged();
                RefreshSelectionDependentCommands();
            }
        }

        private BusBuddy.Core.Models.Route? _selectedRoute;
        /// <summary>
        /// Currently selected route in the grid.
        /// </summary>
        public BusBuddy.Core.Models.Route? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                _selectedRoute = value;
                if (value != null)
                {
                    var slot = RouteSession.ToAssignmentSlot(value);
                    if (_selectedTimeSlot != slot)
                    {
                        _selectedTimeSlot = slot;
                        OnPropertyChanged(nameof(SelectedTimeSlot));
                    }
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRouteSelected));
                SyncAssignmentFromSelectedRoute();
                RefreshSelectionDependentCommands();
            }
        }

        /// <summary>
        /// Indicates whether a route is currently selected in the grid.
        /// </summary>
        public bool IsRouteSelected => SelectedRoute is not null;

        private string _quickSearchText = string.Empty;
        /// <summary>
        /// Text used to filter the routes list (case-insensitive contains on name, description, and school).
        /// </summary>
        public string QuickSearchText
        {
            get => _quickSearchText;
            set
            {
                if (_quickSearchText != value)
                {
                    _quickSearchText = value;
                    OnPropertyChanged();
                    RoutesView.Refresh();
                    OnPropertyChanged(nameof(VisibleRouteCount));
                }
            }
        }

        private bool _showRetiredRoutes;

        /// <summary>When false (default), retired routes are hidden from the grid after Delete.</summary>
        public bool ShowRetiredRoutes
        {
            get => _showRetiredRoutes;
            set
            {
                if (_showRetiredRoutes == value)
                {
                    return;
                }

                _showRetiredRoutes = value;
                OnPropertyChanged();
                RoutesView.Refresh();
                OnPropertyChanged(nameof(VisibleRouteCount));
            }
        }

        private string _statusMessage = "Ready";
        /// <summary>
        /// Simple status text surfaced to the UI (e.g., load results or error messages).
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Total number of routes in the current <see cref="Routes"/> collection.
        /// </summary>
        public int TotalRoutes => Routes.Count;

        /// <summary>Routes shown in the grid given <see cref="ShowRetiredRoutes"/> and search filter.</summary>
        public int VisibleRouteCount
        {
            get
            {
                var count = 0;
                foreach (var item in RoutesView)
                {
                    if (item is BusBuddy.Core.Models.Route)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Number of active routes.
        /// </summary>
        public int ActiveRoutes => Routes.Count(r => r.IsActive);

        /// <summary>
        /// Grid rows may not have <see cref="BusBuddy.Core.Models.Route.StopCount"/> loaded; default to 2 so Drive Path stays enabled until a count proves otherwise.
        /// </summary>
        internal static bool CanRefreshDrivePathFor(BusBuddy.Core.Models.Route? route) =>
            route is not null && route.StopCount.GetValueOrDefault(2) >= 2;
        /// <summary>
        /// Aggregate count of assigned students across all routes (null-safe).
        /// </summary>
        public int TotalAssignedStudents => Routes.Sum(r => r.StudentCount ?? 0);

        // Commands used by RouteManagementView toolbar
        public ICommand AddRouteCommand { get; private set; } = null!;
        public ICommand EditRouteCommand { get; private set; } = null!;
        public ICommand DeleteRouteCommand { get; private set; } = null!;
        public ICommand GenerateScheduleCommand { get; private set; } = null!;
        public ICommand GenerateRoutesCommand { get; private set; } = null!;
        public ICommand GenerateTransferRoutesCommand { get; private set; } = null!;
        public ICommand OpenRouteAssignmentCommand { get; private set; } = null!;
        public ICommand AssignVehicleCommand { get; private set; } = null!;
        public ICommand AssignDriverCommand { get; private set; } = null!;
        public ICommand ExportCsvCommand { get; private set; } = null!;
        public ICommand ExportReportCommand { get; private set; } = null!;
        public ICommand PrintScheduleCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand RefreshDrivePathCommand { get; private set; } = null!;
        public ICommand OptimizeStopOrderCommand { get; private set; } = null!;
        public ICommand CopyRouteCommand { get; private set; } = null!;

        public RouteManagementViewModel(
            IBusBuddyDbContextFactory contextFactory,
            IRouteService routeService,
            IRouteDeterminationService? routeDetermination,
            IDestinationService? destinations = null,
            IRouteOptimizationService? routeOptimization = null,
            MapViewModel? map = null,
            IScheduleService? scheduleService = null,
            RouteExportService? exportService = null,
            IOperationalReportService? reportService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _routeOptimization = routeOptimization;
            _routeDetermination = routeDetermination;
            _destinations = destinations;
            _map = map;
            _scheduleService = scheduleService;
            _exportService = exportService;
            _reportService = reportService;
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            RoutesView = CollectionViewSource.GetDefaultView(Routes);
            RoutesView.Filter = FilterRoutes;

            _openAssignmentRelay = new AsyncRelayCommand(OpenRouteAssignmentAsync, () => IsRouteSelected && !IsBusy);
            OpenRouteAssignmentCommand = _openAssignmentRelay;

            _addRouteRelay = new AsyncRelayCommand(AddRouteAsync, () => !IsBusy);
            AddRouteCommand = _addRouteRelay;
            _editRouteRelay = new AsyncRelayCommand(EditSelectedRouteAsync, () => IsRouteSelected && !IsBusy);
            EditRouteCommand = _editRouteRelay;
            _deleteRouteRelay = new AsyncRelayCommand(DeleteSelectedRouteAsync, () => IsRouteSelected && !IsBusy);
            DeleteRouteCommand = _deleteRouteRelay;
            _generateScheduleRelay = new AsyncRelayCommand(GenerateScheduleAsync, () => IsRouteSelected && !IsBusy);
            GenerateScheduleCommand = _generateScheduleRelay;
            _generateRoutesRelay = new AsyncRelayCommand(GenerateRoutesAsync, () => !IsBusy && !IsRefreshing);
            GenerateRoutesCommand = _generateRoutesRelay;
            _generateTransferRoutesRelay = new AsyncRelayCommand(GenerateTransferRoutesAsync, () => !IsBusy && !IsRefreshing);
            GenerateTransferRoutesCommand = _generateTransferRoutesRelay;
            _assignVehicleRelay = new AsyncRelayCommand(
                AssignVehicleAsync,
                () => IsRouteSelected && SelectedBusId.HasValue && !IsBusy);
            AssignVehicleCommand = _assignVehicleRelay;
            _assignDriverRelay = new AsyncRelayCommand(
                AssignDriverAsync,
                () => IsRouteSelected && SelectedDriverId.HasValue && !IsBusy);
            AssignDriverCommand = _assignDriverRelay;
            _exportCsvRelay = new AsyncRelayCommand(ExportCsvAsync, () => !IsBusy);
            ExportCsvCommand = _exportCsvRelay;
            _exportReportRelay = new AsyncRelayCommand(ExportReportAsync, () => !IsBusy);
            ExportReportCommand = _exportReportRelay;
            _printScheduleRelay = new AsyncRelayCommand(PrintScheduleAsync, () => IsRouteSelected && !IsBusy);
            PrintScheduleCommand = _printScheduleRelay;
            _refreshRelay = new AsyncRelayCommand(() => LoadRoutesAsync(), () => !IsRefreshing);
            RefreshCommand = _refreshRelay;
            _refreshDrivePathRelay = new AsyncRelayCommand(
                RefreshDrivePathAsync,
                () => IsRouteSelected && !IsBusy && CanRefreshDrivePathFor(SelectedRoute));
            RefreshDrivePathCommand = _refreshDrivePathRelay;
            _optimizeStopOrderRelay = new AsyncRelayCommand(OptimizeStopOrderAsync, () => IsRouteSelected && !IsBusy);
            OptimizeStopOrderCommand = _optimizeStopOrderRelay;
            _copyRouteRelay = new AsyncRelayCommand(CopyRouteAsync, () => IsRouteSelected && !IsBusy);
            CopyRouteCommand = _copyRouteRelay;

            RefreshSelectionDependentCommands();
        }

        /// <summary>Loads routes and assignment buses — call once from view <c>Loaded</c>.</summary>
        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                    EnsureBusesLoadedAsync(),
                    EnsureDriversLoadedAsync(),
                    LoadSchoolsAsync(),
                    LoadRoutesAsync())
                .ConfigureAwait(true);
        }

        private async Task LoadSchoolsAsync()
        {
            if (_destinations is null)
            {
                return;
            }

            try
            {
                var schools = await _destinations.GetActiveSchoolsAsync().ConfigureAwait(true);
                AvailableSchools.Clear();
                foreach (var school in schools)
                {
                    AvailableSchools.Add(school);
                }

                Logger.Debug("Loaded {Count} schools for route grid combo", AvailableSchools.Count);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed loading school destinations for route grid");
            }
        }

        private void SyncAssignmentFromSelectedRoute()
        {
            if (SelectedRoute is null)
            {
                SelectedBusId = null;
                SelectedDriverId = null;
                return;
            }

            // Both displays AM defaults (same as RouteAssignmentViewModel.NormalizeTimeSlot).
            var usePm = SelectedTimeSlot == RouteTimeSlot.PM;
            var vehicleId = usePm ? SelectedRoute.PMVehicleId : SelectedRoute.AMVehicleId;
            var driverId = usePm ? SelectedRoute.PMDriverId : SelectedRoute.AMDriverId;

            var match = vehicleId is int vid
                ? AvailableBuses.FirstOrDefault(b => b.BusId == vid)
                : null;
            if (match is null && !usePm && !string.IsNullOrWhiteSpace(SelectedRoute.BusNumber))
            {
                match = AvailableBuses.FirstOrDefault(b =>
                    string.Equals(b.BusNumber, SelectedRoute.BusNumber, StringComparison.OrdinalIgnoreCase));
            }

            SelectedBusId = match?.BusId;
            SelectedDriverId = driverId is int did
                ? AvailableDrivers.FirstOrDefault(d => d.DriverId == did)?.DriverId
                : null;
        }

        private async Task LoadRoutesAsync(bool preserveStatusMessage = false)
        {
            if (!await _loadGate.WaitAsync(0).ConfigureAwait(true))
            {
                return;
            }

            try
            {
                using (LogContext.PushProperty("Operation", "LoadRoutes"))
                {
                    IsRefreshing = true;
                    var result = await _routeService.GetAllRoutesAsync().ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                            ? "Error loading routes"
                            : result.Error;
                        Logger.Warning("GetAllRoutesAsync failed: {Error}", result.Error);
                        return;
                    }

                    var selectedRouteId = SelectedRoute?.RouteId;
                    var routes = result.Value?.OrderBy(r => r.RouteName).ToList() ?? [];
                    Routes.Clear();
                    foreach (var r in routes)
                    {
                        Routes.Add(r);
                    }

                    RoutesView.Refresh();
                    TryRestoreSelectedRoute(selectedRouteId);
                    if (!preserveStatusMessage)
                    {
                        StatusMessage = Routes.Count == 0
                            ? "No routes found — click 'Add Route' to create your first route"
                            : VisibleRouteCount == Routes.Count
                                ? $"Loaded {Routes.Count} routes"
                                : $"Loaded {Routes.Count} routes ({VisibleRouteCount} visible — turn on Show retired routes to see inactive)";
                    }

                    OnPropertyChanged(nameof(TotalRoutes));
                    OnPropertyChanged(nameof(VisibleRouteCount));
                    OnPropertyChanged(nameof(ActiveRoutes));
                    OnPropertyChanged(nameof(TotalAssignedStudents));
                    Logger.Information("Loaded {RouteCount} routes ViaService={ViaService}", Routes.Count, true);
                    RefreshSelectionDependentCommands();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load routes from database");
                StatusMessage = $"Error loading routes: {ex.Message}";
            }
            finally
            {
                IsRefreshing = false;
                _loadGate.Release();
            }
        }

        private void TryRestoreSelectedRoute(int? routeId)
        {
            if (routeId is not int id || id <= 0)
            {
                return;
            }

            SelectedRoute = Routes.FirstOrDefault(r => r.RouteId == id);
        }

        /// <summary>
        /// Predicate used by RoutesView to filter the collection based on QuickSearchText.
        /// </summary>
        private bool FilterRoutes(object obj)
        {
            if (obj is not BusBuddy.Core.Models.Route r)
            {
                return false;
            }

            if (!ShowRetiredRoutes && !r.IsActive)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(QuickSearchText))
            {
                return true;
            }

            var q = QuickSearchText.Trim();
            return (r.RouteName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (r.Description?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (r.School?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (r.Session?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async Task AddRouteAsync()
        {
            if (IsBusy) return;
            try
            {
                using (LogContext.PushProperty("Operation", "AddRoute"))
                {
                    IsBusy = true;
                    var baseName = $"Route {DateTime.UtcNow:HHmmss}";
                    var newRoute = new BusBuddy.Core.Models.Route
                    {
                        RouteName = baseName,
                        School = SelectedRoute?.School ?? string.Empty,
                        Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                        IsActive = true,
                        Session = RouteSession.AM
                    };
                    var result = await _routeService.CreateRouteAsync(newRoute).ConfigureAwait(true);
                    if (!result.IsSuccess || result.Value is null)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                            ? "Error adding route"
                            : result.Error;
                        Logger.Warning("CreateRouteAsync failed: {Error}", result.Error);
                        return;
                    }

                    var persisted = result.Value;
                    Routes.Add(persisted);
                    SelectedRoute = persisted;
                    RoutesView.Refresh();
                    OnPropertyChanged(nameof(TotalRoutes));
                    OnPropertyChanged(nameof(ActiveRoutes));
                    StatusMessage = $"Added route '{persisted.RouteName}'";
                    Logger.Information("Added route {RouteId}:{RouteName} ViaService={ViaService}",
                        persisted.RouteId, persisted.RouteName, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to add route");
                StatusMessage = $"Error adding route: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Persists inline grid edits for the selected route via <see cref="IRouteService"/>.</summary>
        private async Task EditSelectedRouteAsync()
        {
            if (SelectedRoute is null || IsBusy) return;
            try
            {
                using (LogContext.PushProperty("Operation", "EditRoute"))
                using (LogContext.PushProperty("RouteId", SelectedRoute.RouteId))
                {
                    IsBusy = true;
                    SelectedRoute.RouteName = string.IsNullOrWhiteSpace(SelectedRoute.RouteName)
                        ? $"Route-{SelectedRoute.RouteId}"
                        : SelectedRoute.RouteName.Trim();

                    // Keep BusNumber display aligned with AMVehicleId grid combo.
                    if (SelectedRoute.AMVehicleId is int amBusId)
                    {
                        var bus = AvailableBuses.FirstOrDefault(b => b.BusId == amBusId);
                        if (bus is not null)
                        {
                            SelectedRoute.BusNumber = bus.BusNumber;
                        }
                    }

                    var result = await _routeService.UpdateRouteAsync(SelectedRoute).ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                            ? "Error saving route"
                            : result.Error;
                        Logger.Warning("UpdateRouteAsync failed for {RouteId}: {Error}", SelectedRoute.RouteId, result.Error);
                        System.Windows.MessageBox.Show(
                            StatusMessage,
                            "Save Route",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    StatusMessage = $"Saved changes for '{SelectedRoute.RouteName}'";
                    Logger.Information("Updated route {RouteId}:{RouteName} ViaService={ViaService}",
                        SelectedRoute.RouteId, SelectedRoute.RouteName, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save route");
                StatusMessage = $"Error saving route: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteSelectedRouteAsync()
        {
            if (SelectedRoute is null || IsBusy) return;
            var routeToDelete = SelectedRoute;
            try
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Remove '{routeToDelete.RouteName}' from the route list?\n\n"
                    + "Routes with no students, schedules, or trip history are permanently deleted.\n"
                    + "Otherwise the route is retired and hidden here until you turn on Show retired routes.",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (confirm != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = "Delete cancelled";
                    return;
                }

                using (LogContext.PushProperty("Operation", "DeleteRoute"))
                using (LogContext.PushProperty("RouteId", routeToDelete.RouteId))
                {
                    IsBusy = true;
                    var name = routeToDelete.RouteName;
                    var result = await _routeService.DeleteRouteAsync(routeToDelete.RouteId).ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                            ? "Error deleting route"
                            : result.Error;
                        Logger.Warning("DeleteRouteAsync failed for {RouteId}: {Error}", routeToDelete.RouteId, result.Error);
                        System.Windows.MessageBox.Show(
                            StatusMessage,
                            "Delete Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    var retired = result.IsSuccess && !string.IsNullOrWhiteSpace(result.Error);
                    if (retired)
                    {
                        routeToDelete.IsActive = false;
                        SelectedRoute = null;
                        RoutesView.Refresh();
                        OnPropertyChanged(nameof(TotalRoutes));
                        OnPropertyChanged(nameof(ActiveRoutes));
                        StatusMessage = $"Retired and hidden: {name}";
                        Logger.Information(
                            "Retired route {RouteId}:{RouteName} {Message}",
                            routeToDelete.RouteId,
                            name,
                            result.Error);
                        System.Windows.MessageBox.Show(
                            result.Error,
                            "Route retired",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        Routes.Remove(routeToDelete);
                        SelectedRoute = null;
                        RoutesView.Refresh();
                        OnPropertyChanged(nameof(TotalRoutes));
                        OnPropertyChanged(nameof(ActiveRoutes));
                        StatusMessage = $"Permanently deleted route '{name}'";
                        Logger.Information("Deleted route {RouteId}:{RouteName} ViaService={ViaService}",
                            routeToDelete.RouteId, name, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete route");
                StatusMessage = $"Error deleting route: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GenerateScheduleAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }

            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = $"Generating schedule for '{SelectedRoute.RouteName}'...";
                var persisted = await RouteManagementExportHelper
                    .TryPersistScheduleAsync(SelectedRoute, _scheduleService)
                    .ConfigureAwait(true);
                var path = await RouteManagementExportHelper
                    .WriteSchedulePdfAsync(SelectedRoute, printAfter: false, _reportService, _contextFactory)
                    .ConfigureAwait(true);
                StatusMessage = persisted
                    ? $"Schedule saved and opened: {Path.GetFileName(path)}"
                    : $"Schedule PDF opened (assign a bus and driver to persist a calendar row): {Path.GetFileName(path)}";
                UiProofLog.Write(
                    Logger,
                    "Generate Schedule",
                    "RouteManagementView",
                    persisted ? "scheduled" : "pdf-only",
                    SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, "Generate Schedule", "RouteManagementView");
                StatusMessage = $"Error generating schedule: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GenerateRoutesAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                StatusMessage = "Generating routes...";
                var outcome = await RouteGenerationCoordinator.GenerateAsync(
                        FleetKind.HomeToSchool,
                        SelectedRoute?.School,
                        preferSchoolWithStartTime: true,
                        _routeDetermination,
                        _destinations)
                    .ConfigureAwait(true);

                StatusMessage = outcome.StatusMessage;
                UiProofLog.Write(
                    Logger,
                    "Generate Routes",
                    "RouteManagementView",
                    outcome.Success ? "generated" : "failed",
                    outcome.StatusMessage);
                if (!outcome.Success || outcome.Result is null)
                {
                    return;
                }

                await LoadRoutesAsync().ConfigureAwait(true);

                var draft = Routes.FirstOrDefault(r =>
                    r.RouteName.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase));
                if (draft is not null)
                {
                    SelectedRoute = draft;
                }

                _map?.ApplyGenerationResult(outcome.Result);
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, "Generate Routes", "RouteManagementView");
                StatusMessage = $"Error generating routes: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GenerateTransferRoutesAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                StatusMessage = "Generating transfer routes...";
                var outcome = await RouteGenerationCoordinator.GenerateAsync(
                        FleetKind.Transfer,
                        SelectedRoute?.School,
                        preferSchoolWithStartTime: false,
                        _routeDetermination,
                        _destinations)
                    .ConfigureAwait(true);

                await LoadRoutesAsync().ConfigureAwait(true);
                StatusMessage = outcome.StatusMessage;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Generate transfer routes failed");
                StatusMessage = $"Error generating transfer routes: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenRouteAssignmentAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }

            var routeId = SelectedRoute.RouteId;
            var routeName = SelectedRoute.RouteName;
            try
            {
                StatusMessage = $"Opening assignment for '{routeName}'...";
                RouteAssignmentLauncher.ShowDialog(DialogOwner.Resolve(null), SelectedRoute);
                await LoadRoutesAsync(preserveStatusMessage: true).ConfigureAwait(true);
                TryRestoreSelectedRoute(routeId);
                StatusMessage = $"Closed assignment for '{routeName}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Route assignment dialog failed RouteId={RouteId}", routeId);
                StatusMessage = $"Route assignment error for '{routeName}': {ex.Message}";
            }
        }

        public void Dispose()
        {
            // No-op: context is now always local and disposed via using
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(IsRouteSelected))
            {
                RefreshSelectionDependentCommands();
            }
        }

        private void RefreshSelectionDependentCommands()
        {
            _openAssignmentRelay?.NotifyCanExecuteChanged();
            _addRouteRelay?.NotifyCanExecuteChanged();
            _editRouteRelay?.NotifyCanExecuteChanged();
            _deleteRouteRelay?.NotifyCanExecuteChanged();
            _generateScheduleRelay?.NotifyCanExecuteChanged();
            _generateRoutesRelay?.NotifyCanExecuteChanged();
            _generateTransferRoutesRelay?.NotifyCanExecuteChanged();
            _assignVehicleRelay?.NotifyCanExecuteChanged();
            _assignDriverRelay?.NotifyCanExecuteChanged();
            _exportCsvRelay?.NotifyCanExecuteChanged();
            _exportReportRelay?.NotifyCanExecuteChanged();
            _printScheduleRelay?.NotifyCanExecuteChanged();
            _refreshRelay?.NotifyCanExecuteChanged();
            _refreshDrivePathRelay?.NotifyCanExecuteChanged();
            _optimizeStopOrderRelay?.NotifyCanExecuteChanged();
            _copyRouteRelay?.NotifyCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
