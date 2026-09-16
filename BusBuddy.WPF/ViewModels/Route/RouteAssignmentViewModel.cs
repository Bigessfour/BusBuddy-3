using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Commands;
using Serilog;
using Microsoft.Extensions.DependencyInjection; // For resolving MapViewModel / services
using BusBuddy.WPF.ViewModels.Map; // Map markers
using BusBuddy.WPF.Utilities; // MapMarkerLabels pin kinds
using BusBuddy.WPF.Views.Route;
using BusBuddy.WPF.Views.Driver;
using BusBuddy.WPF.ViewModels.Driver;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces; // IGeocodingService
using System.Globalization;
using System.IO; // For PDF export file writing
using System.Threading; // For debounce timer
using System.Text.RegularExpressions; // Start time validation

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Enhanced ViewModel for Route Assignment and Route Building
    /// Implements comprehensive route building workflow with MVVM compliance
    /// Supports Syncfusion SfDataGrid integration and Result pattern error handling
    /// </summary>
    public partial class RouteAssignmentViewModel : INotifyPropertyChanged, IDisposable
    {
        // Backing fields for all properties (restored for CS0103 fix)
        private ObservableCollection<BusBuddy.Core.Models.Route> _availableRoutes = new();
        private ObservableCollection<BusBuddy.Core.Models.Bus> _availableBuses = new();
        private ObservableCollection<BusBuddy.Core.Models.Driver> _availableDrivers = new();
        private ObservableCollection<RouteStop> _routeStops = new();
        private ObservableCollection<BusBuddy.Core.Models.Student> _assignedStudentsForSelectedRoute = new();
        private ObservableCollection<BusBuddy.Core.Models.Student> _unassignedStudents = new();
        private readonly List<BusBuddy.Core.Models.Student> _allUnassignedStudents = new();
        private BusBuddy.Core.Models.Student? _selectedStudent;
        private BusBuddy.Core.Models.Student? _selectedAssignedStudent;
        private BusBuddy.Core.Models.Route? _selectedRoute;
        private BusBuddy.Core.Models.Bus? _selectedBus;
        private BusBuddy.Core.Models.Driver? _selectedDriver;
        private RouteStop? _selectedRouteStop;
        private BusBuddy.Core.Models.RouteTimeSlot _selectedTimeSlot = BusBuddy.Core.Models.RouteTimeSlot.AM;
        private bool _isLoading;
        private bool _isGeneratingRoutes;
        private string _studentSearchText = string.Empty;
        private string _statusMessage = string.Empty;
        private int? _preselectedRouteId;
        private string _startTimeString = "07:30";
        private readonly IRouteService _routeService;
        private readonly IRouteDeterminationService? _routeDetermination;
        private readonly IDestinationService? _destinations;
        private readonly MapViewModel? _map;
        private static readonly ILogger Logger = Log.ForContext<RouteAssignmentViewModel>();
        private Timer? _retimeDebounceTimer; // Debounce timer for auto-retiming after structural stop changes
        private const int RetimeDebounceMs = 600; // Delay before auto timing after modifications
        private static readonly Regex StartTimeRegex = new(@"^\s*(?:[01]?\d|2[0-3]):[0-5]\d\s*$", RegexOptions.Compiled); // HH:mm 24h

        // Compact helpers for robust display names in logs/status
        private static string GetStudentDisplayName(BusBuddy.Core.Models.Student? s)
        {
            if (s is null) return "(unknown student)";
            if (!string.IsNullOrWhiteSpace(s.StudentName)) return s.StudentName!;
            if (!string.IsNullOrWhiteSpace(s.StudentNumber)) return $"Student #{s.StudentNumber}";
            return $"StudentId {s.StudentId}";
        }

        private static string GetRouteDisplayName(BusBuddy.Core.Models.Route? r)
        {
            if (r is null) return "(route)";
            return string.IsNullOrWhiteSpace(r.RouteName) ? $"RouteId {r.RouteId}" : r.RouteName!;
        }

        public RouteAssignmentViewModel(IRouteService routeService)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            (_routeDetermination, _destinations, _map) = ResolveGenerateServices();
            Initialize();
        }

        public RouteAssignmentViewModel(IRouteService routeService, BusBuddy.Core.Models.Route preselectedRoute)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            (_routeDetermination, _destinations, _map) = ResolveGenerateServices();
            _preselectedRouteId = preselectedRoute?.RouteId;
            Initialize();
            if (preselectedRoute != null)
            {
                SelectedRoute = preselectedRoute;
            }
        }

        private static (IRouteDeterminationService? Planner, IDestinationService? Destinations, MapViewModel? Map)
            ResolveGenerateServices()
        {
            var sp = App.ServiceProvider;
            return (
                sp?.GetService<IRouteDeterminationService>(),
                sp?.GetService<IDestinationService>(),
                sp?.GetService<MapViewModel>());
        }

        private void Initialize()
        {
            Logger.Information("RouteAssignmentViewModel initializing PreselectedRouteId={PreselectedRouteId}", _preselectedRouteId);
            InitializeCommands();
            // Kick off data load async (fire & forget)
            _ = LoadDataFromServiceAsync();
            _retimeDebounceTimer = new Timer(_ =>
            {
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (SelectedRoute != null && RouteStops.Any())
                        {
                            Logger.Debug("Auto-retiming route stops (debounced)");
                            _ = TimeRouteStopsAsync();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Auto-retime debounce execution failed");
                }
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>Unassigned students for assignment.</summary>
        public ObservableCollection<BusBuddy.Core.Models.Student> UnassignedStudents
        {
            get => _unassignedStudents;
            set => SetProperty(ref _unassignedStudents, value);
        }

        /// <summary>Available routes to assign to.</summary>
        public ObservableCollection<BusBuddy.Core.Models.Route> AvailableRoutes
        {
            get => _availableRoutes;
            set => SetProperty(ref _availableRoutes, value);
        }

        /// <summary>Available buses for assignment.</summary>
        public ObservableCollection<BusBuddy.Core.Models.Bus> AvailableBuses
        {
            get => _availableBuses;
            set => SetProperty(ref _availableBuses, value);
        }

        /// <summary>Available drivers for assignment.</summary>
        public ObservableCollection<BusBuddy.Core.Models.Driver> AvailableDrivers
        {
            get => _availableDrivers;
            set => SetProperty(ref _availableDrivers, value);
        }

        /// <summary>Stops belonging to the selected/working route.</summary>
        public ObservableCollection<RouteStop> RouteStops
        {
            get => _routeStops;
            set => SetProperty(ref _routeStops, value);
        }

        /// <summary>
        /// User-entered base start time for timing route stops (HH:mm). Defaults to 07:30.
        /// </summary>
        public string StartTimeString
        {
            get => _startTimeString;
            set
            {
                if (SetProperty(ref _startTimeString, value))
                {
                    OnPropertyChanged(nameof(IsStartTimeValid));
                    // Provide immediate feedback if user entered an invalid value
                    if (!IsStartTimeValid)
                    {
                        StatusMessage = "Invalid start time (use HH:mm 24-hour, e.g. 07:30)";
                    }
                    (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsStartTimeValid => StartTimeRegex.IsMatch(_startTimeString);

        // Students currently assigned to the SelectedRoute
        public ObservableCollection<BusBuddy.Core.Models.Student> AssignedStudentsForSelectedRoute
        {
            get => _assignedStudentsForSelectedRoute;
            set => SetProperty(ref _assignedStudentsForSelectedRoute, value);
        }

        // Selection Properties
        public BusBuddy.Core.Models.Student? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (SetProperty(ref _selectedStudent, value))
                {
                    OnPropertyChanged(nameof(CanAssignStudent));
                    RefreshCommandStates();
                }
            }
        }

        public BusBuddy.Core.Models.Student? SelectedAssignedStudent
        {
            get => _selectedAssignedStudent;
            set
            {
                if (SetProperty(ref _selectedAssignedStudent, value))
                {
                    OnPropertyChanged(nameof(CanRemoveStudent));
                    OnPropertyChanged(nameof(CanMarkNotRidingToday));
                    RefreshCommandStates();
                }
            }
        }

        public BusBuddy.Core.Models.Route? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                if (SetProperty(ref _selectedRoute, value))
                {
                    if (value != null)
                    {
                        var slot = RouteSession.ToAssignmentSlot(value);
                        if (_selectedTimeSlot != slot)
                        {
                            _selectedTimeSlot = slot;
                            OnPropertyChanged(nameof(SelectedTimeSlot));
                        }
                    }

                    OnPropertyChanged(nameof(CanAssignStudent));
                    OnPropertyChanged(nameof(CanRemoveStudent));
                    OnPropertyChanged(nameof(CanMarkNotRidingToday));
                    OnPropertyChanged(nameof(AssignedStudentCount));
                    OnPropertyChanged(nameof(SelectedRouteBusDisplay));
                    OnPropertyChanged(nameof(SelectedRouteDriverDisplay));
                    OnPropertyChanged(nameof(CanActivateRoute));
                    OnPropertyChanged(nameof(CanDeactivateRoute));
                    _ = LoadRouteStopsAsync(); // Load stops asynchronously
                    UpdateStatusMessage();
                    RefreshCommandStates();
                }
            }
        }

        public BusBuddy.Core.Models.Bus? SelectedBus
        {
            get => _selectedBus;
            set
            {
                if (SetProperty(ref _selectedBus, value))
                {
                    OnPropertyChanged(nameof(CanAssignVehicle));
                    OnPropertyChanged(nameof(SelectedRouteBusDisplay));
                    RefreshCommandStates();
                }
            }
        }

        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (SetProperty(ref _selectedDriver, value))
                {
                    OnPropertyChanged(nameof(CanAssignDriver));
                    OnPropertyChanged(nameof(SelectedRouteDriverDisplay));
                    RefreshCommandStates();
                }
            }
        }

        public RouteStop? SelectedRouteStop
        {
            get => _selectedRouteStop;
            set
            {
                if (SetProperty(ref _selectedRouteStop, value))
                {
                    OnPropertyChanged(nameof(CanRemoveStop));
                    OnPropertyChanged(nameof(CanMoveStopUp));
                    OnPropertyChanged(nameof(CanMoveStopDown));
                }
            }
        }

        public BusBuddy.Core.Models.RouteTimeSlot SelectedTimeSlot
        {
            get => _selectedTimeSlot;
            set
            {
                if (SetProperty(ref _selectedTimeSlot, value))
                {
                    OnPropertyChanged(nameof(SelectedRouteBusDisplay));
                    OnPropertyChanged(nameof(SelectedRouteDriverDisplay));
                    _ = ReloadStudentListsForRouteAsync();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // Search and UI State
        public string StudentSearchText
        {
            get => _studentSearchText;
            set
            {
                if (SetProperty(ref _studentSearchText, value))
                {
                    FilterStudents();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Display helpers for selected route assignments
        public string SelectedRouteBusDisplay
        {
            get
            {
                if (SelectedRoute == null)
                    return string.Empty;
                var id = SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM ? SelectedRoute.PMVehicleId : SelectedRoute.AMVehicleId;
                var bus = id.HasValue ? AvailableBuses.FirstOrDefault(b => b.BusId == id.Value) : null;
                return bus?.BusNumber ?? "(none)";
            }
        }

        public string SelectedRouteDriverDisplay
        {
            get
            {
                if (SelectedRoute == null)
                    return string.Empty;
                var id = SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM ? SelectedRoute.PMDriverId : SelectedRoute.AMDriverId;
                var d = id.HasValue ? AvailableDrivers.FirstOrDefault(x => x.DriverId == id.Value) : null;
                return d?.DriverName ?? "(none)";
            }
        }

        private static BusBuddy.Core.Models.RouteTimeSlot NormalizeTimeSlot(BusBuddy.Core.Models.RouteTimeSlot slot)
        {
            return slot == BusBuddy.Core.Models.RouteTimeSlot.Both
                ? BusBuddy.Core.Models.RouteTimeSlot.AM
                : slot;
        }

        /// <summary>
        /// Assign a student via drag-and-drop onto the assigned grid.
        /// </summary>
        public Task<bool> TryAssignStudentViaDrag(BusBuddy.Core.Models.Student student)
        {
            return AssignStudentCoreAsync(student);
        }

        /// <summary>
        /// Remove a student via drag-and-drop onto the unassigned grid.
        /// </summary>
        public Task<bool> TryRemoveStudentViaDrag(BusBuddy.Core.Models.Student student)
        {
            return RemoveStudentCoreAsync(student);
        }

        public bool CanDragAssign => SelectedRoute != null && !IsLoading;
        public int UnassignedStudentCount => UnassignedStudents?.Count ?? 0;
        public int AssignedStudentCount => AssignedStudentsForSelectedRoute?.Count ?? 0;
        public int RouteStopCount => RouteStops?.Count ?? 0;

        // Command Availability Properties
        public bool CanAssignStudent => SelectedStudent != null && SelectedRoute != null && !IsLoading;
        public bool CanRemoveStudent => SelectedAssignedStudent != null && SelectedRoute != null && !IsLoading;
        public bool CanMarkNotRidingToday => CanRemoveStudent;
        public bool CanSaveRoute => SelectedRoute != null && !IsLoading;
        public bool CanActivateRoute => SelectedRoute != null && !SelectedRoute.IsActive && !IsLoading;
        public bool CanDeactivateRoute => SelectedRoute != null && SelectedRoute.IsActive && !IsLoading;
        public bool CanAssignVehicle => SelectedRoute != null && SelectedBus != null && !IsLoading;
        public bool CanAssignDriver => SelectedRoute != null && SelectedDriver != null && !IsLoading;
        public bool CanAddStop => SelectedRoute != null && !IsLoading;
        public bool CanRemoveStop => SelectedRouteStop != null && !IsLoading;
        public bool CanMoveStopUp => SelectedRouteStop != null && RouteStops.IndexOf(SelectedRouteStop) > 0 && !IsLoading;
        public bool CanMoveStopDown => SelectedRouteStop != null && RouteStops.IndexOf(SelectedRouteStop) < RouteStops.Count - 1 && !IsLoading;

        #region Commands

        // Existing Commands
        public ICommand AssignStudentCommand { get; private set; } = null!;
        public ICommand RemoveStudentCommand { get; private set; } = null!;
        public ICommand MarkNotRidingTodayCommand { get; private set; } = null!;
        public ICommand AutoAssignCommand { get; private set; } = null!;
        public ICommand SaveRouteCommand { get; private set; } = null!;
        public ICommand DeleteRouteCommand { get; private set; } = null!;
        public ICommand ViewScheduleCommand { get; private set; } = null!;
        public ICommand RefreshDataCommand { get; private set; } = null!;
        public ICommand GenerateReportCommand { get; private set; } = null!;

        // Enhanced Route Building Commands
        public ICommand AssignVehicleCommand { get; private set; } = null!;
        public ICommand AssignDriverCommand { get; private set; } = null!;
        public ICommand AddStopCommand { get; private set; } = null!;
        public ICommand RemoveStopCommand { get; private set; } = null!;
        public ICommand MoveStopUpCommand { get; private set; } = null!;
        public ICommand MoveStopDownCommand { get; private set; } = null!;
        public ICommand ActivateRouteCommand { get; private set; } = null!;
        public ICommand DeactivateRouteCommand { get; private set; } = null!;
        // Plot currently assigned students for selected route

        public ICommand PlotRouteOnMapCommand { get; private set; } = null!;
        public ICommand TimeRouteCommand { get; private set; } = null!; // Basic stop timing
        public ICommand PrintMapCommand { get; private set; } = null!;
        public ICommand GenerateRoutesCommand { get; private set; } = null!;
        public ICommand GenerateTransferRoutesCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            // Existing Commands
            AssignStudentCommand = new RelayCommand(async () => await AssignStudentAsync(), () => CanAssignStudent);
            RemoveStudentCommand = new RelayCommand(async () => await RemoveStudentAsync(), () => CanRemoveStudent);
            MarkNotRidingTodayCommand = new RelayCommand(async () => await MarkNotRidingTodayAsync(), () => CanMarkNotRidingToday);
            AutoAssignCommand = new RelayCommand(async () => await AutoAssignStudentsAsync());
            SaveRouteCommand = new RelayCommand(async () => await SaveRouteAsync(), () => CanSaveRoute);
            DeleteRouteCommand = new RelayCommand(async () => await DeleteRouteAsync());
            ViewScheduleCommand = new RelayCommand(async () => await ViewScheduleAsync(), () => SelectedRoute != null);
            RefreshDataCommand = new RelayCommand(async () => await RefreshDataAsync());
            GenerateReportCommand = new RelayCommand(GenerateReport);

            // Enhanced Route Building Commands
            AssignVehicleCommand = new RelayCommand(async () => await AssignVehicleAsync(), () => CanAssignVehicle);
            AssignDriverCommand = new RelayCommand(async () => await AssignDriverAsync(), () => CanAssignDriver);
            AddStopCommand = new RelayCommand(async () => await AddStopAsync(), () => CanAddStop);
            RemoveStopCommand = new RelayCommand(async () => await RemoveStopAsync(), () => CanRemoveStop);
            MoveStopUpCommand = new RelayCommand(async () => await MoveStopUpAsync(), () => CanMoveStopUp);
            MoveStopDownCommand = new RelayCommand(async () => await MoveStopDownAsync(), () => CanMoveStopDown);
            ActivateRouteCommand = new RelayCommand(async () => await ActivateRouteAsync(), () => CanActivateRoute);
            DeactivateRouteCommand = new RelayCommand(async () => await DeactivateRouteAsync(), () => CanDeactivateRoute);
            PlotRouteOnMapCommand = new RelayCommand(async () => await PlotRouteOnMapAsync(), () => SelectedRoute != null);
            TimeRouteCommand = new RelayCommand(async () => await TimeRouteStopsAsync(), () => SelectedRoute != null && RouteStops.Any() && IsStartTimeValid);
            PrintMapCommand = new RelayCommand(PrintMap, () => SelectedRoute != null);
            GenerateRoutesCommand = new RelayCommand(async () => await GenerateRoutesAsync(), () => !_isGeneratingRoutes);
            GenerateTransferRoutesCommand = new RelayCommand(async () => await GenerateTransferRoutesAsync(), () => !_isGeneratingRoutes);
            // Re-evaluate map/ timing commands
            (PlotRouteOnMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Central helper to re-evaluate all command CanExecute states after CRUD/state changes.
        /// </summary>
        private void RefreshCommandStates()
        {
            (AssignStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MarkNotRidingTodayCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ViewScheduleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AssignVehicleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AssignDriverCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddStopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveStopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveStopUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveStopDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ActivateRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeactivateRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PlotRouteOnMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateRoutesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateTransferRoutesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        private void PrintMap()
        {
            ExportRouteAssignmentPdfAsync(includeMap: true);
        }

        private void ExportRouteAssignmentPdfAsync(bool includeMap)
        {
            if (SelectedRoute == null)
            {
                return;
            }

            try
            {
                Logger.Information("Starting route PDF export for {RouteName} (Slot {Slot})", SelectedRoute.RouteName, SelectedTimeSlot);
                var pdfService = App.ServiceProvider?.GetService<BusBuddy.Core.Services.PdfReportService>()
                                   ?? new BusBuddy.Core.Services.PdfReportService();

                byte[]? mapPng = null;
                if (includeMap)
                {
                    try
                    {
                        var mapVm = _map;
                        if (mapVm != null)
                        {
                            if (mapVm.LatestMapSnapshotPng == null || mapVm.LatestMapSnapshotPng.Length == 0)
                            {
                                Logger.Debug("No existing map snapshot; attempting proactive capture");
                                TryProactiveMapSnapshotCapture();
                            }
                            mapPng = mapVm.LatestMapSnapshotPng;
                        }
                    }
                    catch { /* Non-fatal if map VM unavailable */ }
                }

                BusBuddy.Core.Models.Bus? bus = null;
                BusBuddy.Core.Models.Driver? driver = null;
                if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.AM && SelectedRoute.AMVehicleId.HasValue)
                {
                    bus = AvailableBuses.FirstOrDefault(b => b.BusId == SelectedRoute.AMVehicleId.Value);
                }
                if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM && SelectedRoute.PMVehicleId.HasValue)
                {
                    bus = AvailableBuses.FirstOrDefault(b => b.BusId == SelectedRoute.PMVehicleId.Value);
                }
                if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.AM && SelectedRoute.AMDriverId.HasValue)
                {
                    driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.AMDriverId.Value);
                }
                if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM && SelectedRoute.PMDriverId.HasValue)
                {
                    driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.PMDriverId.Value);
                }

                var pdfBytes = pdfService.GenerateRouteSummaryReport(
                    SelectedRoute,
                    RouteStops.ToList(),
                    AssignedStudentsForSelectedRoute.ToList(),
                    bus,
                    driver,
                    NormalizeTimeSlot(SelectedTimeSlot),
                    mapPng);

                if (pdfBytes.Length == 0)
                {
                    StatusMessage = $"Failed to generate PDF for {SelectedRoute.RouteName}";
                    return;
                }

                var safeName = string.Join("_", (SelectedRoute.RouteName ?? "Route").Split(Path.GetInvalidFileNameChars()));
                var fileName = $"Route_{safeName}_{SelectedTimeSlot}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var exportDir = Path.Combine(AppContext.BaseDirectory, "Exports");
                Directory.CreateDirectory(exportDir);
                var fullPath = Path.Combine(exportDir, fileName);
                File.WriteAllBytes(fullPath, pdfBytes);
                StatusMessage = $"Route PDF exported: {fileName}" + (mapPng != null ? " (with map)" : "");
                Logger.Information("Route PDF export complete: {File} (MapEmbedded={HasMap}) Size={SizeBytes} bytes", fullPath, mapPng != null, pdfBytes.Length);
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF export error: {ex.Message}";
                Logger.Error(ex, "Route PDF export failed for {RouteId}", SelectedRoute?.RouteId);
            }
        }

        /// <summary>
        /// Attempts to proactively capture a map snapshot by locating an existing MapView instance in visual trees.
        /// Scans Application.Current.Windows for a MapView and invokes its internal snapshot via reflection.
        /// If none found, logs and returns silently. Avoids tight coupling until a formal capture command is exposed.
        /// </summary>
        private void TryProactiveMapSnapshotCapture()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;
                foreach (Window w in app.Windows)
                {
                    // Depth-first search visual tree for MapView type
                    var target = FindDescendantByTypeName(w, "MapView");
                    if (target != null)
                    {
                        var m = target.GetType().GetMethod("TryCaptureMapSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (m != null)
                        {
                            m.Invoke(target, null);
                            Logger.Debug("Invoked TryCaptureMapSnapshot via reflection on MapView");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Proactive map snapshot capture attempt failed (non-fatal)");
            }
        }

        // Simple visual tree walker (recursive)
        private static System.Windows.DependencyObject? FindDescendantByTypeName(System.Windows.DependencyObject root, string typeName)
        {
            if (root == null) return null;
            if (root.GetType().Name == typeName) return root;
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                var match = FindDescendantByTypeName(child, typeName);
                if (match != null) return match;
            }
            return null;
        }

        #region IDisposable
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _retimeDebounceTimer?.Dispose();
                Logger.Debug("Disposed RouteAssignmentViewModel resources (debounce timer)");
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Dispose encountered a non-fatal exception");
            }
            GC.SuppressFinalize(this);
        }
        ~RouteAssignmentViewModel()
        {
            Dispose();
        }
        #endregion

        #endregion

        #region Command Implementations

        // Enhanced Student Assignment Commands
        private async Task AssignStudentAsync()
        {
            if (SelectedStudent == null)
            {
                return;
            }

            await AssignStudentCoreAsync(SelectedStudent);
        }

        private async Task<bool> AssignStudentCoreAsync(BusBuddy.Core.Models.Student student)
        {
            if (student == null || SelectedRoute == null || IsLoading)
            {
                return false;
            }

            try
            {
                IsLoading = true;
                var route = SelectedRoute;
                var slot = NormalizeTimeSlot(SelectedTimeSlot);
                var studentName = GetStudentDisplayName(student);
                var routeName = GetRouteDisplayName(route);
                RefreshCommandStates();

                if (AssignedStudentsForSelectedRoute.Any(s => s.StudentId == student.StudentId))
                {
                    StatusMessage = $"{student.StudentName} is already on {routeName} ({slot})";
                    return true;
                }

                var overrideSeating = false;
                if (_routeDetermination is not null)
                {
                    var slotKind = slot == RouteTimeSlot.AM
                        ? RouteTimeSlotKind.AM
                        : RouteTimeSlotKind.PM;
                    var fitness = await _routeDetermination
                        .RecalculateOnAssignAsync(student.StudentId, route.RouteId, slotKind)
                        .ConfigureAwait(true);

                    if (fitness.Severity == AssignFitnessSeverity.Warn && fitness.Reasons.Count > 0)
                    {
                        StatusMessage = string.Join("; ", fitness.Reasons);
                        MessageBox.Show(
                            string.Join("\n", fitness.Reasons),
                            "Assignment warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    if (!fitness.Allowed)
                    {
                        // Special-needs pairing is not a seating question, so the seating override below
                        // must never be offered for it: a special-needs child rides a special-needs route
                        // and a special-needs route carries only those children.
                        if (StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student)
                            != StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route))
                        {
                            var mismatch = StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student)
                                ? $"{studentName} requires a special-needs route. Pick a special-needs route (home pickup, equipped bus, aide)."
                                : $"{routeName} is a special-needs route. Assign {studentName} to a general route instead.";
                            StatusMessage = mismatch;
                            Logger.Information(
                                "Assignment refused — special-needs mismatch Student={StudentId} Route={RouteId}",
                                student.StudentId,
                                route.RouteId);
                            MessageBox.Show(mismatch, "Assignment blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                        var body = string.Join("\n", fitness.Reasons);
                        if (fitness.SuggestedRouteIds.Count > 0)
                        {
                            body += "\n\nSuggested route IDs: " + string.Join(", ", fitness.SuggestedRouteIds);
                        }

                        if (fitness.SuggestNewRoute && student.DestinationId is int schoolId)
                        {
                            var gen = MessageBox.Show(
                                body + "\n\nCreate new draft routes for this student's school?",
                                "Assignment blocked",
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Warning);
                            if (gen == MessageBoxResult.Yes)
                            {
                                var genResult = await _routeDetermination.GenerateAndAssignAsync(
                                        schoolId,
                                        RouteTimeSlotKind.Both,
                                        FleetKind.HomeToSchool)
                                    .ConfigureAwait(true);
                                StatusMessage = genResult.Success
                                    ? $"Generated {genResult.Proposals.Count} draft route(s)"
                                    : (genResult.Error ?? "Route generation failed");
                                await LoadDataFromServiceAsync().ConfigureAwait(true);
                                return false;
                            }

                            if (gen == MessageBoxResult.Cancel)
                            {
                                StatusMessage = "Assignment cancelled";
                                return false;
                            }
                        }

                        var askOverride = MessageBox.Show(
                            body + "\n\nOverride seating capacity and assign anyway?",
                            "Assignment blocked",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        if (askOverride != MessageBoxResult.Yes)
                        {
                            StatusMessage = "Assignment blocked: " + string.Join("; ", fitness.Reasons);
                            return false;
                        }

                        overrideSeating = true;
                    }
                }

                var result = await _routeService.AssignStudentToRouteAsync(
                    student.StudentId, route.RouteId, slot, overrideSeating);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to assign student: {result.Error}";
                    MessageBox.Show(result.Error!, "Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                await ReloadStudentListsForRouteAsync();
                StatusMessage = $"Successfully assigned {studentName} to {routeName} ({slot})";
                Logger.Information("Student {StudentName} assigned to route {RouteName} ({Slot})", studentName, routeName, slot);

                if (ReferenceEquals(SelectedStudent, student))
                {
                    SelectedStudent = null;
                }

                (AssignStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MarkNotRidingTodayCommand as RelayCommand)?.RaiseCanExecuteChanged();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign student to route");
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to assign student: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveStudentAsync()
        {
            if (SelectedAssignedStudent == null)
            {
                return;
            }

            await RemoveStudentCoreAsync(SelectedAssignedStudent);
        }

        /// <summary>
        /// Same-day not riding. Does not delete the published stop or the year assignment.
        /// </summary>
        private async Task MarkNotRidingTodayAsync()
        {
            if (SelectedAssignedStudent == null || SelectedRoute == null)
            {
                return;
            }

            var student = SelectedAssignedStudent;
            var route = SelectedRoute;
            var stopCountBefore = RouteStops.Count;
            var am = student.AMRoute;
            var pm = student.PMRoute;

            try
            {
                IsLoading = true;
                var result = await _routeService.RecordRiderExceptionAsync(
                    route.RouteId,
                    student.StudentId,
                    // Same UTC-labelled calendar day the route and its stop ETAs use, so "today"
                    // means one day for both and the per-day exception key cannot split in two.
                    DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                    "Not riding today");
                if (!result.IsSuccess)
                {
                    StatusMessage = result.Error ?? "Could not record not-riding exception";
                    return;
                }

                StatusMessage =
                    $"{student.StudentName} not riding today — published stops and year assignment unchanged";
                Logger.Information(
                    "Rider exception recorded Student={StudentId} Route={RouteId} Stops={Stops} AMRoute={AM} PMRoute={PM}",
                    student.StudentId,
                    route.RouteId,
                    stopCountBefore,
                    am,
                    pm);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to record rider exception");
                StatusMessage = $"Failed to mark not riding: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                RefreshCommandStates();
            }
        }

        private async Task<bool> RemoveStudentCoreAsync(BusBuddy.Core.Models.Student student)
        {
            if (student == null || SelectedRoute == null || IsLoading)
            {
                return false;
            }

            try
            {
                IsLoading = true;
                var route = SelectedRoute;
                var slot = NormalizeTimeSlot(SelectedTimeSlot);
                var studentName = GetStudentDisplayName(student);
                var routeName = GetRouteDisplayName(route);
                StatusMessage = $"Removing {studentName} from {routeName} ({slot})...";

                var result = await _routeService.RemoveStudentFromRouteAsync(student.StudentId, route.RouteId, slot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to remove student: {result.Error}";
                    MessageBox.Show(result.Error!, "Removal Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                await ReloadStudentListsForRouteAsync();
                StatusMessage = $"Successfully removed {studentName} from {routeName} ({slot})";
                Logger.Information("Student {StudentName} removed from route {RouteName} ({Slot})", studentName, routeName, slot);

                if (ReferenceEquals(SelectedAssignedStudent, student))
                {
                    SelectedAssignedStudent = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to remove student from route");
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to remove student: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AutoAssignStudentsAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                MessageBox.Show("Please select a route first.", "Route Required",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                IsLoading = true;
                var slot = NormalizeTimeSlot(SelectedTimeSlot);
                StatusMessage = $"Auto-assigning students to {SelectedRoute.RouteName} ({slot})...";

                var result = await _routeService.AutoAssignStudentsAsync(SelectedRoute.RouteId, slot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Auto-assignment failed: {result.Error}";
                    MessageBox.Show(result.Error!, "Auto-Assign Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await ReloadStudentListsForRouteAsync();
                var count = result.Value?.Count ?? 0;
                StatusMessage = $"Auto-assigned {count} student(s) to {SelectedRoute.RouteName} ({slot})";
                Logger.Information("Auto-assignment completed for route {RouteName} ({Slot}): {Count} students",
                    SelectedRoute.RouteName, slot, count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to auto-assign students");
                StatusMessage = $"Auto-assignment failed: {ex.Message}";
                MessageBox.Show($"Auto-assignment failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Vehicle and Driver Assignment Commands
        private async Task AssignVehicleAsync()
        {
            if (SelectedRoute == null || SelectedBus == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Assigning {SelectedBus.BusNumber} to {SelectedRoute.RouteName}...";

                var result = await _routeService.AssignVehicleToRouteAsync(SelectedRoute.RouteId, SelectedBus.BusId, SelectedTimeSlot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to assign vehicle: {result.Error}";
                    MessageBox.Show(result.Error!, "Vehicle Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update route properties based on time slot
                switch (SelectedTimeSlot)
                {
                    case BusBuddy.Core.Models.RouteTimeSlot.AM:
                        SelectedRoute.AMVehicleId = SelectedBus.BusId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.PM:
                        SelectedRoute.PMVehicleId = SelectedBus.BusId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.Both:
                        SelectedRoute.AMVehicleId = SelectedBus.BusId;
                        SelectedRoute.PMVehicleId = SelectedBus.BusId;
                        break;
                }

                StatusMessage = $"Successfully assigned {SelectedBus.BusNumber} to {SelectedRoute.RouteName} for {SelectedTimeSlot}";
                Logger.Information("Vehicle {BusNumber} assigned to route {RouteName} for {TimeSlot}",
                    SelectedBus.BusNumber, SelectedRoute.RouteName, SelectedTimeSlot);

                SelectedBus = null;
                OnPropertyChanged(nameof(SelectedRouteBusDisplay));
                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign vehicle to route");
                StatusMessage = $"Failed to assign vehicle: {ex.Message}";
                MessageBox.Show($"Failed to assign vehicle: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AssignDriverAsync()
        {
            if (SelectedRoute == null || SelectedDriver == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Assigning {SelectedDriver.DriverName} to {SelectedRoute.RouteName}...";

                var result = await _routeService.AssignDriverToRouteAsync(SelectedRoute.RouteId, SelectedDriver.DriverId, SelectedTimeSlot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to assign driver: {result.Error}";
                    MessageBox.Show(result.Error!, "Driver Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update route properties based on time slot
                switch (SelectedTimeSlot)
                {
                    case BusBuddy.Core.Models.RouteTimeSlot.AM:
                        SelectedRoute.AMDriverId = SelectedDriver.DriverId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.PM:
                        SelectedRoute.PMDriverId = SelectedDriver.DriverId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.Both:
                        SelectedRoute.AMDriverId = SelectedDriver.DriverId;
                        SelectedRoute.PMDriverId = SelectedDriver.DriverId;
                        break;
                }

                StatusMessage = $"Successfully assigned {SelectedDriver.DriverName} to {SelectedRoute.RouteName} for {SelectedTimeSlot}";
                Logger.Information("Driver {DriverName} assigned to route {RouteName} for {TimeSlot}",
                    SelectedDriver.DriverName, SelectedRoute.RouteName, SelectedTimeSlot);

                SelectedDriver = null;
                OnPropertyChanged(nameof(SelectedRouteDriverDisplay));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign driver to route");
                StatusMessage = $"Failed to assign driver: {ex.Message}";
                MessageBox.Show($"Failed to assign driver: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Route Stop Management Commands
        private async Task AddStopAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                var dialog = new RouteStopEditDialog($"Stop {RouteStops.Count + 1}", string.Empty)
                {
                    Owner = Application.Current?.MainWindow
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var stopName = dialog.StopName;
                var newStop = new RouteStop
                {
                    RouteId = SelectedRoute.RouteId,
                    StopName = stopName,
                    StopOrder = RouteStops.Count + 1,
                    StopAddress = string.IsNullOrWhiteSpace(dialog.StopAddress) ? stopName : dialog.StopAddress,
                    Latitude = dialog.Latitude,
                    Longitude = dialog.Longitude
                };

                IsLoading = true;
                StatusMessage = $"Adding stop '{stopName}' to {SelectedRoute.RouteName}...";

                var result = await _routeService.AddStopToRouteAsync(SelectedRoute.RouteId, newStop);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to add stop: {result.Error}";
                    MessageBox.Show(result.Error!, "Add Stop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Add(newStop);
                OnPropertyChanged(nameof(RouteStopCount));
                StatusMessage = $"Successfully added stop '{stopName}' to {SelectedRoute.RouteName}";
                Logger.Information("Added stop {StopName} to route {RouteName}", stopName, SelectedRoute.RouteName);
                // Schedule auto-retime
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to add stop to route");
                StatusMessage = $"Failed to add stop: {ex.Message}";
                MessageBox.Show($"Failed to add stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveStopAsync()
        {
            if (SelectedRouteStop == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Removing stop '{SelectedRouteStop.StopName}'...";

                var result = await _routeService.RemoveStopFromRouteAsync(SelectedRoute!.RouteId, SelectedRouteStop.RouteStopId);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to remove stop: {result.Error}";
                    MessageBox.Show(result.Error!, "Remove Stop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Remove(SelectedRouteStop);
                OnPropertyChanged(nameof(RouteStopCount));
                StatusMessage = $"Successfully removed stop '{SelectedRouteStop.StopName}'";
                Logger.Information("Removed stop {StopName} from route {RouteName}", SelectedRouteStop.StopName, SelectedRoute!.RouteName);

                SelectedRouteStop = null;
                // Schedule auto-retime
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to remove stop from route");
                StatusMessage = $"Failed to remove stop: {ex.Message}";
                MessageBox.Show($"Failed to remove stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                RefreshCommandStates();
            }
        }

        private async Task MoveStopUpAsync()
        {
            if (SelectedRouteStop == null || IsLoading)
            {
                return;
            }

            var currentIndex = RouteStops.IndexOf(SelectedRouteStop);
            if (currentIndex <= 0)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Reordering route stops...";

                var stops = RouteStops.ToList();
                stops.RemoveAt(currentIndex);
                stops.Insert(currentIndex - 1, SelectedRouteStop);

                var orderedStopIds = stops.Select(s => s.RouteStopId).ToList();
                var result = await _routeService.ReorderRouteStopsAsync(SelectedRoute!.RouteId, orderedStopIds);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to reorder stops: {result.Error}";
                    MessageBox.Show(result.Error!, "Reorder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Move(currentIndex, currentIndex - 1);
                StatusMessage = $"Successfully moved stop '{SelectedRouteStop.StopName}' up";
                Logger.Information("Moved stop {StopName} up in route {RouteName}", SelectedRouteStop.StopName, SelectedRoute!.RouteName);
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to move stop up");
                StatusMessage = $"Failed to move stop: {ex.Message}";
                MessageBox.Show($"Failed to move stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                RefreshCommandStates();
            }
        }

        private async Task MoveStopDownAsync()
        {
            if (SelectedRouteStop == null || IsLoading)
            {
                return;
            }

            var currentIndex = RouteStops.IndexOf(SelectedRouteStop);
            if (currentIndex >= RouteStops.Count - 1)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Reordering route stops...";

                var stops = RouteStops.ToList();
                stops.RemoveAt(currentIndex);
                stops.Insert(currentIndex + 1, SelectedRouteStop);

                var orderedStopIds = stops.Select(s => s.RouteStopId).ToList();
                var result = await _routeService.ReorderRouteStopsAsync(SelectedRoute!.RouteId, orderedStopIds);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to reorder stops: {result.Error}";
                    MessageBox.Show(result.Error!, "Reorder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Move(currentIndex, currentIndex + 1);
                StatusMessage = $"Successfully moved stop '{SelectedRouteStop.StopName}' down";
                Logger.Information("Moved stop {StopName} down in route {RouteName}", SelectedRouteStop.StopName, SelectedRoute!.RouteName);
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to move stop down");
                StatusMessage = $"Failed to move stop: {ex.Message}";
                MessageBox.Show($"Failed to move stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Basic sequential timing of route stops based on a user-provided StartTimeString.
        /// Each stop gets arrival = current time cursor, departure = arrival + StopDuration minutes (default 2 if 0).
        /// Persisted via IRouteService.UpdateRouteStopsTimingAsync when available.
        /// </summary>
        private async Task TimeRouteStopsAsync()
        {
            if (SelectedRoute == null || !RouteStops.Any())
            {
                return;
            }

            if (!IsStartTimeValid)
            {
                StatusMessage = "Cannot time stops — invalid Start Time (HH:mm)";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Calculating stop times...";

                // Parse start time; fallback to 07:30 if invalid
                if (!TimeSpan.TryParseExact(
                        _startTimeString.Trim(),
                        new[] { @"hh\:mm", @"h\:mm" },
                        CultureInfo.InvariantCulture,
                        out var startOfRun))
                {
                    startOfRun = new TimeSpan(7, 30, 0); // 07:30 fallback
                    _startTimeString = "07:30"; // normalize
                    OnPropertyChanged(nameof(StartTimeString));
                }

                // A stop time is a face time — 07:00 means seven in the morning at the stop — so the
                // clock is written as-is on a UTC-labelled calendar day, matching Route.Date and the
                // timestamptz Kind converters. No time-zone shift here.
                var runDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var stampedUtc = DateTime.UtcNow;
                var cursor = startOfRun;

                // Order stops by StopOrder to ensure consistency
                foreach (var stop in RouteStops.OrderBy(s => s.StopOrder))
                {
                    var dwell = TimeSpan.FromMinutes(stop.StopDuration > 0 ? stop.StopDuration : 2); // default dwell
                    // ScheduledArrival is the published time PdfReportService prefers, so it has to move
                    // with the grid or the printed route keeps a stale generated time.
                    stop.ScheduledArrival = cursor;
                    stop.ScheduledDeparture = cursor + dwell;
                    stop.EstimatedArrivalTime = runDate + stop.ScheduledArrival;
                    stop.EstimatedDepartureTime = runDate + stop.ScheduledDeparture;
                    stop.UpdatedDate = stampedUtc;
                    cursor = stop.ScheduledDeparture; // advance cursor
                }

                var persistResult = await _routeService.UpdateRouteStopsTimingAsync(SelectedRoute.RouteId, RouteStops);
                if (!persistResult.IsSuccess)
                {
                    StatusMessage = $"Timing calculated but failed to persist: {persistResult.Error}";
                    MessageBox.Show(persistResult.Error ?? "Failed to persist timing", "Timing Persistence", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = $"Timing updated for {RouteStops.Count} stops (Start {StartTimeString})";
                }

                // Notify grid
                foreach (var prop in new[] { nameof(RouteStops) })
                {
                    OnPropertyChanged(prop);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to time route stops");
                StatusMessage = $"Error timing stops: {ex.Message}";
                MessageBox.Show($"Failed to time stops: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // Route Activation Commands
        private async Task ActivateRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            // Activation guard – ensure minimal required components
            var missing = new List<string>();
            if (!AssignedStudentsForSelectedRoute.Any()) missing.Add("at least one student");
            if (!RouteStops.Any()) missing.Add("at least one stop");
            var hasVehicle = SelectedRoute.AMVehicleId.HasValue || SelectedRoute.PMVehicleId.HasValue;
            if (!hasVehicle) missing.Add("vehicle");
            var hasDriver = SelectedRoute.AMDriverId.HasValue || SelectedRoute.PMDriverId.HasValue;
            if (!hasDriver) missing.Add("driver");
            if (missing.Any())
            {
                var msg = "Cannot activate route – missing: " + string.Join(", ", missing);
                StatusMessage = msg;
                MessageBox.Show(msg, "Activation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Activating route '{SelectedRoute.RouteName}'...";

                // Perform full service validation first; surface issues and abort if invalid
                var validation = await _routeService.ValidateRouteForActivationAsync(SelectedRoute.RouteId);
                if (!validation.IsSuccess)
                {
                    StatusMessage = $"Validation error: {validation.Error}";
                    MessageBox.Show(validation.Error ?? "Unknown validation error", "Activation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (validation.Value != null && !validation.Value.IsValid)
                {
                    var issues = string.Join("\n", validation.Value.Issues);
                    var msg = $"Route failed validation:\n{issues}";
                    StatusMessage = "Activation blocked by validation";
                    MessageBox.Show(msg, "Activation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = await _routeService.ActivateRouteAsync(SelectedRoute.RouteId);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to activate route: {result.Error}";
                    MessageBox.Show(result.Error!, "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedRoute.IsActive = true;
                OnPropertyChanged(nameof(CanActivateRoute));
                OnPropertyChanged(nameof(CanDeactivateRoute));
                StatusMessage = $"Successfully activated route '{SelectedRoute.RouteName}'";
                Logger.Information("Activated route {RouteName}", SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to activate route");
                StatusMessage = $"Failed to activate route: {ex.Message}";
                MessageBox.Show($"Failed to activate route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeactivateRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Deactivating route '{SelectedRoute.RouteName}'...";

                var result = await _routeService.DeactivateRouteAsync(SelectedRoute.RouteId);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to deactivate route: {result.Error}";
                    MessageBox.Show(result.Error!, "Deactivation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedRoute.IsActive = false;
                OnPropertyChanged(nameof(CanActivateRoute));
                OnPropertyChanged(nameof(CanDeactivateRoute));
                StatusMessage = $"Successfully deactivated route '{SelectedRoute.RouteName}'";
                Logger.Information("Deactivated route {RouteName}", SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to deactivate route");
                StatusMessage = $"Failed to deactivate route: {ex.Message}";
                MessageBox.Show($"Failed to deactivate route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Enhanced existing commands
        private async Task SaveRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Saving route '{SelectedRoute.RouteName}'...";

                var result = await _routeService.UpdateRouteAsync(SelectedRoute);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to save route: {result.Error}";
                    MessageBox.Show(result.Error!, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (RouteStops.Any())
                {
                    var timingResult = await _routeService.UpdateRouteStopsTimingAsync(SelectedRoute.RouteId, RouteStops);
                    if (!timingResult.IsSuccess)
                    {
                        Logger.Warning("Route stop timing persistence failed: {Error}", timingResult.Error);
                    }
                }

                StatusMessage = $"Successfully saved route '{SelectedRoute.RouteName}'";
                Logger.Information("Saved route {RouteName}", SelectedRoute.RouteName);
                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save route");
                StatusMessage = $"Failed to save route: {ex.Message}";
                MessageBox.Show($"Failed to save route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete or retire route '{SelectedRoute.RouteName}'?\n\nEmpty routes are removed. Routes still referenced by schedules or student keys are retired.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Deleting route '{SelectedRoute.RouteName}'...";

                var deleteResult = await _routeService.DeleteRouteAsync(SelectedRoute.RouteId);
                if (!deleteResult.IsSuccess)
                {
                    StatusMessage = $"Failed to delete route: {deleteResult.Error}";
                    MessageBox.Show(deleteResult.Error!, "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var routeName = SelectedRoute.RouteName;
                if (!string.IsNullOrWhiteSpace(deleteResult.Error))
                {
                    SelectedRoute.IsActive = false;
                    StatusMessage = deleteResult.Error;
                    Logger.Information("Retired route {RouteName}", routeName);
                    return;
                }

                AvailableRoutes.Remove(SelectedRoute);
                SelectedRoute = AvailableRoutes.FirstOrDefault();

                StatusMessage = $"Successfully deleted route '{routeName}'";
                Logger.Information("Deleted route {RouteName}", routeName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete route");
                StatusMessage = $"Failed to delete route: {ex.Message}";
                MessageBox.Show($"Failed to delete route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing data...";

                await LoadDataFromServiceAsync();
                Logger.Information("Data refreshed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh data");
                StatusMessage = $"Failed to refresh data: {ex.Message}";
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Helper method to load route stops
        private async Task LoadRouteStopsAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            try
            {
                RouteStops.Clear();
                AssignedStudentsForSelectedRoute.Clear();

                var result = await _routeService.GetRouteStopsAsync(SelectedRoute.RouteId);
                if (result.IsSuccess)
                {
                    foreach (var stop in result.Value!)
                    {
                        RouteStops.Add(stop);
                    }
                }
                else
                {
                    StatusMessage = $"Could not load stops for {GetRouteDisplayName(SelectedRoute)}: {result.Error}";
                    Logger.Error("Failed to load route stops for route {RouteName}: {Error}", SelectedRoute.RouteName, result.Error);
                }

                await ReloadStudentListsForRouteAsync();

                OnPropertyChanged(nameof(RouteStopCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                Logger.Information("Loaded {StopCount} stops for route {RouteName}", RouteStops.Count, SelectedRoute.RouteName);
                if (result.IsSuccess && RouteStops.Count == 0)
                {
                    StatusMessage = $"No stops found for {GetRouteDisplayName(SelectedRoute)} — add stops to begin routing.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load route stops for route {RouteName}", SelectedRoute.RouteName);
                StatusMessage = $"Could not load stops for {GetRouteDisplayName(SelectedRoute)}: {ex.Message}";
            }
        }

        private async Task ViewScheduleAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            try
            {
                var stopsResult = await _routeService.GetRouteStopsAsync(SelectedRoute.RouteId);
                if (!stopsResult.IsSuccess)
                {
                    StatusMessage = stopsResult.Error ?? "Could not load published stops.";
                    return;
                }

                var appointments = DriverScheduleViewModel.FromPublishedStops(
                    SelectedRoute,
                    stopsResult.Value ?? []);
                var status = appointments.Count == 0
                    ? $"No published stops on {SelectedRoute.RouteName} — add stops before viewing the schedule."
                    : $"Published times for {SelectedRoute.RouteName} ({appointments.Count} stops)";

                new Window
                {
                    Title = $"Schedule — {SelectedRoute.RouteName}",
                    Content = new DriverScheduleView(new DriverScheduleViewModel(appointments, status)),
                    Width = 1200,
                    Height = 800,
                    Owner = Application.Current?.MainWindow
                }.Show();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open published schedule for route {RouteName}", SelectedRoute.RouteName);
                StatusMessage = $"Could not open schedule: {ex.Message}";
            }
        }

        private void GenerateReport()
        {
            ExportRouteAssignmentPdfAsync(includeMap: false);
        }

        #endregion

        #region Data Loading

        private async Task LoadDataFromServiceAsync()
        {
            try
            {
                UnassignedStudents.Clear();
                AvailableRoutes.Clear();
                AvailableBuses.Clear();
                AvailableDrivers.Clear();

                IsLoading = true;

                var studentsTask = _routeService.GetUnassignedStudentsAsync(NormalizeTimeSlot(SelectedTimeSlot));
                var routesTask = _routeService.GetAllRoutesAsync();
                var busesTask = _routeService.GetAvailableBusesAsync();
                var driversTask = _routeService.GetAvailableDriversAsync();

                await Task.WhenAll(studentsTask, routesTask, busesTask, driversTask);

                var loadErrors = new List<string>();

                _allUnassignedStudents.Clear();
                if (studentsTask.Result.IsSuccess && studentsTask.Result.Value != null)
                {
                    _allUnassignedStudents.AddRange(studentsTask.Result.Value);
                }
                else
                {
                    loadErrors.Add($"unassigned students ({studentsTask.Result.Error})");
                }
                FilterStudents();

                if (routesTask.Result.IsSuccess && routesTask.Result.Value != null)
                {
                    foreach (var r in routesTask.Result.Value)
                    {
                        AvailableRoutes.Add(r);
                    }
                }
                else
                {
                    loadErrors.Add($"routes ({routesTask.Result.Error})");
                }

                if (busesTask.Result.IsSuccess && busesTask.Result.Value != null)
                {
                    foreach (var b in busesTask.Result.Value)
                    {
                        AvailableBuses.Add(b);
                    }
                }
                else
                {
                    loadErrors.Add($"buses ({busesTask.Result.Error})");
                }

                if (driversTask.Result.IsSuccess && driversTask.Result.Value != null)
                {
                    foreach (var d in driversTask.Result.Value)
                    {
                        AvailableDrivers.Add(d);
                    }
                }
                else
                {
                    loadErrors.Add($"drivers ({driversTask.Result.Error})");
                }

                if (_preselectedRouteId.HasValue
                    && AvailableRoutes.All(r => r.RouteId != _preselectedRouteId.Value))
                {
                    var preselected = await _routeService.GetRouteByIdAsync(_preselectedRouteId.Value);
                    if (preselected.IsSuccess && preselected.Value != null)
                    {
                        AvailableRoutes.Insert(0, preselected.Value);
                    }
                }

                if (AvailableRoutes.Any())
                {
                    if (_preselectedRouteId.HasValue)
                    {
                        SelectedRoute = AvailableRoutes.FirstOrDefault(r => r.RouteId == _preselectedRouteId.Value)
                            ?? AvailableRoutes.First();
                    }
                    else
                    {
                        SelectedRoute = AvailableRoutes.First();
                    }
                }

                OnPropertyChanged(nameof(UnassignedStudentCount));
                UpdateStatusMessage();

                if (loadErrors.Count > 0)
                {
                    Logger.Error("Route assignment data partially unavailable: {Failures}", string.Join("; ", loadErrors));
                    StatusMessage = "Could not load " + string.Join("; ", loadErrors);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading route assignment data from service");
                SelectedRoute = null;
                UnassignedStudents.Clear();
                _allUnassignedStudents.Clear();
                AvailableRoutes.Clear();
                AvailableBuses.Clear();
                AvailableDrivers.Clear();
                RouteStops.Clear();
                AssignedStudentsForSelectedRoute.Clear();
                OnPropertyChanged(nameof(UnassignedStudentCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                OnPropertyChanged(nameof(RouteStopCount));
                StatusMessage = $"Could not load route data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReloadStudentListsForRouteAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            var slot = NormalizeTimeSlot(SelectedTimeSlot);

            var loadErrors = new List<string>();

            var assignedResult = await _routeService.GetStudentsForRouteAsync(SelectedRoute.RouteId, slot);
            AssignedStudentsForSelectedRoute.Clear();
            if (assignedResult.IsSuccess && assignedResult.Value != null)
            {
                foreach (var s in assignedResult.Value)
                {
                    AssignedStudentsForSelectedRoute.Add(s);
                }
            }
            else
            {
                loadErrors.Add($"assigned students ({assignedResult.Error})");
            }

            var unassignedResult = await _routeService.GetUnassignedStudentsAsync(slot);
            _allUnassignedStudents.Clear();
            if (unassignedResult.IsSuccess && unassignedResult.Value != null)
            {
                _allUnassignedStudents.AddRange(unassignedResult.Value);
            }
            else
            {
                loadErrors.Add($"unassigned students ({unassignedResult.Error})");
            }
            FilterStudents();

            SelectedRoute.StudentCount = AssignedStudentsForSelectedRoute.Count;

            OnPropertyChanged(nameof(AssignedStudentCount));
            OnPropertyChanged(nameof(UnassignedStudentCount));

            if (loadErrors.Count > 0)
            {
                Logger.Error("Route {RouteId} ({Slot}) roster load failed: {Failures}",
                    SelectedRoute.RouteId, slot, string.Join("; ", loadErrors));
                StatusMessage = "Could not load " + string.Join("; ", loadErrors);
            }
            else
            {
                UpdateStatusMessage();
            }
        }

        private void FilterStudents()
        {
            UnassignedStudents.Clear();
            var term = StudentSearchText?.Trim() ?? string.Empty;
            IEnumerable<BusBuddy.Core.Models.Student> source = _allUnassignedStudents;

            if (!string.IsNullOrEmpty(term))
            {
                source = source.Where(s =>
                    (s.StudentName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Grade?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.HomeAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.City?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            foreach (var s in source)
            {
                UnassignedStudents.Add(s);
            }

            OnPropertyChanged(nameof(UnassignedStudentCount));
        }

        private void UpdateStatusMessage()
        {
            if (SelectedRoute != null)
            {
                StatusMessage = $"Route: {SelectedRoute.RouteName} | " +
                               $"Students: {AssignedStudentCount} | " +
                               $"Unassigned: {UnassignedStudentCount}";
            }
            else
            {
                StatusMessage = $"No route selected | Unassigned students: {UnassignedStudentCount}";
            }
        }

        /// <summary>
        /// Basic plotting of the selected route's currently assigned students onto the shared map (MapViewModel).
        /// Reuses existing MapViewModel marker infrastructure; only plots students with coordinates or successfully geocoded addresses.
        /// </summary>
        private async Task PlotRouteOnMapAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }
            try
            {
                StatusMessage = $"Plotting {AssignedStudentCount} students for {SelectedRoute.RouteName}...";
                Logger.Information("PlotRouteOnMap invoked for RouteId={RouteId} Name={RouteName}", SelectedRoute.RouteId, SelectedRoute.RouteName);

                var mapVm = _map;
                if (mapVm == null)
                {
                    StatusMessage = "Map VM not registered";
                    return;
                }

                var mapsGeo = App.ServiceProvider?.GetService<IMapsGeoService>();

                // Schools, catalog stops and the depot are always-on district layers (specs/maps.md
                // "Default: district overlay of schools + catalog stops; selecting a route adds homes
                // on that run and the path"), so only the per-household pins from the previous route
                // are cleared here.
                for (int i = mapVm.MapMarkers.Count - 1; i >= 0; i--)
                {
                    if (MapMarkerLabels.IsPerHousehold(mapVm.MapMarkers[i].Kind))
                    {
                        mapVm.MapMarkers.RemoveAt(i);
                    }
                }

                // The path and the numbered stop pins are the map's own pipeline; pushing the selection
                // draws them instead of leaving this view with homes and no route line.
                var mapRoute = mapVm.Routes.FirstOrDefault(r => r.RouteId == SelectedRoute.RouteId);
                if (mapRoute != null)
                {
                    mapVm.SelectedRoute = mapRoute;
                }

                var students = AssignedStudentsForSelectedRoute.ToList();
                if (students.Count == 0)
                {
                    StatusMessage = "No students assigned to plot";
                    return;
                }

                // Fire-and-forget background geocode/plot to keep UI responsive
                _ = Task.Run(async () =>
                {
                    foreach (var s in students)
                    {
                        try
                        {
                            double? lat = null, lon = null;

                            // Prefer existing stored coordinates
                            if (s.HasValidatedHomeCoordinates)
                            {
                                lat = (double)s.Latitude!.Value;
                                lon = (double)s.Longitude!.Value;
                            }
                            else if (mapsGeo is not null && mapsGeo.IsConfigured && !string.IsNullOrWhiteSpace(s.HomeAddress))
                            {
                                var r = await mapsGeo.ValidateAndGeocodeAsync(s.HomeAddress, s.City, s.State, s.Zip);
                                if (r.Ok && LocationCoordinate.IsValidated(r.Latitude, r.Longitude))
                                {
                                    lat = r.Latitude!.Value;
                                    lon = r.Longitude!.Value;
                                }
                            }

                            if (lat == null || lon == null)
                            {
                                continue; // skip if no coordinates
                            }

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                mapVm.PlotStop(
                                    lat.Value,
                                    lon.Value,
                                    new[] { s.StudentName ?? "Student" },
                                    s.StudentName,
                                    studentIds: new[] { s.StudentId });
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Failed to plot student {StudentId} {StudentName}", s.StudentId, s.StudentName);
                        }
                    }
                    StatusMessage = $"Plotted {SelectedRoute.RouteName} students";
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error plotting route students on map");
                StatusMessage = "Error plotting students";
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
        // Duplicate IDisposable region removed (primary implementation with logging retained earlier in file)
    }
}
