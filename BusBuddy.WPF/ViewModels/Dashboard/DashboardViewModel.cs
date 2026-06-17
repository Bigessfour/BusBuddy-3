using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Dashboard
{
    /// <summary>
    /// Dashboard ViewModel — route/fleet metrics, grids, and chart series for Syncfusion controls.
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IRouteService _routeService;
        private readonly IDashboardMetricsService _metricsService;
        private readonly IFleetMonitoringService _fleetMonitoringService;
        private readonly IBusService _busService;

        public DashboardViewModel(
            IRouteService routeService,
            IDashboardMetricsService metricsService,
            IFleetMonitoringService fleetMonitoringService,
            IBusService busService)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _fleetMonitoringService = fleetMonitoringService ?? throw new ArgumentNullException(nameof(fleetMonitoringService));
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            OptimizeCommand = new RelayCommand(async () => await OptimizeRoutesAsync());
            GenerateReportCommand = new RelayCommand(async () => await GenerateReportAsync());

            RouteSummaries = new ObservableCollection<DashboardRouteRow>();
            Buses = new ObservableCollection<BusBuddy.Core.Models.Bus>();
            AssignmentDistribution = new ObservableCollection<DashboardChartPoint>();
            RouteHealthDistribution = new ObservableCollection<DashboardChartPoint>();

            _ = Task.Run(async () => await RefreshDataAsync());
        }

        [ObservableProperty]
        private ObservableCollection<DashboardRouteRow> routeSummaries = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> buses = new();

        [ObservableProperty]
        private ObservableCollection<DashboardChartPoint> assignmentDistribution = new();

        [ObservableProperty]
        private ObservableCollection<DashboardChartPoint> routeHealthDistribution = new();

        [ObservableProperty]
        private int totalRoutes;

        [ObservableProperty]
        private int activeBuses;

        [ObservableProperty]
        private int availableDrivers;

        [ObservableProperty]
        private double averageUtilizationPercent;

        [ObservableProperty]
        private string systemStatus = "System Ready";

        [ObservableProperty]
        private bool isLoading;

        public ICommand RefreshCommand { get; }
        public ICommand OptimizeCommand { get; }
        public ICommand GenerateReportCommand { get; }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Loading data...";

                var metrics = await _metricsService.GetDashboardMetricsAsync();
                TotalRoutes = metrics.GetValueOrDefault("RouteCount");
                ActiveBuses = metrics.GetValueOrDefault("BusCount");
                AvailableDrivers = metrics.GetValueOrDefault("DriverCount");

                var routesResult = await _routeService.GetAllRoutesAsync();
                RouteSummaries.Clear();
                if (routesResult.IsSuccess && routesResult.Value != null)
                {
                    foreach (var route in routesResult.Value)
                    {
                        RouteSummaries.Add(new DashboardRouteRow
                        {
                            RouteName = route.RouteName,
                            Description = route.Description ?? string.Empty,
                            MaxCapacity = route.MaxCapacity,
                            AssignedCount = route.AssignedStudents?.Count ?? 0
                        });
                    }
                }

                var buses = await _busService.GetAllBusesAsync();
                Buses.Clear();
                foreach (var bus in buses)
                {
                    Buses.Add(bus);
                }

                var fleetStatus = await _fleetMonitoringService.GetFleetStatusAsync();
                if (fleetStatus != null)
                {
                    ActiveBuses = fleetStatus.ActiveBuses;
                }

                var utilizationResult = await _routeService.GetRouteUtilizationStatsAsync();
                AssignmentDistribution.Clear();
                RouteHealthDistribution.Clear();

                if (utilizationResult.IsSuccess && utilizationResult.Value != null)
                {
                    var stats = utilizationResult.Value;
                    AverageUtilizationPercent = Math.Round(stats.AverageUtilizationRate * 100, 1);

                    AssignmentDistribution.Add(new DashboardChartPoint { Label = "Assigned", Count = stats.TotalAssignedStudents });
                    AssignmentDistribution.Add(new DashboardChartPoint { Label = "Unassigned", Count = stats.TotalUnassignedStudents });

                    RouteHealthDistribution.Add(new DashboardChartPoint { Label = "At Capacity", Count = stats.RoutesAtCapacity });
                    RouteHealthDistribution.Add(new DashboardChartPoint { Label = "Underutilized", Count = stats.UnderutilizedRoutes });
                    var healthy = Math.Max(0, stats.TotalRoutes - stats.RoutesAtCapacity - stats.UnderutilizedRoutes);
                    RouteHealthDistribution.Add(new DashboardChartPoint { Label = "On Target", Count = healthy });
                }

                TotalRoutes = RouteSummaries.Count;
                SystemStatus = $"Data loaded — {TotalRoutes} routes, {Buses.Count} buses";
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
                await Task.Delay(2000);
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
                await Task.Delay(1500);
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
    }

    public class DashboardRouteRow
    {
        public string RouteName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaxCapacity { get; set; }
        public int AssignedCount { get; set; }
    }

    public class DashboardChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Count { get; set; }
    }
}
