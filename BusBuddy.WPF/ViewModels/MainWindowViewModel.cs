using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
        public ObservableCollection<BusBuddy.Core.Models.Student> Students { get; set; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Route> Routes { get; set; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Bus> Buses { get; set; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Driver> Drivers { get; set; } = new();

        // Default constructor - uses sample data (current working approach)
        public MainWindowViewModel()
        {
            Logger.Information("MainWindowViewModel initialized with sample data");
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
            LoadDatabaseDataAsync();
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
            Students.Add(new BusBuddy.Core.Models.Student
            {
                StudentNumber = "12345",
                StudentName = "John Doe",
                Grade = "5th",
                HomeAddress = "123 Main St"
            });
            Students.Add(new BusBuddy.Core.Models.Student
            {
                StudentNumber = "12346",
                StudentName = "Jane Smith",
                Grade = "4th",
                HomeAddress = "456 Oak Ave"
            });

            // Add sample routes
            Routes.Add(new BusBuddy.Core.Models.Route
            {
                RouteName = "Route 1",
                Date = System.DateTime.Today,
                Description = "Elementary School Route",
                School = "Riverside Elementary"
            });
            Routes.Add(new BusBuddy.Core.Models.Route
            {
                RouteName = "Route 2",
                Date = System.DateTime.Today,
                Description = "Middle School Route",
                School = "Lincoln Middle School"
            });

            // Add sample buses
            Buses.Add(new BusBuddy.Core.Models.Bus
            {
                BusNumber = "Bus 001",
                LicenseNumber = "ABC123",
                Make = "Bluebird",
                Model = "Vision",
                Year = 2020,
                SeatingCapacity = 35
            });
            Buses.Add(new BusBuddy.Core.Models.Bus
            {
                BusNumber = "Bus 002",
                LicenseNumber = "DEF456",
                Make = "Thomas",
                Model = "Saf-T-Liner",
                Year = 2019,
                SeatingCapacity = 32
            });

            // Add sample drivers
            Drivers.Add(new BusBuddy.Core.Models.Driver
            {
                DriverName = "Mike Johnson",
                DriverPhone = "555-0123",
                DriverEmail = "mike@busbuddy.com",
                DriversLicenceType = "CDL"
            });
            Drivers.Add(new BusBuddy.Core.Models.Driver
            {
                DriverName = "Sarah Wilson",
                DriverPhone = "555-0124",
                DriverEmail = "sarah@busbuddy.com",
                DriversLicenceType = "CDL"
            });
        }
    }
}
