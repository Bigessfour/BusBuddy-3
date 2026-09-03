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
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.WPF;
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
                CommandManager.InvalidateRequerySuggested();
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
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>Combined busy state for status UI bindings.</summary>
        public bool IsLoading => IsBusy || IsRefreshing;

        /// <summary>
        /// Buses available for assignment (Active only) — loaded lazily when first needed.
        /// </summary>
        public ObservableCollection<BusBuddy.Core.Models.Bus> AvailableBuses { get; } = new();

        private BusBuddy.Core.Models.Bus? _selectedBus;
        /// <summary>
        /// Currently selected bus to assign to the selected route.
        /// </summary>
        public BusBuddy.Core.Models.Bus? SelectedBus
        {
            get => _selectedBus;
            set { _selectedBus = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
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
                // Ensure command CanExecute reflects the current selection state
                // Using WPF's CommandManager to prompt a requery for CanExecute
                CommandManager.InvalidateRequerySuggested();
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
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            RoutesView = CollectionViewSource.GetDefaultView(Routes);
            RoutesView.Filter = FilterRoutes;

            OpenRouteAssignmentCommand = new RelayCommand(OpenRouteAssignment, () => IsRouteSelected && !IsBusy);
            AssignStudentsCommand = OpenRouteAssignmentCommand;
            PrintRouteMapsCommand = OpenRouteAssignmentCommand;

            AddRouteCommand = new AsyncRelayCommand(AddRouteAsync, () => !IsBusy);
            EditRouteCommand = new AsyncRelayCommand(EditSelectedRouteAsync, () => IsRouteSelected && !IsBusy);
            DeleteRouteCommand = new AsyncRelayCommand(DeleteSelectedRouteAsync, () => IsRouteSelected && !IsBusy);
            GenerateScheduleCommand = new RelayCommand(GenerateSchedule, () => IsRouteSelected && !IsBusy);
            GenerateRoutesCommand = new AsyncRelayCommand(GenerateRoutesAsync, () => !IsBusy && !IsRefreshing);
            GenerateTransferRoutesCommand = new AsyncRelayCommand(GenerateTransferRoutesAsync, () => !IsBusy && !IsRefreshing);
            AssignVehicleCommand = new AsyncRelayCommand(AssignVehicleAsync, () => IsRouteSelected && SelectedBus != null && !IsBusy);
            ExportCsvCommand = new RelayCommand(ExportCsv);
            ExportReportCommand = new RelayCommand(ExportReport);
            PrintScheduleCommand = new RelayCommand(PrintSchedule);
            RefreshCommand = new AsyncRelayCommand(LoadRoutesAsync, () => !IsRefreshing);
            RefreshDrivePathCommand = new AsyncRelayCommand(RefreshDrivePathAsync, () => IsRouteSelected && !IsBusy);
            CopyRouteCommand = new AsyncRelayCommand(CopyRouteAsync, () => IsRouteSelected && !IsBusy);

            RefreshSelectionDependentCommands();
        }

        /// <summary>Loads routes and assignment buses — call once from view <c>Loaded</c>.</summary>
        public async Task InitializeAsync()
        {
            await Task.WhenAll(EnsureBusesLoadedAsync(), LoadRoutesAsync()).ConfigureAwait(true);
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
        private void GenerateSchedule() => PrintSchedule();

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

        private void OpenRouteAssignment()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }

            try
            {
                StatusMessage = $"Opening assignment for '{SelectedRoute.RouteName}'...";
                RouteAssignmentLauncher.ShowDialog(
                    System.Windows.Application.Current?.MainWindow,
                    SelectedRoute);
                _ = LoadRoutesAsync();
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
        /// Loads Active buses the first time assignment UI is used.
        /// </summary>
        public async Task EnsureBusesLoadedAsync()
        {
            if (AvailableBuses.Count > 0) return;
            try
            {
                var result = await _routeService.GetAvailableBusesAsync().ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    Logger.Warning("GetAvailableBusesAsync failed: {Error}", result.Error);
                    return;
                }

                foreach (var b in result.Value ?? [])
                {
                    AvailableBuses.Add(b);
                }
                Logger.Debug("Loaded {Count} active buses for assignment ViaService={ViaService}", AvailableBuses.Count, true);
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
        private void ExportCsv()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportRoutesCsv"))
                {
                    var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BusBuddy", "Exports");
                    Directory.CreateDirectory(exportDir);
                    var fileName = $"routes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                    var fullPath = Path.Combine(exportDir, fileName);
                    using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
                    sw.WriteLine("RouteId,RouteName,Date,Active,StudentCount,StopCount,School");
                    foreach (var r in Routes)
                    {
                        string Csv(string? v)
                        {
                            if (string.IsNullOrEmpty(v)) return string.Empty;
                            var esc = v.Replace("\"", "\"\"", StringComparison.Ordinal);
                            return "\"" + esc + "\"";
                        }
                        sw.WriteLine(string.Join(',', r.RouteId, Csv(r.RouteName), r.Date.ToString("yyyy-MM-dd"), r.IsActive, r.StudentCount ?? 0, r.StopCount ?? 0, Csv(r.School)));
                    }
                    sw.Flush();
                    StatusMessage = $"Exported {Routes.Count} routes";
                    Logger.Information("Exported {Count} routes to {File}", Routes.Count, fullPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed exporting routes CSV");
                StatusMessage = "Error exporting routes";
            }
        }
        private void ExportReport()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportRouteSummary"))
                {
                    var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BusBuddy", "Exports");
                    Directory.CreateDirectory(exportDir);
                    var fileName = $"route-summary-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                    var fullPath = Path.Combine(exportDir, fileName);
                    using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
                    sw.WriteLine($"Route Summary Export {DateTime.UtcNow:O}");
                    sw.WriteLine("====================================");
                    foreach (var r in Routes)
                    {
                        sw.WriteLine($"[{r.RouteId}] {r.RouteName} | Date:{r.Date:yyyy-MM-dd} | Active:{r.IsActive} | Students:{r.StudentCount ?? 0} | Stops:{r.StopCount ?? 0}");
                    }
                    sw.Flush();
                    StatusMessage = "Exported route summary";
                    Logger.Information("Exported route summary with {Count} routes to {File}", Routes.Count, fullPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed exporting route summary");
                StatusMessage = "Error exporting report";
            }
        }
        private void PrintSchedule()
        {
            try
            {
                if (SelectedRoute == null)
                {
                    StatusMessage = "Select a route first";
                    return;
                }
                using (LogContext.PushProperty("Operation", "PrintSchedule"))
                using (LogContext.PushProperty("RouteId", SelectedRoute.RouteId))
                {
                    var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BusBuddy", "Printouts");
                    Directory.CreateDirectory(exportDir);
                    var fileName = $"route-{SelectedRoute.RouteId}-schedule-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                    var fullPath = Path.Combine(exportDir, fileName);
                    using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
                    sw.WriteLine($"Schedule for {SelectedRoute.RouteName} ({SelectedRoute.Date:yyyy-MM-dd})");
                    sw.WriteLine($"Active: {SelectedRoute.IsActive}  Students: {SelectedRoute.StudentCount ?? 0}  Stops: {SelectedRoute.StopCount ?? 0}");
                    sw.WriteLine("(Detailed stop listing not included)");
                    sw.Flush();
                    StatusMessage = "Printed schedule (text)";
                    Logger.Information("Printed schedule for route {RouteId} to {File}", SelectedRoute.RouteId, fullPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed printing schedule");
                StatusMessage = "Error printing schedule";
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
            // If selection state changed, refresh commands depending on it
            if (propertyName == nameof(IsRouteSelected))
            {
                RefreshSelectionDependentCommands();
            }
        }

        private void RefreshSelectionDependentCommands()
        {
            // Our lightweight RelayCommand implementation wires CanExecuteChanged to CommandManager.RequerySuggested
            // so forcing a global requery is sufficient.
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
