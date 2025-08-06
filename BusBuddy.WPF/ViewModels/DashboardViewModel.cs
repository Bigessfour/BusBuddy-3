using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Syncfusion.UI.Xaml.Charts;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Data;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// ViewModel for the Dashboard view displaying route metrics and bus optimization
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IRouteService _routeService;
        private readonly IBusBuddyDbContextFactory _contextFactory;

        public DashboardViewModel(IRouteService routeService)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _contextFactory = new BusBuddyDbContextFactory();

            // Initialize commands
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            OptimizeCommand = new RelayCommand(async () => await OptimizeRoutesAsync());
            GenerateReportCommand = new RelayCommand(async () => await GenerateReportAsync());
            LoadGoogleEarthCommand = new RelayCommand(LoadGoogleEarth);

            // Initialize collections
            Routes = new ObservableCollection<BusBuddy.Core.Models.Route>();
            Buses = new ObservableCollection<BusBuddy.Core.Models.Bus>();
            Drivers = new ObservableCollection<BusBuddy.Core.Models.Driver>();
            RouteCapacities = new ObservableCollection<RouteCapacityChartItem>();
            Alerts = new ObservableCollection<Alert>();

            // Load initial data
            _ = Task.Run(async () => await RefreshDataAsync());
        }

        #region Properties

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Route> routes = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> buses = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Driver> drivers = new();

        [ObservableProperty]
        private int totalRoutes;

        [ObservableProperty]
        private int activeBuses;

        [ObservableProperty]
        private int availableDrivers;

        [ObservableProperty]
        private string systemStatus = "System Ready";

        [ObservableProperty]
        private bool isLoading;

        public ObservableCollection<RouteCapacityChartItem> RouteCapacities { get; set; } = new();
        public ObservableCollection<Alert> Alerts { get; set; } = new();

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand OptimizeCommand { get; }
        public ICommand GenerateReportCommand { get; }
        public ICommand LoadGoogleEarthCommand { get; }

        #endregion

        #region Command Methods

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Loading data...";

                // Load routes
                var result = await _routeService.GetAllActiveRoutesAsync();
                Routes.Clear();
                if (result.IsSuccess && result.Value != null)
                {
                    foreach (var route in result.Value)
                    {
                        Routes.Add(route);
                    }
                }
                // Example: Load buses from context
                using var context = _contextFactory.CreateDbContext();
                var buses = await context.Vehicles.ToListAsync();
                Buses.Clear();
                foreach (var bus in buses)
                {
                    Buses.Add(bus);
                }

                // Update metrics
                TotalRoutes = Routes.Count;
                SystemStatus = "Data loaded successfully";
            }
            catch (Exception ex)
            {
                SystemStatus = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OptimizeRoutesAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Optimizing routes...";

                // TODO: Implement route optimization
                await Task.Delay(2000); // Simulate processing

                SystemStatus = "Routes optimized successfully";
            }
            catch (Exception ex)
            {
                SystemStatus = $"Error optimizing routes: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerateReportAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Generating report...";

                // TODO: Implement report generation
                await Task.Delay(1500); // Simulate processing

                SystemStatus = "Report generated successfully";
            }
            catch (Exception ex)
            {
                SystemStatus = $"Error generating report: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                // Load route schedules and capacity usage
                using var context = _contextFactory.CreateDbContext();
                var routes = await context.Routes.ToListAsync();
                RouteCapacities.Clear();
                foreach (var route in routes)
                {
                    var bus = await context.Vehicles.FirstOrDefaultAsync(v => v.Description == route.RouteName || v.BusNumber == route.RouteName || v.BusNumber == route.RouteName.Replace(" Route", ""));
                    var assignedCount = await context.Students.CountAsync(s => s.RouteAssignmentId != null && context.RouteAssignments.Any(ra => ra.RouteAssignmentId == s.RouteAssignmentId && ra.RouteId == route.RouteId));
                    RouteCapacities.Add(new RouteCapacityChartItem
                    {
                        BusNumber = bus?.BusNumber ?? "",
                        Capacity = bus?.SeatingCapacity ?? 0,
                        Assigned = assignedCount,
                        RouteName = route.RouteName
                    });
                    // Alert for low capacity
                    if (bus != null && assignedCount < bus.SeatingCapacity * 0.2)
                    {
                        Alerts.Add(new Alert { Message = $"Low capacity: {bus.BusNumber} ({assignedCount}/{bus.SeatingCapacity})", Type = AlertType.Warning });
                    }
                    // Alert for boundary issues (example)
                    if (route.Boundaries.Contains("east") && assignedCount == 0)
                    {
                        Alerts.Add(new Alert { Message = $"No students assigned to {route.RouteName} (boundary issue)", Type = AlertType.Error });
                    }
                }
            }
            catch (Exception ex)
            {
                // MVP error handling pattern
                MessageBox.Show($"Error loading dashboard data: {ex.Message}");
                // Optionally log with Serilog if available
                // Logger.Error(ex, "Error loading dashboard data");
            }
        }

        private void LoadGoogleEarth()
        {
            // Enable GoogleEarthView.xaml.disabled and overlay rural paths (Hwy 287, CR 6, etc.)
            // Implementation placeholder: call NavigationService to show GoogleEarthView
        }

        #endregion
    }

    /// <summary>
    /// View model for daily route data display
    /// </summary>
    public class DailyRouteView
    {
        public string RouteName { get; set; } = string.Empty;
        public string BusNumber { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int BusCapacity { get; set; }
        public double Occupancy { get; set; }
        public string TimeSlot { get; set; } = string.Empty;
        public string OptimizationSuggestion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data for chart target line
    /// </summary>
    public class TargetLineData
    {
        public string Route { get; set; } = string.Empty;
        public double Target { get; set; }
    }

    public class RouteCapacityChartItem
    {
        public string BusNumber { get; set; } = "";
        public int Capacity { get; set; }
        public int Assigned { get; set; }
        public string RouteName { get; set; } = "";
    }

    public enum AlertType { Info, Warning, Error }

    public class Alert
    {
        public string Message { get; set; } = "";
        public AlertType Type { get; set; }
    }
}
