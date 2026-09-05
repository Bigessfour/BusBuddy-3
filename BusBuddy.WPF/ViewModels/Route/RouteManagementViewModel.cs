using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Serilog;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Threading;
using System.IO;
using Serilog.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using BusBuddy.WPF;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Phase 2 Route Management ViewModel
    /// Enhanced route planning and management functionality
    /// </summary>
    public class RouteManagementViewModel : INotifyPropertyChanged, IDisposable
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
        private readonly IRoutingService? _routingService;
        private readonly IRouteDeterminationService? _routeDetermination;
        private readonly IDestinationService? _destinations;
        private IStudentService? _studentService;
        private IScheduleService? _scheduleService;
        private RouteExportService? _exportService;
        private IOperationalReportService? _reportService;
        private IRoutePopulationScaffold? _routePopulation;

        private IAsyncRelayCommand _openAssignmentRelay = null!;
        private IAsyncRelayCommand _addRouteRelay = null!;
        private IAsyncRelayCommand _editRouteRelay = null!;
        private IAsyncRelayCommand _deleteRouteRelay = null!;
        private IAsyncRelayCommand _generateScheduleRelay = null!;
        private IAsyncRelayCommand _generateRoutesRelay = null!;
        private IAsyncRelayCommand _generateTransferRoutesRelay = null!;
        private IAsyncRelayCommand _assignVehicleRelay = null!;
        private IAsyncRelayCommand _exportCsvRelay = null!;
        private IAsyncRelayCommand _exportReportRelay = null!;
        private IAsyncRelayCommand _printScheduleRelay = null!;
        private IAsyncRelayCommand _refreshRelay = null!;
        private IAsyncRelayCommand _refreshDrivePathRelay = null!;
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

        private RouteTimeSlot _selectedTimeSlot = RouteTimeSlot.Both;
        /// <summary>
        /// Selected time slot (AM/PM/Both) for vehicle assignment.
        /// </summary>
        public RouteTimeSlot SelectedTimeSlot
        {
            get => _selectedTimeSlot;
            set { _selectedTimeSlot = value; OnPropertyChanged(); }
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
                }
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
        /// <summary>
        /// Number of active routes.
        /// </summary>
        public int ActiveRoutes => Routes.Count(r => r.IsActive);
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
        /// <summary>Alias for <see cref="OpenRouteAssignmentCommand"/> (legacy binding name).</summary>
        public ICommand AssignStudentsCommand { get; private set; } = null!;
        public ICommand AssignVehicleCommand { get; private set; } = null!;
        public ICommand ExportCsvCommand { get; private set; } = null!;
        public ICommand ExportReportCommand { get; private set; } = null!;
        public ICommand PrintScheduleCommand { get; private set; } = null!;
        public ICommand PrintRouteMapsCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand RefreshDrivePathCommand { get; private set; } = null!;
        public ICommand CopyRouteCommand { get; private set; } = null!;

        public RouteManagementViewModel()
        {
            var dependencies = ResolveDependencies();
            _contextFactory = dependencies.ContextFactory;
            _routeService = dependencies.RouteService ?? new RouteService(_contextFactory);
            _routingService = dependencies.RoutingService ?? App.ServiceProvider?.GetService<IRoutingService>();
            _routeDetermination = dependencies.RouteDetermination;
            _destinations = dependencies.Destinations ?? App.ServiceProvider?.GetService<IDestinationService>();
            ResolveOptionalServices();
            InitializeViewModel();
        }

        private static (
            IBusBuddyDbContextFactory ContextFactory,
            IRouteService? RouteService,
            IRoutingService? RoutingService,
            IRouteDeterminationService? RouteDetermination,
            IDestinationService? Destinations) ResolveDependencies()
        {
            var sp = App.ServiceProvider;
            if (sp is not null)
            {
                return (
                    sp.GetRequiredService<IBusBuddyDbContextFactory>(),
                    sp.GetService<IRouteService>(),
                    sp.GetService<IRoutingService>(),
                    sp.GetService<IRouteDeterminationService>(),
                    sp.GetService<IDestinationService>());
            }

            return (new BusBuddyDbContextFactory(), null, null, null, null);
        }

        public RouteManagementViewModel(
            IBusBuddyDbContextFactory contextFactory,
            IRouteService? routeService,
            IRouteDeterminationService? routeDetermination,
            IDestinationService? destinations = null,
            IRoutingService? routingService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _routeService = routeService ?? new RouteService(_contextFactory);
            _routingService = routingService ?? App.ServiceProvider?.GetService<IRoutingService>();
            _routeDetermination = routeDetermination;
            _destinations = destinations ?? App.ServiceProvider?.GetService<IDestinationService>();
            ResolveOptionalServices();
            InitializeViewModel();
        }

        private void ResolveOptionalServices()
        {
            var sp = App.ServiceProvider;
            if (sp is null)
            {
                return;
            }

            _studentService = sp.GetService<IStudentService>();
            _scheduleService = sp.GetService<IScheduleService>();
            _exportService = sp.GetService<RouteExportService>();
            _reportService = sp.GetService<IOperationalReportService>();
            _routePopulation = sp.GetService<IRoutePopulationScaffold>();
        }

        private void InitializeViewModel()
        {
            RoutesView = CollectionViewSource.GetDefaultView(Routes);
            RoutesView.Filter = FilterRoutes;

            _openAssignmentRelay = new AsyncRelayCommand(OpenRouteAssignmentAsync, () => IsRouteSelected && !IsBusy);
            OpenRouteAssignmentCommand = _openAssignmentRelay;
            AssignStudentsCommand = _openAssignmentRelay;
            PrintRouteMapsCommand = _openAssignmentRelay;

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
            _exportCsvRelay = new AsyncRelayCommand(ExportCsvAsync, () => !IsBusy);
            ExportCsvCommand = _exportCsvRelay;
            _exportReportRelay = new AsyncRelayCommand(ExportReportAsync, () => !IsBusy);
            ExportReportCommand = _exportReportRelay;
            _printScheduleRelay = new AsyncRelayCommand(PrintScheduleAsync, () => IsRouteSelected && !IsBusy);
            PrintScheduleCommand = _printScheduleRelay;
            _refreshRelay = new AsyncRelayCommand(LoadRoutesAsync, () => !IsRefreshing);
            RefreshCommand = _refreshRelay;
            _refreshDrivePathRelay = new AsyncRelayCommand(RefreshDrivePathAsync, () => IsRouteSelected && !IsBusy);
            RefreshDrivePathCommand = _refreshDrivePathRelay;
            _copyRouteRelay = new AsyncRelayCommand(CopyRouteAsync, () => IsRouteSelected && !IsBusy);
            CopyRouteCommand = _copyRouteRelay;

            RefreshSelectionDependentCommands();
        }

        /// <summary>Loads routes and assignment buses — call once from view <c>Loaded</c>.</summary>
        public async Task InitializeAsync()
        {
            await Task.WhenAll(EnsureBusesLoadedAsync(), LoadSchoolsAsync(), LoadRoutesAsync()).ConfigureAwait(true);
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
                return;
            }

            var match = AvailableBuses.FirstOrDefault(b =>
                SelectedRoute.AMVehicleId.HasValue && b.BusId == SelectedRoute.AMVehicleId.Value)
                ?? AvailableBuses.FirstOrDefault(b =>
                    !string.IsNullOrWhiteSpace(SelectedRoute.BusNumber)
                    && string.Equals(b.BusNumber, SelectedRoute.BusNumber, StringComparison.OrdinalIgnoreCase));
            SelectedBusId = match?.BusId;
        }

        private async Task LoadRoutesAsync()
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

                    var routes = result.Value?.OrderBy(r => r.RouteName).ToList() ?? [];
                    await EnrichRouteCountsAsync(routes).ConfigureAwait(true);
                    Routes.Clear();
                    foreach (var r in routes)
                    {
                        Routes.Add(r);
                    }

                    RoutesView.Refresh();
                    StatusMessage = Routes.Count == 0
                        ? "No routes found — click 'Add Route' to create your first route"
                        : $"Loaded {Routes.Count} routes";
                    OnPropertyChanged(nameof(TotalRoutes));
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

        /// <summary>
        /// Predicate used by RoutesView to filter the collection based on QuickSearchText.
        /// </summary>
        private bool FilterRoutes(object obj)
        {
            if (obj is not BusBuddy.Core.Models.Route r)
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
                   || (r.School?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async Task AddRouteAsync()
        {
            if (IsBusy) return;
            try
            {
                using (LogContext.PushProperty("Operation", "AddRoute"))
                {
                    IsBusy = true;
                    var baseName = $"Route {DateTime.Now:HHmmss}";
                    var newRoute = new BusBuddy.Core.Models.Route
                    {
                        RouteName = baseName,
                        School = SelectedRoute?.School ?? string.Empty,
                        Date = DateTime.Today,
                        IsActive = true
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

        private async Task RefreshDrivePathAsync()
        {
            if (SelectedRoute is null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = $"Refreshing drive path for '{SelectedRoute.RouteName}'...";
                var refresh = await RouteDrivePathRefresher
                    .TryRefreshAsync(_routingService, SelectedRoute)
                    .ConfigureAwait(true);

                if (refresh.Success)
                {
                    var update = await _routeService.UpdateRouteAsync(SelectedRoute).ConfigureAwait(true);
                    StatusMessage = update.IsSuccess
                        ? $"Drive path updated ({refresh.Path?.DistanceMeters} m, {refresh.Path?.Duration})"
                        : $"Drive path computed but save failed: {update.Error}";
                    return;
                }

                StatusMessage = refresh.Message ?? "Drive path refresh skipped.";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed refreshing drive path");
                StatusMessage = $"Error refreshing drive path: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CopyRouteAsync()
        {
            if (SelectedRoute is null)
            {
                return;
            }

            try
            {
                var sourceName = SelectedRoute.RouteName;
                Logger.Information("Copying route {RouteId}:{RouteName}", SelectedRoute.RouteId, sourceName);
                var result = await _routeService.CloneRouteAsync(
                    SelectedRoute.RouteId,
                    DateTime.Today.AddDays(1),
                    $"Copy of {sourceName}");
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Copy failed: {result.Error}";
                    Logger.Warning("CloneRouteAsync failed for {RouteName}: {Error}", sourceName, result.Error);
                    return;
                }

                await LoadRoutesAsync();
                StatusMessage = $"Copied route '{sourceName}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to copy route");
                StatusMessage = $"Error copying route: {ex.Message}";
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
                    RouteVehicleLinker.TrySyncFromBusNumber(SelectedRoute, AvailableBuses);

                    var result = await _routeService.UpdateRouteAsync(SelectedRoute).ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                            ? "Error saving route"
                            : result.Error;
                        Logger.Warning("UpdateRouteAsync failed for {RouteId}: {Error}", SelectedRoute.RouteId, result.Error);
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
                    $"Delete route '{routeToDelete.RouteName}'? This cannot be undone.",
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
                        return;
                    }

                    Routes.Remove(routeToDelete);
                    SelectedRoute = null;
                    RoutesView.Refresh();
                    OnPropertyChanged(nameof(TotalRoutes));
                    OnPropertyChanged(nameof(ActiveRoutes));
                    StatusMessage = $"Deleted route '{name}'";
                    Logger.Information("Deleted route {RouteId}:{RouteName} ViaService={ViaService}",
                        routeToDelete.RouteId, name, true);
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
                var persisted = await TryPersistScheduleAsync(SelectedRoute).ConfigureAwait(true);
                var path = await WriteSchedulePdfAsync(SelectedRoute, printAfter: false).ConfigureAwait(true);
                StatusMessage = persisted
                    ? $"Schedule saved and opened: {Path.GetFileName(path)}"
                    : $"Schedule PDF opened (assign a bus and driver to persist a calendar row): {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Generate schedule failed");
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
                if (!outcome.Success || outcome.Result is null)
                {
                    return;
                }

                await LoadRoutesAsync().ConfigureAwait(true);
                if (_routePopulation is not null)
                {
                    await _routePopulation.PopulateRoutesAsync().ConfigureAwait(true);
                }

                var draft = Routes.FirstOrDefault(r =>
                    r.RouteName.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase));
                if (draft is not null)
                {
                    SelectedRoute = draft;
                }

                var mapVm = App.ServiceProvider?.GetService<BusBuddy.WPF.ViewModels.Map.MapViewModel>();
                mapVm?.ApplyGenerationResult(outcome.Result);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Generate routes failed");
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

            try
            {
                StatusMessage = $"Opening assignment for '{SelectedRoute.RouteName}'...";
                RouteAssignmentLauncher.ShowDialog(DialogOwner.Resolve(null), SelectedRoute);
                await LoadRoutesAsync().ConfigureAwait(true);
                StatusMessage = $"Closed assignment for '{SelectedRoute.RouteName}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed opening route assignment");
                StatusMessage = $"Error opening assignment: {ex.Message}";
            }
        }

        private async Task AssignVehicleAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }
            if (SelectedBus is null && SelectedBusId is int busId)
            {
                SelectedBus = AvailableBuses.FirstOrDefault(b => b.BusId == busId);
            }
            if (SelectedBus is null)
            {
                StatusMessage = "Select a bus to assign";
                return;
            }
            if (IsBusy) return;
            try
            {
                using (LogContext.PushProperty("Operation", "AssignVehicle"))
                using (LogContext.PushProperty("RouteId", SelectedRoute.RouteId))
                {
                    IsBusy = true;
                    StatusMessage = $"Assigning bus {SelectedBus.BusNumber} to route '{SelectedRoute.RouteName}'...";
                    var result = await _routeService.AssignVehicleToRouteAsync(
                        SelectedRoute.RouteId, SelectedBus.BusId, SelectedTimeSlot).ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error) ? "Assignment failed" : result.Error;
                        Logger.Warning("Vehicle assignment failed: {Message}", result.Error);
                        return;
                    }

                    Logger.Information(
                        "Assigned vehicle {VehicleId} to route {RouteId} for {Slot} ViaService={ViaService}",
                        SelectedBus.BusId, SelectedRoute.RouteId, SelectedTimeSlot, true);
                    await LoadSingleRouteAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                    StatusMessage = $"Assigned bus {SelectedBus.BusNumber} ({SelectedTimeSlot})";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error assigning vehicle to route");
                StatusMessage = $"Error assigning vehicle: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Loads Active / In Service buses for assignment. Reloads so a bus added
        /// in Vehicle Management appears without restarting Route Management.
        /// </summary>
        public async Task EnsureBusesLoadedAsync()
        {
            try
            {
                var result = await _routeService.GetAvailableBusesAsync().ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    Logger.Warning("GetAvailableBusesAsync failed: {Error}", result.Error);
                    return;
                }

                AvailableBuses.Clear();
                foreach (var b in result.Value ?? [])
                {
                    AvailableBuses.Add(b);
                }
                Logger.Debug("Loaded {Count} assignable buses ViaService={ViaService}", AvailableBuses.Count, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading active buses");
            }
        }

        private async Task LoadSingleRouteAsync(int routeId)
        {
            try
            {
                var result = await _routeService.GetRouteByIdAsync(routeId).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    return;
                }

                var updated = result.Value;
                var existing = Routes.FirstOrDefault(r => r.RouteId == routeId);
                if (existing is null)
                {
                    return;
                }

                existing.AMVehicleId = updated.AMVehicleId;
                existing.PMVehicleId = updated.PMVehicleId;
                existing.PMBusId = updated.PMBusId;
                existing.BusNumber = updated.BusNumber;
                OnPropertyChanged(nameof(SelectedRoute));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed refreshing route after assignment");
            }
        }
        private async Task EnrichRouteCountsAsync(IList<BusBuddy.Core.Models.Route> routes)
        {
            if (_studentService is null || routes.Count == 0)
            {
                return;
            }

            try
            {
                var students = await _studentService.GetAllStudentsAsync().ConfigureAwait(true) ?? [];
                foreach (var route in routes)
                {
                    route.StudentCount = students.Count(s =>
                        string.Equals(s.AMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.PMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase));
                    try
                    {
                        var stops = await _routeService.GetRouteStopsAsync(route.RouteId).ConfigureAwait(true);
                        if (stops.IsSuccess)
                        {
                            route.StopCount = stops.Value?.Count() ?? 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex, "Stop count skipped for route {RouteId}", route.RouteId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed enriching route student/stop counts");
            }
        }

        private async Task ExportCsvAsync()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportRoutesCsv"))
                {
                    var fileName = $"BusBuddy_Routes_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    var path = PromptSavePath(fileName, "CSV files (*.csv)|*.csv|All files (*.*)|*.*");
                    if (path is null)
                    {
                        StatusMessage = "Export cancelled";
                        return;
                    }

                    if (_exportService is not null)
                    {
                        var generated = await _exportService.ExportRoutesToCsvAsync().ConfigureAwait(true);
                        if (!string.Equals(generated, path, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(generated, path, overwrite: true);
                        }

                        RevealOrOpen(path);
                        StatusMessage = $"Exported CSV: {Path.GetFileName(path)}";
                        return;
                    }

                    WriteFallbackCsv(path);
                    RevealOrOpen(path);
                    StatusMessage = $"Exported {Routes.Count} routes";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed exporting routes CSV");
                StatusMessage = $"Error exporting routes: {ex.Message}";
            }
        }

        private async Task ExportReportAsync()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportRouteSummary"))
                {
                    var fileName = $"BusBuddy_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    var path = PromptSavePath(fileName, "Text files (*.txt)|*.txt|All files (*.*)|*.*");
                    if (path is null)
                    {
                        StatusMessage = "Export cancelled";
                        return;
                    }

                    if (_exportService is not null)
                    {
                        var generated = await _exportService.GenerateRouteReportAsync().ConfigureAwait(true);
                        if (!string.Equals(generated, path, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(generated, path, overwrite: true);
                        }

                        RevealOrOpen(path);
                        StatusMessage = $"Exported report: {Path.GetFileName(path)}";
                        return;
                    }

                    WriteFallbackReport(path);
                    RevealOrOpen(path);
                    StatusMessage = "Exported route summary";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed exporting route summary");
                StatusMessage = $"Error exporting report: {ex.Message}";
            }
        }

        private async Task PrintScheduleAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = $"Printing schedule for '{SelectedRoute.RouteName}'...";
                var path = await WriteSchedulePdfAsync(SelectedRoute, printAfter: true).ConfigureAwait(true);
                StatusMessage = $"Schedule sent to printer / opened: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed printing schedule");
                StatusMessage = $"Error printing schedule: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string> WriteSchedulePdfAsync(BusBuddy.Core.Models.Route route, bool printAfter)
        {
            var exportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BusBuddy",
                "Printouts");
            Directory.CreateDirectory(exportDir);

            string path;
            if (_reportService is not null)
            {
                var generated = await _reportService.GenerateAsync(new OperationalReportRequest
                {
                    Kind = printAfter ? OperationalReportKind.PrintSchedules : OperationalReportKind.DailySchedule,
                    RouteId = route.RouteId,
                    OutputDirectory = exportDir
                }).ConfigureAwait(true);
                path = generated.FilePath;
            }
            else
            {
                path = RoutePdfPrinter.GenerateRoutePdf(
                    _contextFactory,
                    route.RouteId,
                    exportDir,
                    RouteTimeSlot.Both);
            }

            if (printAfter)
            {
                RevealOrOpen(path, print: true);
            }
            else
            {
                RevealOrOpen(path, print: false);
            }

            return path;
        }

        private async Task<bool> TryPersistScheduleAsync(BusBuddy.Core.Models.Route route)
        {
            if (_scheduleService is null)
            {
                return false;
            }

            var busId = route.AMVehicleId ?? route.PMVehicleId;
            var driverId = route.AMDriverId ?? route.PMDriverId;
            if (!busId.HasValue || !driverId.HasValue)
            {
                return false;
            }

            var date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
            var departure = date.Add(route.AMBeginTime ?? TimeSpan.FromHours(7));
            var arrival = date.Add(route.AMBeginTime ?? TimeSpan.FromHours(7)).AddMinutes(route.EstimatedDuration ?? 45);
            if (arrival <= departure)
            {
                arrival = departure.AddMinutes(45);
            }

            await _scheduleService.AddScheduleAsync(new Schedule
            {
                RouteId = route.RouteId,
                BusId = busId.Value,
                DriverId = driverId.Value,
                ScheduleDate = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                DepartureTime = DateTime.SpecifyKind(departure, DateTimeKind.Utc),
                ArrivalTime = DateTime.SpecifyKind(arrival, DateTimeKind.Utc),
                Location = route.School,
                Notes = $"Generated from Route Management for {route.RouteName}",
                Status = "Scheduled"
            }).ConfigureAwait(true);
            return true;
        }

        private static string? PromptSavePath(string defaultFileName, string filter)
        {
            try
            {
                if (System.Windows.Application.Current is not null)
                {
                    var dialog = new SaveFileDialog
                    {
                        FileName = defaultFileName,
                        Filter = filter,
                        OverwritePrompt = true
                    };
                    return dialog.ShowDialog() == true ? dialog.FileName : null;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SaveFileDialog unavailable; using documents folder");
            }

            var exportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BusBuddy",
                "Exports");
            Directory.CreateDirectory(exportDir);
            return Path.Combine(exportDir, defaultFileName);
        }

        private void WriteFallbackCsv(string fullPath)
        {
            using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
            sw.WriteLine("RouteId,RouteName,Date,Active,StudentCount,StopCount,School,BusNumber");
            foreach (var r in Routes)
            {
                string Csv(string? v)
                {
                    if (string.IsNullOrEmpty(v)) return string.Empty;
                    var esc = v.Replace("\"", "\"\"", StringComparison.Ordinal);
                    return "\"" + esc + "\"";
                }

                sw.WriteLine(string.Join(',', r.RouteId, Csv(r.RouteName), r.Date.ToString("yyyy-MM-dd"), r.IsActive, r.StudentCount ?? 0, r.StopCount ?? 0, Csv(r.School), Csv(r.BusNumber)));
            }
        }

        private void WriteFallbackReport(string fullPath)
        {
            using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
            sw.WriteLine($"Route Summary Export {DateTime.UtcNow:O}");
            sw.WriteLine("====================================");
            foreach (var r in Routes)
            {
                sw.WriteLine($"[{r.RouteId}] {r.RouteName} | School:{r.School} | Bus:{r.BusNumber} | Date:{r.Date:yyyy-MM-dd} | Active:{r.IsActive} | Students:{r.StudentCount ?? 0} | Stops:{r.StopCount ?? 0}");
            }
        }

        private static void RevealOrOpen(string path, bool print = false)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            if (print)
            {
                psi.Verb = "print";
            }

            Process.Start(psi);
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
            _exportCsvRelay?.NotifyCanExecuteChanged();
            _exportReportRelay?.NotifyCanExecuteChanged();
            _printScheduleRelay?.NotifyCanExecuteChanged();
            _refreshRelay?.NotifyCanExecuteChanged();
            _refreshDrivePathRelay?.NotifyCanExecuteChanged();
            _copyRouteRelay?.NotifyCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
