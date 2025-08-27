using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.WPF.Commands;

namespace BusBuddy.WPF.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private new static readonly ILogger Logger = Log.ForContext<MainWindowViewModel>();
        private string _title = "BusBuddy - School Transportation Management";

        // Optional services for database connectivity
        private readonly IStudentService? _studentService;
        private readonly IDriverService? _driverService;
        private readonly IRouteService? _routeService;
        private readonly BusService? _busService;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        // Collections for the main grids - using fully qualified names to avoid namespace conflicts
        public ObservableCollection<BusBuddy.Core.Domain.Student> Students { get; set; } = new();
        public ObservableCollection<BusBuddy.Core.Domain.Route> Routes { get; set; } = new();
        public ObservableCollection<BusBuddy.Core.Domain.Bus> Buses { get; set; } = new();
        public ObservableCollection<BusBuddy.Core.Domain.Driver> Drivers { get; set; } = new();

        // MVVM Commands for UI accessibility and Azure SQL operations
        public ICommand? NavigateToStudentsCommand { get; private set; }
        public ICommand? NavigateToRoutesCommand { get; private set; }
        public ICommand? NavigateToBusesCommand { get; private set; }
        public ICommand? NavigateToDriversCommand { get; private set; }
        public ICommand? NavigateToMapCommand { get; private set; }
        public ICommand? NavigateToReportsCommand { get; private set; }
        public ICommand? GenerateEligibilityPdfCommand { get; private set; }
        public ICommand? PrintEligibilityPdfCommand { get; private set; }
        public ICommand? AddStudentCommand { get; private set; }
        public ICommand? EditStudentCommand { get; private set; }
        public ICommand? AddBusCommand { get; private set; }
        public ICommand? AddDriverCommand { get; private set; }
        public ICommand? OptimizeRoutesCommand { get; private set; }
        public ICommand? ExportSchedulesCommand { get; private set; }
        public ICommand? MaintenanceCommand { get; private set; }
        public ICommand? FleetStatusCommand { get; private set; }
        public ICommand? AssignBusCommand { get; private set; }
        public ICommand? ScheduleCommand { get; private set; }
        public ICommand? SwitchToDarkThemeCommand { get; private set; }
        public ICommand? SwitchToLightThemeCommand { get; private set; }

        // Default constructor - uses sample data (current working approach)
        public MainWindowViewModel()
        {
            Logger.Information("MainWindowViewModel initialized with sample data");
            InitializeCommands();
            LoadSampleData();
        }

        // DI constructor - uses database services when available
        public MainWindowViewModel(
            IStudentService studentService,
            IDriverService driverService,
            IRouteService routeService,
            BusService busService)
        {
            _studentService = studentService;
            _driverService = driverService;
            _routeService = routeService;
            _busService = busService;

            Logger.Information("MainWindowViewModel initialized with database services");
            InitializeCommands();
            LoadDatabaseDataAsync();
        }

        private void InitializeCommands()
        {
            // Navigation Commands
            NavigateToStudentsCommand = new RelayCommand(() => OnNavigateToStudents());
            NavigateToRoutesCommand = new RelayCommand(() => OnNavigateToRoutes());
            NavigateToBusesCommand = new RelayCommand(() => OnNavigateToBuses());
            NavigateToDriversCommand = new RelayCommand(() => OnNavigateToDrivers());
            NavigateToMapCommand = new RelayCommand(() => OnNavigateToMap());
            NavigateToReportsCommand = new RelayCommand(() => OnNavigateToReports());

            // Document Commands
            GenerateEligibilityPdfCommand = new RelayCommand(() => OnGenerateEligibilityPdf());
            PrintEligibilityPdfCommand = new RelayCommand(() => OnPrintEligibilityPdf());

            // CRUD Commands for Azure SQL operations
            AddStudentCommand = new RelayCommand(() => OnAddStudent());
            EditStudentCommand = new RelayCommand(() => OnEditStudent());
            AddBusCommand = new RelayCommand(() => OnAddBus());
            AddDriverCommand = new RelayCommand(() => OnAddDriver());

            // Route Management Commands
            OptimizeRoutesCommand = new RelayCommand(() => OnOptimizeRoutes());
            ExportSchedulesCommand = new RelayCommand(() => OnExportSchedules());

            // Fleet Management Commands
            MaintenanceCommand = new RelayCommand(() => OnMaintenance());
            FleetStatusCommand = new RelayCommand(() => OnFleetStatus());
            AssignBusCommand = new RelayCommand(() => OnAssignBus());
            ScheduleCommand = new RelayCommand(() => OnSchedule());

            // Theme Commands
            SwitchToDarkThemeCommand = new RelayCommand(() => OnSwitchToDarkTheme());
            SwitchToLightThemeCommand = new RelayCommand(() => OnSwitchToLightTheme());
        }

        private async void LoadDatabaseDataAsync()
        {
            try
            {
                Logger.Information("Loading data from database...");

                // Test database connectivity first
                await TestDatabaseConnectivity();

                // Load actual data from services
                if (_studentService != null)
                {
                    var students = await _studentService.GetAllStudentsAsync();
                    Students.Clear();
                    foreach (var student in students)
                        Students.Add(student);
                    Logger.Information("Loaded {Count} students from database", Students.Count);
                }

                if (_routeService != null)
                {
                    var routesResult = await _routeService.GetAllRoutesAsync();
                    if (routesResult.IsSuccess && routesResult.Value != null)
                    {
                        Routes.Clear();
                        foreach (var route in routesResult.Value)
                            Routes.Add(route);
                        Logger.Information("Loaded {Count} routes from database", Routes.Count);
                    }
                    else
                    {
                        Logger.Warning("Failed to load routes: {Error}", routesResult.Error);
                    }
                }

                if (_busService != null)
                {
                    var buses = await _busService.GetAllBusesAsync();
                    Buses.Clear();
                    foreach (var bus in buses)
                        Buses.Add(bus);
                    Logger.Information("Loaded {Count} buses from database", Buses.Count);
                }

                if (_driverService != null)
                {
                    var drivers = await _driverService.GetAllDriversAsync();
                    Drivers.Clear();
                    foreach (var driver in drivers)
                        Drivers.Add(driver);
                    Logger.Information("Loaded {Count} drivers from database", Drivers.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load data from database, falling back to sample data");
                LoadSampleData();
            }
        }

        private async Task TestDatabaseConnectivity()
        {
            try
            {
                using var context = new BusBuddyDbContext();
                await context.Database.CanConnectAsync();
                Logger.Information("✅ Database connectivity test successful");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Database connectivity test failed");
                throw;
            }
        }

        private void LoadSampleData()
        {
            // Add sample students
            Students.Add(new BusBuddy.Core.Domain.Student
            {
                StudentNumber = "12345",
                StudentName = "John Doe",
                Grade = "5th",
                HomeAddress = "123 Main St"
            });
            Students.Add(new BusBuddy.Core.Domain.Student
            {
                StudentNumber = "12346",
                StudentName = "Jane Smith",
                Grade = "4th",
                HomeAddress = "456 Oak Ave"
            });

            // Add sample routes
            Routes.Add(new BusBuddy.Core.Domain.Route
            {
                RouteName = "Route 1",
                Date = System.DateTime.Today,
                Description = "Elementary School Route",
                School = "Riverside Elementary"
            });
            Routes.Add(new BusBuddy.Core.Domain.Route
            {
                RouteName = "Route 2",
                Date = System.DateTime.Today,
                Description = "Middle School Route",
                School = "Lincoln Middle School"
            });

            // Add sample buses
            Buses.Add(new BusBuddy.Core.Domain.Bus
            {
                BusNumber = "Bus 001",
                LicenseNumber = "ABC123",
                Make = "Bluebird",
                Model = "Vision",
                Year = 2020,
                SeatingCapacity = 35
            });
            Buses.Add(new BusBuddy.Core.Domain.Bus
            {
                BusNumber = "Bus 002",
                LicenseNumber = "DEF456",
                Make = "Thomas",
                Model = "Saf-T-Liner",
                Year = 2019,
                SeatingCapacity = 32
            });

            // Add sample drivers
            Drivers.Add(new BusBuddy.Core.Domain.Driver
            {
                DriverName = "Mike Johnson",
                DriverPhone = "555-012-3456",
                DriverEmail = "mike@busbuddy.com",
                DriversLicenceType = "CDL"
            });
            Drivers.Add(new BusBuddy.Core.Domain.Driver
            {
                DriverName = "Sarah Wilson",
                DriverPhone = "555-012-4567",
                DriverEmail = "sarah@busbuddy.com",
                DriversLicenceType = "CDL"
            });
        }

        // Simple refresh methods used by MainWindow after dialog operations
        public async Task RefreshStudentsAsync()
        {
            if (_studentService != null)
            {
                try
                {
                    var students = await _studentService.GetAllStudentsAsync();
                    Students.Clear();
                    foreach (var s in students) Students.Add(s);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "RefreshStudentsAsync failed; retaining existing collection");
                }
            }
        }

        public async Task RefreshRoutesAsync()
        {
            if (_routeService != null)
            {
                try
                {
                    var routesResult = await _routeService.GetAllRoutesAsync();
                    if (routesResult.IsSuccess && routesResult.Value != null)
                    {
                        Routes.Clear();
                        foreach (var r in routesResult.Value) Routes.Add(r);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "RefreshRoutesAsync failed; retaining existing collection");
                }
            }
        }

        public async Task RefreshBusesAsync()
        {
            if (_busService != null)
            {
                try
                {
                    var buses = await _busService.GetAllBusesAsync();
                    Buses.Clear();
                    foreach (var b in buses) Buses.Add(b);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "RefreshBusesAsync failed; retaining existing collection");
                }
            }
        }

        public async Task RefreshDriversAsync()
        {
            if (_driverService != null)
            {
                try
                {
                    var drivers = await _driverService.GetAllDriversAsync();
                    Drivers.Clear();
                    foreach (var d in drivers) Drivers.Add(d);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "RefreshDriversAsync failed; retaining existing collection");
                }
            }
        }

        #region Command Handlers

        // Navigation command handlers - these will be invoked by the View
        private void OnNavigateToStudents()
        {
            Logger.Information("Navigate to Students command executed via ViewModel");
            // Navigation logic will be handled by the View through events or messaging
            NavigationRequested?.Invoke("Students");
        }

        private void OnNavigateToRoutes()
        {
            Logger.Information("Navigate to Routes command executed via ViewModel");
            NavigationRequested?.Invoke("Routes");
        }

        private void OnNavigateToBuses()
        {
            Logger.Information("Navigate to Buses command executed via ViewModel");
            NavigationRequested?.Invoke("Buses");
        }

        private void OnNavigateToDrivers()
        {
            Logger.Information("Navigate to Drivers command executed via ViewModel");
            NavigationRequested?.Invoke("Drivers");
        }

        private void OnNavigateToMap()
        {
            Logger.Information("Navigate to Map command executed via ViewModel");
            NavigationRequested?.Invoke("Map");
        }

        private void OnNavigateToReports()
        {
            Logger.Information("Navigate to Reports command executed via ViewModel");
            NavigationRequested?.Invoke("Reports");
        }

        // Document command handlers
        private void OnGenerateEligibilityPdf()
        {
            Logger.Information("Generate Eligibility PDF command executed via ViewModel");
            DocumentActionRequested?.Invoke("GenerateEligibilityPdf");
        }

        private void OnPrintEligibilityPdf()
        {
            Logger.Information("Print Eligibility PDF command executed via ViewModel");
            DocumentActionRequested?.Invoke("PrintEligibilityPdf");
        }

        // CRUD command handlers for Azure SQL operations
        private async void OnAddStudent()
        {
            Logger.Information("Add Student command executed via ViewModel");
            try
            {
                if (_studentService != null)
                {
                    // This would normally create a new student via the service
                    // For now, trigger the UI action
                    CrudActionRequested?.Invoke("AddStudent");
                }
                else
                {
                    CrudActionRequested?.Invoke("AddStudent");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Add Student command");
            }
        }

        private async void OnEditStudent()
        {
            Logger.Information("Edit Student command executed via ViewModel");
            try
            {
                if (_studentService != null)
                {
                    // This would normally edit the selected student via the service
                    CrudActionRequested?.Invoke("EditStudent");
                }
                else
                {
                    CrudActionRequested?.Invoke("EditStudent");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Edit Student command");
            }
        }

        private async void OnAddBus()
        {
            Logger.Information("Add Bus command executed via ViewModel");
            try
            {
                if (_busService != null)
                {
                    // Azure SQL operation for adding bus
                    CrudActionRequested?.Invoke("AddBus");
                }
                else
                {
                    CrudActionRequested?.Invoke("AddBus");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Add Bus command");
            }
        }

        private async void OnAddDriver()
        {
            Logger.Information("Add Driver command executed via ViewModel");
            try
            {
                if (_driverService != null)
                {
                    // Azure SQL operation for adding driver
                    CrudActionRequested?.Invoke("AddDriver");
                }
                else
                {
                    CrudActionRequested?.Invoke("AddDriver");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Add Driver command");
            }
        }

        // Route management command handlers
        private async void OnOptimizeRoutes()
        {
            Logger.Information("Optimize Routes command executed via ViewModel");
            try
            {
                if (_routeService != null)
                {
                    // Azure SQL operation for route optimization
                    RouteActionRequested?.Invoke("OptimizeRoutes");
                }
                else
                {
                    RouteActionRequested?.Invoke("OptimizeRoutes");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Optimize Routes command");
            }
        }

        private async void OnExportSchedules()
        {
            Logger.Information("Export Schedules command executed via ViewModel");
            try
            {
                RouteActionRequested?.Invoke("ExportSchedules");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in Export Schedules command");
            }
        }

        // Fleet management command handlers
        private void OnMaintenance()
        {
            Logger.Information("Maintenance command executed via ViewModel");
            FleetActionRequested?.Invoke("Maintenance");
        }

        private void OnFleetStatus()
        {
            Logger.Information("Fleet Status command executed via ViewModel");
            FleetActionRequested?.Invoke("FleetStatus");
        }

        private void OnAssignBus()
        {
            Logger.Information("Assign Bus command executed via ViewModel");
            FleetActionRequested?.Invoke("AssignBus");
        }

        private void OnSchedule()
        {
            Logger.Information("Schedule command executed via ViewModel");
            FleetActionRequested?.Invoke("Schedule");
        }

        // Theme command handlers
        private void OnSwitchToDarkTheme()
        {
            Logger.Information("Switch to Dark Theme command executed via ViewModel");
            ThemeChangeRequested?.Invoke("FluentDark");
        }

        private void OnSwitchToLightTheme()
        {
            Logger.Information("Switch to Light Theme command executed via ViewModel");
            ThemeChangeRequested?.Invoke("FluentLight");
        }

        #endregion

        #region Events for View Communication

        // Events to communicate with the View while maintaining MVVM separation
        public event Action<string>? NavigationRequested;
        public event Action<string>? DocumentActionRequested;
        public event Action<string>? CrudActionRequested;
        public event Action<string>? RouteActionRequested;
        public event Action<string>? FleetActionRequested;
        public event Action<string>? ThemeChangeRequested;

        #endregion
    }
}
