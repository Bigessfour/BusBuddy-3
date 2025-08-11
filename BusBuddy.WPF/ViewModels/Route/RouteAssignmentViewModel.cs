using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Commands;
using Serilog;
using Microsoft.Extensions.DependencyInjection; // For resolving GoogleEarthViewModel / services
using BusBuddy.WPF.ViewModels.GoogleEarth; // Map markers
using BusBuddy.Core.Services.Interfaces; // IGeocodingService
using System.Globalization;
using System.IO; // For PDF export file writing
using System.Threading; // For debounce timer


namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Enhanced ViewModel for Route Assignment and Route Building
    /// Implements comprehensive route building workflow with MVVM compliance
    /// Supports Syncfusion SfDataGrid integration and Result pattern error handling
    /// </summary>
    public class RouteAssignmentViewModel : INotifyPropertyChanged, IDisposable
    {
        // Backing fields for all properties (restored for CS0103 fix)
        private ObservableCollection<BusBuddy.Core.Models.Route> _availableRoutes = new();
        private ObservableCollection<BusBuddy.Core.Models.Bus> _availableBuses = new();
        private ObservableCollection<BusBuddy.Core.Models.Driver> _availableDrivers = new();
        private ObservableCollection<RouteStop> _routeStops = new();
        private ObservableCollection<BusBuddy.Core.Models.Student> _assignedStudentsForSelectedRoute = new();
        private ObservableCollection<BusBuddy.Core.Models.Student> _unassignedStudents = new();
        private BusBuddy.Core.Models.Student? _selectedStudent;
        private BusBuddy.Core.Models.Student? _selectedAssignedStudent;
        private BusBuddy.Core.Models.Route? _selectedRoute;
        private BusBuddy.Core.Models.Bus? _selectedBus;
        private BusBuddy.Core.Models.Driver? _selectedDriver;
        private RouteStop? _selectedRouteStop;
        private string _newRouteName = string.Empty;
        private DateTime _newRouteDate = DateTime.Today;
        private string _newRouteDescription = string.Empty;
        private BusBuddy.Core.Models.RouteTimeSlot _selectedTimeSlot = BusBuddy.Core.Models.RouteTimeSlot.AM;
        private bool _isRouteBeingBuilt;
        private bool _isRouteActive;
        private bool _isLoading;
        private string _studentSearchText = string.Empty;
        private string _statusMessage = string.Empty;
        private int? _preselectedRouteId;
        private string _startTimeString = "07:30";
        private readonly IRouteService? _routeService;
        private static readonly ILogger Logger = Log.ForContext<RouteAssignmentViewModel>();
    private Timer? _retimeDebounceTimer; // Debounce timer for auto-retiming after structural stop changes
    private const int RetimeDebounceMs = 600; // Delay before auto timing after modifications

        // Constructors added (MVP restoration)
        // 1) Parameterless for XAML designer / fallback
        // 2) routeService injection (primary)
        // 3) routeService + preselected route (used by RouteAssignmentView overload)
        public RouteAssignmentViewModel()
        {
            try
            {
                // Try resolve IRouteService from DI if available
                _routeService = App.ServiceProvider?.GetService<IRouteService>();
            }
            catch { }
            Initialize();
        }

        public RouteAssignmentViewModel(IRouteService? routeService)
        {
            _routeService = routeService;
            Initialize();
        }

        public RouteAssignmentViewModel(IRouteService? routeService, BusBuddy.Core.Models.Route preselectedRoute)
        {
            _routeService = routeService;
            _preselectedRouteId = preselectedRoute?.RouteId;
            Initialize();
            // If the route collection already loaded synchronously (mock), select it
            if (preselectedRoute != null)
            {
                SelectedRoute = preselectedRoute;
            }
        }

        private void Initialize()
        {
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
                            TimeRouteStops();
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
            set => SetProperty(ref _startTimeString, value);
        }

        // Students currently assigned to the SelectedRoute (MVP-local collection)
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
                    OnPropertyChanged(nameof(CanAssignStudent));
                    OnPropertyChanged(nameof(CanRemoveStudent));
                    OnPropertyChanged(nameof(AssignedStudentCount));
                    OnPropertyChanged(nameof(SelectedRouteBusDisplay));
                    OnPropertyChanged(nameof(SelectedRouteDriverDisplay));
                    OnPropertyChanged(nameof(CanActivateRoute));
                    OnPropertyChanged(nameof(CanDeactivateRoute));
                    OnPropertyChanged(nameof(IsRouteSelected));
                    _ = LoadRouteStopsAsync(); // Load stops asynchronously
                    UpdateStatusMessage();
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

        // Route Building Properties
        public string NewRouteName
        {
            get => _newRouteName;
            set
            {
                if (SetProperty(ref _newRouteName, value))
                {
                    OnPropertyChanged(nameof(CanCreateRoute));
                }
            }
        }

        public DateTime NewRouteDate
        {
            get => _newRouteDate;
            set
            {
                if (SetProperty(ref _newRouteDate, value))
                {
                    OnPropertyChanged(nameof(CanCreateRoute));
                }
            }
        }

        public string NewRouteDescription
        {
            get => _newRouteDescription;
            set => SetProperty(ref _newRouteDescription, value);
        }

        public BusBuddy.Core.Models.RouteTimeSlot SelectedTimeSlot
        {
            get => _selectedTimeSlot;
            set => SetProperty(ref _selectedTimeSlot, value);
        }

        public bool IsRouteBeingBuilt
        {
            get => _isRouteBeingBuilt;
            set
            {
                if (SetProperty(ref _isRouteBeingBuilt, value))
                {
                    OnPropertyChanged(nameof(CanCreateRoute));
                    OnPropertyChanged(nameof(CanCancelRouteBuilding));
                }
            }
        }

        public bool IsRouteActive
        {
            get => _isRouteActive;
            set => SetProperty(ref _isRouteActive, value);
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
                var bus = id.HasValue ? AvailableBuses.FirstOrDefault(b => b.VehicleId == id.Value) : null;
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

        // Computed Properties
    public int UnassignedStudentCount => UnassignedStudents?.Count ?? 0;
    public int AssignedStudentCount => AssignedStudentsForSelectedRoute?.Count ?? 0;
        public int RouteStopCount => RouteStops?.Count ?? 0;
        public bool IsRouteSelected => SelectedRoute != null;

        // Command Availability Properties
        public bool CanAssignStudent => SelectedStudent != null && SelectedRoute != null && !IsLoading;
        public bool CanRemoveStudent => SelectedAssignedStudent != null && SelectedRoute != null && !IsLoading;
        public bool CanCreateRoute => !string.IsNullOrWhiteSpace(NewRouteName) && !IsRouteBeingBuilt && !IsLoading;
        public bool CanSaveRoute => SelectedRoute != null && !IsLoading;
        public bool CanActivateRoute => SelectedRoute != null && !SelectedRoute.IsActive && !IsLoading;
        public bool CanDeactivateRoute => SelectedRoute != null && SelectedRoute.IsActive && !IsLoading;
        public bool CanAssignVehicle => SelectedRoute != null && SelectedBus != null && !IsLoading;
        public bool CanAssignDriver => SelectedRoute != null && SelectedDriver != null && !IsLoading;
        public bool CanAddStop => SelectedRoute != null && !IsLoading;
        public bool CanRemoveStop => SelectedRouteStop != null && !IsLoading;
        public bool CanMoveStopUp => SelectedRouteStop != null && RouteStops.IndexOf(SelectedRouteStop) > 0 && !IsLoading;
        public bool CanMoveStopDown => SelectedRouteStop != null && RouteStops.IndexOf(SelectedRouteStop) < RouteStops.Count - 1 && !IsLoading;
        public bool CanCancelRouteBuilding => IsRouteBeingBuilt && !IsLoading;
        public bool CanValidateRoute => SelectedRoute != null && !IsLoading;

        // Available TimeSlots for ComboBox binding
        public Array TimeSlots => Enum.GetValues<BusBuddy.Core.Models.RouteTimeSlot>();



        #region Commands

        // Existing Commands
        public ICommand AssignStudentCommand { get; private set; } = null!;
        public ICommand RemoveStudentCommand { get; private set; } = null!;
        public ICommand AutoAssignCommand { get; private set; } = null!;
        public ICommand CreateRouteCommand { get; private set; } = null!;
        public ICommand SaveRouteCommand { get; private set; } = null!;
        public ICommand DeleteRouteCommand { get; private set; } = null!;
        public ICommand ViewScheduleCommand { get; private set; } = null!;
        public ICommand RefreshDataCommand { get; private set; } = null!;
        public ICommand GenerateReportCommand { get; private set; } = null!;

        // Enhanced Route Building Commands
        public ICommand StartRouteBuildingCommand { get; private set; } = null!;
        public ICommand CancelRouteBuildingCommand { get; private set; } = null!;
        public ICommand AssignVehicleCommand { get; private set; } = null!;
        public ICommand AssignDriverCommand { get; private set; } = null!;
        public ICommand AddStopCommand { get; private set; } = null!;
        public ICommand RemoveStopCommand { get; private set; } = null!;
        public ICommand MoveStopUpCommand { get; private set; } = null!;
        public ICommand MoveStopDownCommand { get; private set; } = null!;
        public ICommand ValidateRouteCommand { get; private set; } = null!;
        public ICommand ActivateRouteCommand { get; private set; } = null!;
        public ICommand DeactivateRouteCommand { get; private set; } = null!;
        public ICommand CloneRouteCommand { get; private set; } = null!;
    // Basic mapping (MVP) — plot currently assigned students for selected route

    public ICommand PlotRouteOnMapCommand { get; private set; } = null!;
    public ICommand TimeRouteCommand { get; private set; } = null!; // Basic stop timing
    public ICommand PrintMapCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            // Existing Commands
            AssignStudentCommand = new RelayCommand(async () => await AssignStudentAsync(), () => CanAssignStudent);
            RemoveStudentCommand = new RelayCommand(async () => await RemoveStudentAsync(), () => CanRemoveStudent);
            AutoAssignCommand = new RelayCommand(async () => await AutoAssignStudentsAsync());
            CreateRouteCommand = new RelayCommand(async () => await CreateNewRouteAsync(), () => CanCreateRoute);
            SaveRouteCommand = new RelayCommand(async () => await SaveRouteAsync(), () => CanSaveRoute);
            DeleteRouteCommand = new RelayCommand(async () => await DeleteRouteAsync());
            ViewScheduleCommand = new RelayCommand(ViewSchedule);
            RefreshDataCommand = new RelayCommand(async () => await RefreshDataAsync());
            GenerateReportCommand = new RelayCommand(GenerateReport);

            // Enhanced Route Building Commands
            StartRouteBuildingCommand = new RelayCommand(StartRouteBuilding, () => !IsRouteBeingBuilt);
            CancelRouteBuildingCommand = new RelayCommand(CancelRouteBuilding, () => CanCancelRouteBuilding);
            AssignVehicleCommand = new RelayCommand(async () => await AssignVehicleAsync(), () => CanAssignVehicle);
            AssignDriverCommand = new RelayCommand(async () => await AssignDriverAsync(), () => CanAssignDriver);
            AddStopCommand = new RelayCommand(async () => await AddStopAsync(), () => CanAddStop);
            RemoveStopCommand = new RelayCommand(async () => await RemoveStopAsync(), () => CanRemoveStop);
            MoveStopUpCommand = new RelayCommand(async () => await MoveStopUpAsync(), () => CanMoveStopUp);
            MoveStopDownCommand = new RelayCommand(async () => await MoveStopDownAsync(), () => CanMoveStopDown);
            ValidateRouteCommand = new RelayCommand(async () => await ValidateRouteAsync(), () => CanValidateRoute);
            ActivateRouteCommand = new RelayCommand(async () => await ActivateRouteAsync(), () => CanActivateRoute);
            DeactivateRouteCommand = new RelayCommand(async () => await DeactivateRouteAsync(), () => CanDeactivateRoute);
            CloneRouteCommand = new RelayCommand(async () => await CloneRouteAsync());
            PlotRouteOnMapCommand = new RelayCommand(async () => await PlotRouteOnMapAsync(), () => SelectedRoute != null);
            TimeRouteCommand = new RelayCommand(() => TimeRouteStops(), () => SelectedRoute != null && RouteStops.Any());
        PrintMapCommand = new RelayCommand(PrintMap, () => SelectedRoute != null);
                    // Re-evaluate map/ timing commands
                    (PlotRouteOnMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        private void PrintMap()
        {
            if (SelectedRoute == null)
                return;

            try
            {
                Logger.Information("Starting route PDF export for {RouteName} (Slot {Slot})", SelectedRoute.RouteName, SelectedTimeSlot);
                // Resolve PDF service (core) from DI or instantiate if lightweight
                var pdfService = App.ServiceProvider?.GetService<BusBuddy.Core.Services.PdfReportService>()
                                   ?? new BusBuddy.Core.Services.PdfReportService();

                // Try to obtain a recent map snapshot from GoogleEarthViewModel (optional)
                byte[]? mapPng = null;
                try
                {
                    var mapVm = App.ServiceProvider?.GetService<GoogleEarthViewModel>();
                    if (mapVm != null)
                    {
                        // If no snapshot yet, try to proactively request one via an event/command pattern (future) or fallback to current value
                        if (mapVm.LatestMapSnapshotPng == null || mapVm.LatestMapSnapshotPng.Length == 0)
                        {
                            Logger.Debug("No existing map snapshot; attempting proactive capture (reflection invoke TryCaptureMapSnapshot on GoogleEarthView if available)");
                            TryProactiveMapSnapshotCapture();
                            // Re-check after attempt
                            if (mapVm.LatestMapSnapshotPng == null || mapVm.LatestMapSnapshotPng.Length == 0)
                            {
                                Logger.Debug("Map snapshot still unavailable after proactive attempt");
                            }
                        }
                        mapPng = mapVm.LatestMapSnapshotPng; // may still be null; PDF service handles absence gracefully
                    }
                }
                catch { /* Non-fatal if map VM unavailable */ }

                // Determine assigned bus/driver for current time slot
                BusBuddy.Core.Models.Bus? bus = null;
                BusBuddy.Core.Models.Driver? driver = null;
                if (SelectedRoute != null)
                {
                    if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.AM && SelectedRoute.AMVehicleId.HasValue)
                        bus = AvailableBuses.FirstOrDefault(b => b.VehicleId == SelectedRoute.AMVehicleId.Value);
                    if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM && SelectedRoute.PMVehicleId.HasValue)
                        bus = AvailableBuses.FirstOrDefault(b => b.VehicleId == SelectedRoute.PMVehicleId.Value);
                    if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.AM && SelectedRoute.AMDriverId.HasValue)
                        driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.AMDriverId.Value);
                    if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM && SelectedRoute.PMDriverId.HasValue)
                        driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.PMDriverId.Value);
                }

                var pdfBytes = pdfService.GenerateRouteSummaryReport(
                    SelectedRoute,
                    RouteStops.ToList(),
                    AssignedStudentsForSelectedRoute.ToList(),
                    bus,
                    driver,
                    (BusBuddy.Core.Models.RouteTimeSlot)SelectedTimeSlot,
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
        /// Attempts to proactively capture a map snapshot by locating an existing GoogleEarthView instance in visual trees.
        /// MVP lightweight approach: scans Application.Current.Windows for a GoogleEarthView and invokes its internal snapshot via reflection.
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
                    // Depth-first search visual tree for GoogleEarthView type
                    var target = FindDescendantByTypeName(w, "GoogleEarthView");
                    if (target != null)
                    {
                        var m = target.GetType().GetMethod("TryCaptureMapSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (m != null)
                        {
                            m.Invoke(target, null);
                            Logger.Debug("Invoked TryCaptureMapSnapshot via reflection on GoogleEarthView");
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

        // Simple visual tree walker (recursive) — MVP helper
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
            if (SelectedStudent == null || SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                // Capture references defensively to avoid null after awaits
                var student = SelectedStudent;
                var route = SelectedRoute;
                var studentName = student?.StudentName ?? "(unknown)";
                var routeName = route?.RouteName ?? "(no route)";
                StatusMessage = $"Assigning {studentName} to {routeName}...";

                // Prevent duplicate assignment in memory (MVP guard)
                if (student != null && AssignedStudentsForSelectedRoute.Any(s => s.StudentId == student.StudentId))
                {
                    StatusMessage = $"{student.StudentName} is already on {routeName}";
                    return;
                }

                if (_routeService != null && student != null && route != null)
                {
                    var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to assign student: {result.Error}";
                        MessageBox.Show(result.Error!, "Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Update collections
                if (student != null)
                {
                    UnassignedStudents.Remove(student);
                    AssignedStudentsForSelectedRoute.Add(student);
                }
                // Update route student count live (MVP)
                if (route != null)
                {
                    IncrementRouteStudentCount(route, +1);
                }
                OnPropertyChanged(nameof(UnassignedStudentCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                StatusMessage = $"Successfully assigned {studentName} to {routeName}";

                Logger.Information("Student {StudentName} assigned to route {RouteName}", studentName, routeName);

                SelectedStudent = null; // clear selection so command disables
                (AssignStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign student to route");
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to assign student: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveStudentAsync()
        {
            if (SelectedAssignedStudent == null || SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Removing {SelectedAssignedStudent.StudentName} from {SelectedRoute.RouteName}...";

                if (_routeService != null)
                {
                    var result = await _routeService.RemoveStudentFromRouteAsync(SelectedAssignedStudent.StudentId, SelectedRoute.RouteId);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to remove student: {result.Error}";
                        MessageBox.Show(result.Error!, "Removal Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Update collections
                UnassignedStudents.Add(SelectedAssignedStudent);
                AssignedStudentsForSelectedRoute.Remove(SelectedAssignedStudent);
                IncrementRouteStudentCount(SelectedRoute, -1);
                OnPropertyChanged(nameof(UnassignedStudentCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                StatusMessage = $"Successfully removed {SelectedAssignedStudent.StudentName} from {SelectedRoute.RouteName}";

                Logger.Information("Student {StudentName} removed from route {RouteName}",
                    SelectedAssignedStudent.StudentName, SelectedRoute.RouteName);

                SelectedAssignedStudent = null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to remove student from route");
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to remove student: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Lightweight helper to keep SelectedRoute.StudentCount in sync during MVP without full reload.
        /// </summary>
        private void IncrementRouteStudentCount(BusBuddy.Core.Models.Route route, int delta)
        {
            try
            {
                var current = route.StudentCount ?? 0;
                var updated = current + delta;
                if (updated < 0) updated = 0;
                route.StudentCount = updated;
                OnPropertyChanged(nameof(AssignedStudentCount));
            }
            catch { }
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
                StatusMessage = "Auto-assigning students based on proximity and capacity...";

                // For MVP implementation - replace with actual service call
                await Task.Delay(1000); // Simulate processing

                // TODO: Implement actual auto-assignment logic with service
                // if (_routeService != null)
                // {
                //     var result = await _routeService.AutoAssignStudentsAsync(SelectedRoute.Id, UnassignedStudents.ToList());
                //     if (result.IsSuccess)
                //     {
                //         foreach (var student in result.Value!)
                //         {
                //             UnassignedStudents.Remove(student);
                //         }
                //     }
                // }

                OnPropertyChanged(nameof(UnassignedStudentCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                StatusMessage = "Auto-assignment completed successfully";

                Logger.Information("Auto-assignment completed for route {RouteName}", SelectedRoute.RouteName);
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

        // Enhanced Route Building Commands
        private async Task CreateNewRouteAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRouteName) || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Creating new route '{NewRouteName}'...";

                if (_routeService != null)
                {
                    var result = await _routeService.CreateNewRouteAsync(NewRouteName, NewRouteDate, NewRouteDescription);
                    if (result.IsSuccess)
                    {
                        AvailableRoutes.Add(result.Value!);
                        SelectedRoute = result.Value;
                        IsRouteBeingBuilt = true;

                        // Clear form
                        NewRouteName = string.Empty;
                        NewRouteDescription = string.Empty;
                        NewRouteDate = DateTime.Today;

                        StatusMessage = $"Successfully created route '{result.Value!.RouteName}'. Now configure vehicles, drivers, and stops.";
                        Logger.Information("Created new route {RouteId} - {RouteName}", result.Value.RouteId, result.Value.RouteName);
                    }
                    else
                    {
                        StatusMessage = $"Failed to create route: {result.Error}";
                        MessageBox.Show(result.Error!, "Route Creation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // MVP fallback - create mock route
                    var mockRoute = new BusBuddy.Core.Models.Route
                    {
                        RouteId = AvailableRoutes.Count + 1,
                        RouteName = NewRouteName,
                        Date = NewRouteDate,
                        Description = NewRouteDescription,
                        School = "Default School", // Required property
                        IsActive = false
                    };

                    AvailableRoutes.Add(mockRoute);
                    SelectedRoute = mockRoute;
                    IsRouteBeingBuilt = true;

                    // Clear form
                    NewRouteName = string.Empty;
                    NewRouteDescription = string.Empty;

                    StatusMessage = $"Successfully created route '{mockRoute.RouteName}' (MVP mode)";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create route");
                StatusMessage = $"Failed to create route: {ex.Message}";
                MessageBox.Show($"Failed to create route: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void StartRouteBuilding()
        {
            IsRouteBeingBuilt = true;
            StatusMessage = "Route building mode activated. Create a new route or select an existing one to modify.";
            Logger.Information("Route building mode started");
        }

        private void CancelRouteBuilding()
        {
            IsRouteBeingBuilt = false;
            SelectedRoute = null;
            NewRouteName = string.Empty;
            NewRouteDescription = string.Empty;
            StatusMessage = "Route building cancelled";
            Logger.Information("Route building mode cancelled");
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

                if (_routeService != null)
                {
                    var result = await _routeService.AssignVehicleToRouteAsync(SelectedRoute.RouteId, SelectedBus.VehicleId, SelectedTimeSlot);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to assign vehicle: {result.Error}";
                        MessageBox.Show(result.Error!, "Vehicle Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Update route properties based on time slot
                switch (SelectedTimeSlot)
                {
                    case BusBuddy.Core.Models.RouteTimeSlot.AM:
                        SelectedRoute.AMVehicleId = SelectedBus.VehicleId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.PM:
                        SelectedRoute.PMVehicleId = SelectedBus.VehicleId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.Both:
                        SelectedRoute.AMVehicleId = SelectedBus.VehicleId;
                        SelectedRoute.PMVehicleId = SelectedBus.VehicleId;
                        break;
                }

                StatusMessage = $"Successfully assigned {SelectedBus.BusNumber} to {SelectedRoute.RouteName} for {SelectedTimeSlot}";
                Logger.Information("Vehicle {BusNumber} assigned to route {RouteName} for {TimeSlot}",
                    SelectedBus.BusNumber, SelectedRoute.RouteName, SelectedTimeSlot);

                SelectedBus = null;
                OnPropertyChanged(nameof(SelectedRouteBusDisplay));
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

                if (_routeService != null)
                {
                    var result = await _routeService.AssignDriverToRouteAsync(SelectedRoute.RouteId, SelectedDriver.DriverId, SelectedTimeSlot);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to assign driver: {result.Error}";
                        MessageBox.Show(result.Error!, "Driver Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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
                // TODO: Open dialog to get stop details
                var stopName = $"Stop {RouteStops.Count + 1}";
                var newStop = new RouteStop
                {
                    RouteId = SelectedRoute.RouteId,
                    StopName = stopName,
                    StopOrder = RouteStops.Count + 1,
                    StopAddress = "New Stop Address"
                };

                IsLoading = true;
                StatusMessage = $"Adding stop '{stopName}' to {SelectedRoute.RouteName}...";

                if (_routeService != null)
                {
                    var result = await _routeService.AddStopToRouteAsync(SelectedRoute.RouteId, newStop);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to add stop: {result.Error}";
                        MessageBox.Show(result.Error!, "Add Stop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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

                if (_routeService != null)
                {
                    var result = await _routeService.RemoveStopFromRouteAsync(SelectedRoute!.RouteId, SelectedRouteStop.RouteStopId);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to remove stop: {result.Error}";
                        MessageBox.Show(result.Error!, "Remove Stop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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

                if (_routeService != null)
                {
                    var orderedStopIds = stops.Select(s => s.RouteStopId).ToList();
                    var result = await _routeService.ReorderRouteStopsAsync(SelectedRoute!.RouteId, orderedStopIds);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to reorder stops: {result.Error}";
                        MessageBox.Show(result.Error!, "Reorder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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

                if (_routeService != null)
                {
                    var orderedStopIds = stops.Select(s => s.RouteStopId).ToList();
                    var result = await _routeService.ReorderRouteStopsAsync(SelectedRoute!.RouteId, orderedStopIds);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to reorder stops: {result.Error}";
                        MessageBox.Show(result.Error!, "Reorder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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
        /// For MVP each stop gets arrival = current time cursor, departure = arrival + StopDuration minutes (default 2 if 0).
        /// Persisted via IRouteService.UpdateRouteStopsTimingAsync when available.
        /// </summary>
        private async void TimeRouteStops()
        {
            if (SelectedRoute == null || !RouteStops.Any())
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Calculating stop times...";

                // Parse start time; fallback to 07:30 if invalid
                var baseDate = DateTime.Today;
                var startParseOk = DateTime.TryParseExact(_startTimeString.Trim(), new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime);
                if (!startParseOk)
                {
                    parsedTime = DateTime.Today.AddHours(7).AddMinutes(30); // 07:30 fallback
                    _startTimeString = "07:30"; // normalize
                    OnPropertyChanged(nameof(StartTimeString));
                }
                var current = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, parsedTime.Hour, parsedTime.Minute, 0, DateTimeKind.Local);

                // Order stops by StopOrder to ensure consistency
                foreach (var stop in RouteStops.OrderBy(s => s.StopOrder))
                {
                    stop.EstimatedArrivalTime = current;
                    var dwellMinutes = stop.StopDuration > 0 ? stop.StopDuration : 2; // MVP default dwell
                    stop.EstimatedDepartureTime = current.AddMinutes(dwellMinutes);
                    stop.UpdatedDate = DateTime.Now;
                    current = stop.EstimatedDepartureTime; // advance cursor
                }

                // Persist if service available
                if (_routeService != null)
                {
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
                }
                else
                {
                    StatusMessage = $"Timing (in-memory) updated for {RouteStops.Count} stops (Start {StartTimeString})";
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

        // Route Validation and Activation Commands
        private async Task ValidateRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Validating route '{SelectedRoute.RouteName}'...";

                if (_routeService != null)
                {
                    var result = await _routeService.ValidateRouteForActivationAsync(SelectedRoute.RouteId);
                    if (result.IsSuccess)
                    {
                        var validation = result.Value!;
                        // Using model RouteValidationResult (Issues + Summary) after RTD-01 consolidation
                        var message = validation.IsValid
                            ? "Route validation passed! Ready for activation."
                            : $"Route validation failed:\n\nIssues:\n{string.Join("\n", validation.Issues)}";

                        StatusMessage = validation.IsValid ? "Route validation passed" : "Route validation failed";
                        MessageBox.Show(message, "Route Validation", MessageBoxButton.OK,
                            validation.IsValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
                    }
                    else
                    {
                        StatusMessage = $"Validation failed: {result.Error}";
                        MessageBox.Show(result.Error!, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // MVP validation
                    var isValid = !string.IsNullOrEmpty(SelectedRoute.RouteName) && RouteStops.Any();
                    StatusMessage = isValid ? "Route validation passed (MVP mode)" : "Route validation failed (MVP mode)";
                    MessageBox.Show(isValid ? "Route is valid!" : "Route needs a name and at least one stop.",
                        "Route Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to validate route");
                StatusMessage = $"Validation error: {ex.Message}";
                MessageBox.Show($"Validation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ActivateRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Activating route '{SelectedRoute.RouteName}'...";

                if (_routeService != null)
                {
                    var result = await _routeService.ActivateRouteAsync(SelectedRoute.RouteId);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to activate route: {result.Error}";
                        MessageBox.Show(result.Error!, "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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

                if (_routeService != null)
                {
                    var result = await _routeService.DeactivateRouteAsync(SelectedRoute.RouteId);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to deactivate route: {result.Error}";
                        MessageBox.Show(result.Error!, "Deactivation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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

        private async Task CloneRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Cloning route '{SelectedRoute.RouteName}'...";

                var newDate = DateTime.Today.AddDays(1);
                var newName = $"{SelectedRoute.RouteName} (Copy)";

                if (_routeService != null)
                {
                    var result = await _routeService.CloneRouteAsync(SelectedRoute.RouteId, newDate, newName);
                    if (result.IsSuccess)
                    {
                        AvailableRoutes.Add(result.Value!);
                        SelectedRoute = result.Value;
                        StatusMessage = $"Successfully cloned route as '{newName}'";
                        Logger.Information("Cloned route {OriginalName} to {NewName}", SelectedRoute.RouteName, newName);
                    }
                    else
                    {
                        StatusMessage = $"Failed to clone route: {result.Error}";
                        MessageBox.Show(result.Error!, "Clone Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // MVP fallback
                    var clonedRoute = new BusBuddy.Core.Models.Route
                    {
                        RouteId = AvailableRoutes.Count + 1,
                        RouteName = newName,
                        Date = newDate,
                        Description = SelectedRoute.Description,
                        School = "Default School", // Required property
                        IsActive = false
                    };

                    AvailableRoutes.Add(clonedRoute);
                    SelectedRoute = clonedRoute;
                    StatusMessage = $"Successfully cloned route as '{newName}' (MVP mode)";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clone route");
                StatusMessage = $"Failed to clone route: {ex.Message}";
                MessageBox.Show($"Failed to clone route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (_routeService != null)
                {
                    var result = await _routeService.UpdateRouteAsync(SelectedRoute);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = $"Failed to save route: {result.Error}";
                        MessageBox.Show(result.Error!, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                StatusMessage = $"Successfully saved route '{SelectedRoute.RouteName}'";
                Logger.Information("Saved route {RouteName}", SelectedRoute.RouteName);
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
                $"Are you sure you want to delete route '{SelectedRoute.RouteName}'?\n\nThis will unassign all students and cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Deleting route '{SelectedRoute.RouteName}'...";

                if (_routeService != null)
                {
                    var deleteResult = await _routeService.DeleteRouteAsync(SelectedRoute.RouteId);
                    if (!deleteResult.IsSuccess)
                    {
                        StatusMessage = $"Failed to delete route: {deleteResult.Error}";
                        MessageBox.Show(deleteResult.Error!, "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var routeName = SelectedRoute.RouteName;
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

                if (_routeService != null)
                {
                    await LoadDataFromServiceAsync();
                }
                else
                {
                    // TODO: Replace with actual service calls
                    await Task.Delay(500); // Simulate loading
                    LoadMockData(); // For MVP
                }
                StatusMessage = "Data refreshed successfully";
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

                if (_routeService != null)
                {
                    var result = await _routeService.GetRouteStopsAsync(SelectedRoute.RouteId);
                    if (result.IsSuccess)
                    {
                        foreach (var stop in result.Value!)
                        {
                            RouteStops.Add(stop);
                        }
                    }
                }
                else
                {
                    // MVP fallback - load mock stops
                    for (int i = 1; i <= 3; i++)
                    {
                        RouteStops.Add(new RouteStop
                        {
                            RouteStopId = i,
                            RouteId = SelectedRoute.RouteId,
                            StopName = $"Stop {i}",
                            StopOrder = i,
                            StopAddress = $"{i * 100} Mock Street"
                        });
                    }

                    // MVP: Seed some assigned students visually for the grid
                    var seedCount = Math.Min(10, UnassignedStudents.Count);
                    foreach (var s in UnassignedStudents.Take(seedCount).ToList())
                    {
                        AssignedStudentsForSelectedRoute.Add(s);
                        UnassignedStudents.Remove(s);
                    }
                }

                OnPropertyChanged(nameof(RouteStopCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                Logger.Information("Loaded {StopCount} stops for route {RouteName}", RouteStops.Count, SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load route stops for route {RouteName}", SelectedRoute.RouteName);
            }
        }



        private void CreateNewRoute()
        {
            try
            {
                var routeName = $"Route {AvailableRoutes.Count + 1}";
                var newRoute = new BusBuddy.Core.Models.Route
                {
                    RouteName = routeName,
                    Date = DateTime.Today,
                    IsActive = true,
                    School = "Default School" // Required property
                };

                AvailableRoutes.Add(newRoute);
                SelectedRoute = newRoute;

                StatusMessage = $"Created new route: {routeName}";
                Logger.Information("Created new route {RouteName}", routeName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create new route");
                StatusMessage = $"Error creating route: {ex.Message}";
            }
        }

        private async void SaveRoute()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            try
            {
                // if (_routeService != null)
                // {
                //     await _routeService.SaveRouteAsync(SelectedRoute);
                // }
                StatusMessage = $"Saved route: {SelectedRoute.RouteName}";
                Logger.Information("Saved route {RouteName}", SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save route");
                StatusMessage = $"Error saving route: {ex.Message}";
                MessageBox.Show($"Failed to save route: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteRoute()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete route '{SelectedRoute.RouteName}'?\n\nThis will unassign all students.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // if (_routeService != null)
                    // {
                    //     await _routeService.DeleteRouteAsync(SelectedRoute.RouteId);
                    // }
                    AvailableRoutes.Remove(SelectedRoute);

                    OnPropertyChanged(nameof(UnassignedStudentCount));
                    StatusMessage = $"Deleted route: {SelectedRoute.RouteName}";
                    Logger.Information("Deleted route {RouteName}", SelectedRoute.RouteName);

                    SelectedRoute = AvailableRoutes.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to delete route");
                    StatusMessage = $"Error deleting route: {ex.Message}";
                    MessageBox.Show($"Failed to delete route: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewSchedule()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            // TODO: Open schedule view
            MessageBox.Show($"Schedule view for {SelectedRoute.RouteName} - Coming in next phase!",
                "Feature Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void RefreshData()
        {
            StatusMessage = "Refreshing data...";
            // await LoadInitialData();
            StatusMessage = "Data refreshed successfully";
        }

        private void GenerateReport()
        {
            // TODO: Generate route assignment report
            MessageBox.Show("Route assignment report generation - Coming in next phase!",
                "Feature Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Data Loading

        private async Task LoadDataFromServiceAsync()
        {
            try
            {
                if (_routeService == null)
                {
                    LoadMockData();
                    return;
                }

                UnassignedStudents.Clear();
                AvailableRoutes.Clear();
                AvailableBuses.Clear();
                AvailableDrivers.Clear();

                IsLoading = true;

                var studentsTask = _routeService.GetUnassignedStudentsAsync();
                var routesTask = _routeService.GetRoutesWithCapacityAsync();
                var busesTask = _routeService.GetAvailableBusesAsync();
                var driversTask = _routeService.GetAvailableDriversAsync();

                await Task.WhenAll(studentsTask, routesTask, busesTask, driversTask);

                if (studentsTask.Result.IsSuccess && studentsTask.Result.Value != null)
                {
                    foreach (var s in studentsTask.Result.Value)
                    {
                        UnassignedStudents.Add(s);
                    }
                }

                if (routesTask.Result.IsSuccess && routesTask.Result.Value != null)
                {
                    foreach (var r in routesTask.Result.Value)
                    {
                        AvailableRoutes.Add(r);
                    }
                }

                if (busesTask.Result.IsSuccess && busesTask.Result.Value != null)
                {
                    foreach (var b in busesTask.Result.Value)
                    {
                        AvailableBuses.Add(b);
                    }
                }

                if (driversTask.Result.IsSuccess && driversTask.Result.Value != null)
                {
                    foreach (var d in driversTask.Result.Value)
                    {
                        AvailableDrivers.Add(d);
                    }
                }

                if (AvailableRoutes.Any())
                {
                    if (_preselectedRouteId.HasValue)
                    {
                        var match = AvailableRoutes.FirstOrDefault(r => r.RouteId == _preselectedRouteId.Value);
                        if (match != null)
                        {
                            SelectedRoute = match;
                        }
                        else
                        {
                            SelectedRoute = AvailableRoutes.First();
                        }
                    }
                    else
                    {
                        SelectedRoute = AvailableRoutes.First();
                    }
                }

                OnPropertyChanged(nameof(UnassignedStudentCount));
                UpdateStatusMessage();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading data from service; falling back to mock data");
                LoadMockData();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadMockData()
        {
            try
            {
                // Mock unassigned students
                UnassignedStudents.Clear();
                for (int i = 1; i <= 25; i++)
                {
                    UnassignedStudents.Add(new BusBuddy.Core.Models.Student
                    {
                        StudentId = i,
                        StudentNumber = $"STU{i:000}",
                        StudentName = $"Student {i}",
                        Grade = (i % 12 + 1).ToString(),
                        // Address = $"{i * 100} Main Street", // Removed for MVP
                        Active = true
                    });
                }

                // Mock routes
                AvailableRoutes.Clear();
                for (int i = 1; i <= 5; i++)
                {
                    AvailableRoutes.Add(new BusBuddy.Core.Models.Route
                    {
                        RouteId = i,
                        RouteName = $"Route {i}",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Mock Elementary School"
                    });
                }

                // Mock buses
                AvailableBuses.Clear();
                for (int i = 1; i <= 10; i++)
                {
                    AvailableBuses.Add(new BusBuddy.Core.Models.Bus
                    {
                        VehicleId = i,
                        BusNumber = $"Bus-{i:000}",
                        Make = "Mock Bus",
                        Model = "School Bus",
                        Year = 2020,
                        Status = "Active"
                    });
                }

                // Mock drivers
                AvailableDrivers.Clear();
                for (int i = 1; i <= 8; i++)
                {
                    AvailableDrivers.Add(new BusBuddy.Core.Models.Driver
                    {
                        DriverId = i,
                        DriverName = $"Driver {i}",
                        Status = "Active"
                    });
                }

                // Select first route or preselected
                if (AvailableRoutes.Any())
                {
                    if (_preselectedRouteId.HasValue)
                    {
                        var match = AvailableRoutes.FirstOrDefault(r => r.RouteId == _preselectedRouteId.Value);
                        if (match != null)
                        {
                            SelectedRoute = match;
                        }
                        else
                        {
                            SelectedRoute = AvailableRoutes.First();
                        }
                    }
                    else
                    {
                        SelectedRoute = AvailableRoutes.First();
                    }
                }

                OnPropertyChanged(nameof(UnassignedStudentCount));
                UpdateStatusMessage();

                Logger.Information("Loaded mock data: {StudentCount} students, {RouteCount} routes, {BusCount} buses, {DriverCount} drivers",
                    UnassignedStudents.Count, AvailableRoutes.Count, AvailableBuses.Count, AvailableDrivers.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load mock data");
                StatusMessage = $"Error loading mock data: {ex.Message}";
            }
        }

        private async Task LoadInitialData()
        {
            try
            {
                if (_routeService != null)
                {
                    var result = await _routeService.GetAllRoutesAsync();
                    if (result.IsSuccess && result.Value != null)
                    {
                        _availableRoutes.Clear();
                        foreach (var route in result.Value)
                        {
                            _availableRoutes.Add(route);
                        }
                        // TODO: Add similar logic for students, buses, drivers if services are available
                        OnPropertyChanged(nameof(AvailableRoutes));
                        UpdateStatusMessage();
                        Logger.Information("Loaded Azure SQL data: {RouteCount} routes", _availableRoutes.Count);
                        return;
                    }
                    else
                    {
                        Logger.Warning("RouteService.GetAllRoutesAsync failed or returned no data, falling back to mocks. Error: {Error}", result.Error);
                    }
                }
                else
                {
                    Logger.Warning("_routeService is null, falling back to mock data.");
                }
                // Fallback to mock data if service is unavailable or fails
                LoadMockData();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load initial data");
                StatusMessage = $"Error loading data: {ex.Message}";
                MessageBox.Show($"Failed to load data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterStudents()
        {
            // TODO: Implement search filtering
            // For MVP, basic filtering can be handled by the SfDataGrid's built-in filtering
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
        /// Basic plotting of the selected route's currently assigned students onto the shared map (GoogleEarthViewModel).
        /// Reuses existing GoogleEarthViewModel marker infrastructure; only plots students with coordinates or successfully geocoded addresses.
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

                var sp = App.ServiceProvider;
                if (sp == null)
                {
                    StatusMessage = "Mapping unavailable (no service provider)";
                    return;
                }

                var mapVm = sp.GetService<GoogleEarthViewModel>();
                if (mapVm == null)
                {
                    StatusMessage = "Map VM not registered";
                    return;
                }

                var geocoder = sp.GetService<IGeocodingService>();

                // Remove previous dynamic student markers (keep seeded school anchor)
                for (int i = mapVm.MapMarkers.Count - 1; i >= 0; i--)
                {
                    var m = mapVm.MapMarkers[i];
                    if (!string.Equals(m.Label, "Wiley School RE-13JT", StringComparison.OrdinalIgnoreCase))
                    {
                        mapVm.MapMarkers.RemoveAt(i);
                    }
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
                            if (s.Latitude.HasValue && s.Longitude.HasValue)
                            {
                                lat = (double)s.Latitude.Value;
                                lon = (double)s.Longitude.Value;
                            }
                            else if (geocoder != null && !string.IsNullOrWhiteSpace(s.HomeAddress))
                            {
                                var r = await geocoder.GeocodeAsync(s.HomeAddress, s.City, s.State, s.Zip);
                                if (r != null)
                                {
                                    lat = r.Value.latitude;
                                    lon = r.Value.longitude;
                                }
                            }

                            if (lat == null || lon == null)
                            {
                                continue; // skip if no coordinates
                            }

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                mapVm.MapMarkers.Add(new GoogleEarthViewModel.MapMarker
                                {
                                    Label = s.StudentName,
                                    Latitude = lat.Value,
                                    Longitude = lon.Value
                                });
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

        #region IDisposable
        public void Dispose()
        {
            try { _retimeDebounceTimer?.Dispose(); } catch { }
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
