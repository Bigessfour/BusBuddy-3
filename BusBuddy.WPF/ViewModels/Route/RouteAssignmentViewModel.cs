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
                    NotifyCommandHintProperties();
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
                    OnPropertyChanged(nameof(CanEditStop));
                    OnPropertyChanged(nameof(CanMoveStopUp));
                    OnPropertyChanged(nameof(CanMoveStopDown));
                    RefreshCommandStates();
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
        public bool CanEditStop => SelectedRouteStop != null && SelectedRoute != null && !IsLoading;
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
        public ICommand EditStopCommand { get; private set; } = null!;
        public ICommand RemoveStopCommand { get; private set; } = null!;
        public ICommand MoveStopUpCommand { get; private set; } = null!;
        public ICommand MoveStopDownCommand { get; private set; } = null!;
        public ICommand ActivateRouteCommand { get; private set; } = null!;
        public ICommand DeactivateRouteCommand { get; private set; } = null!;
        // Plot currently assigned students for selected route

        public ICommand PlotRouteOnMapCommand { get; private set; } = null!;
        public ICommand RefreshDrivePathCommand { get; private set; } = null!;
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
            ViewScheduleCommand = new RelayCommand(async () => await ViewScheduleAsync(), () => SelectedRoute != null && !IsLoading);
            RefreshDataCommand = new RelayCommand(async () => await RefreshDataAsync(), () => !IsLoading);
            GenerateReportCommand = ExportRouteSheetCommand;

            // Enhanced Route Building Commands
            AssignVehicleCommand = new RelayCommand(async () => await AssignVehicleAsync(), () => CanAssignVehicle);
            AssignDriverCommand = new RelayCommand(async () => await AssignDriverAsync(), () => CanAssignDriver);
            AddStopCommand = new RelayCommand(async () => await AddStopAsync(), () => CanAddStop);
            EditStopCommand = new RelayCommand(async () => await EditStopAsync(), () => CanEditStop);
            RemoveStopCommand = new RelayCommand(async () => await RemoveStopAsync(), () => CanRemoveStop);
            MoveStopUpCommand = new RelayCommand(async () => await MoveStopUpAsync(), () => CanMoveStopUp);
            MoveStopDownCommand = new RelayCommand(async () => await MoveStopDownAsync(), () => CanMoveStopDown);
            ActivateRouteCommand = new RelayCommand(async () => await ActivateRouteAsync(), () => CanActivateRoute);
            DeactivateRouteCommand = new RelayCommand(async () => await DeactivateRouteAsync(), () => CanDeactivateRoute);
            PlotRouteOnMapCommand = new RelayCommand(async () => await PlotRouteOnMapAsync(), () => SelectedRoute != null);
            RefreshDrivePathCommand = new RelayCommand(
                async () => await RefreshAssignmentDrivePathAsync(),
                () => SelectedRoute != null && RouteStops.Count >= 2 && !IsLoading);
            TimeRouteCommand = new RelayCommand(async () => await TimeRouteStopsAsync(), () => SelectedRoute != null && RouteStops.Any() && IsStartTimeValid);
            PrintMapCommand = PrintRouteSheetCommand;
            GenerateRoutesCommand = new RelayCommand(async () => await GenerateRoutesAsync(), () => !_isGeneratingRoutes);
            GenerateTransferRoutesCommand = new RelayCommand(async () => await GenerateTransferRoutesAsync(), () => !_isGeneratingRoutes);
            // Re-evaluate map/ timing commands
            (PlotRouteOnMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshDrivePathCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportRouteSheetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintRouteSheetCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
            (RefreshDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AssignVehicleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AssignDriverCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddStopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditStopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveStopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveStopUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveStopDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ActivateRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeactivateRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PlotRouteOnMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshDrivePathCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportRouteSheetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintRouteSheetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateRoutesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateTransferRoutesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            NotifyCommandHintProperties();
        }
        private void ExportRouteAssignmentPdfAsync(bool includeMap)
        {
            SaveRouteSheet(includeMap);
        }

        #region IDisposable
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                Logger.Debug("Disposed RouteAssignmentViewModel resources");
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
