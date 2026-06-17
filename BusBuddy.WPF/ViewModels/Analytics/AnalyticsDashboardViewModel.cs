using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Analytics
{
    public partial class AnalyticsDashboardViewModel : ObservableObject
    {
        private readonly IFuelService _fuelService;
        private readonly IMaintenanceService _maintenanceService;
        private readonly IRouteService _routeService;
        private readonly IBusService _busService;

        public AnalyticsDashboardViewModel(
            IFuelService fuelService,
            IMaintenanceService maintenanceService,
            IRouteService routeService,
            IBusService busService)
        {
            _fuelService = fuelService;
            _maintenanceService = maintenanceService;
            _routeService = routeService;
            _busService = busService;

            FleetPerformance = new ObservableCollection<AnalyticsChartPoint>();
            RouteEfficiency = new ObservableCollection<AnalyticsChartPoint>();
            MaintenanceMetrics = new ObservableCollection<AnalyticsChartPoint>();
            FuelAnalytics = new ObservableCollection<AnalyticsChartPoint>();

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            _ = Task.Run(RefreshAsync);
        }

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> fleetPerformance;

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> routeEfficiency;

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> maintenanceMetrics;

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> fuelAnalytics;

        [ObservableProperty]
        private string statusMessage = "Loading analytics...";

        [ObservableProperty]
        private bool isLoading;

        public IAsyncRelayCommand RefreshCommand { get; }

        private async Task RefreshAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading analytics...";

                var buses = (await _busService.GetAllBusesAsync()).ToList();
                FleetPerformance.Clear();
                FleetPerformance.Add(new AnalyticsChartPoint { Label = "Active", Value = buses.Count(b => b.Status == "Active") });
                FleetPerformance.Add(new AnalyticsChartPoint { Label = "Maintenance", Value = buses.Count(b => b.Status == "Maintenance") });
                FleetPerformance.Add(new AnalyticsChartPoint { Label = "Out of Service", Value = buses.Count(b => b.Status == "Out of Service") });

                var utilization = await _routeService.GetRouteUtilizationStatsAsync();
                RouteEfficiency.Clear();
                if (utilization.IsSuccess && utilization.Value != null)
                {
                    var stats = utilization.Value;
                    RouteEfficiency.Add(new AnalyticsChartPoint { Label = "Assigned", Value = stats.TotalAssignedStudents });
                    RouteEfficiency.Add(new AnalyticsChartPoint { Label = "Unassigned", Value = stats.TotalUnassignedStudents });
                    RouteEfficiency.Add(new AnalyticsChartPoint { Label = "At Capacity", Value = stats.RoutesAtCapacity });
                }

                var maintenance = (await _maintenanceService.GetAllMaintenanceRecordsAsync()).ToList();
                MaintenanceMetrics.Clear();
                MaintenanceMetrics.Add(new AnalyticsChartPoint { Label = "Scheduled", Value = maintenance.Count(m => m.Status == "Scheduled") });
                MaintenanceMetrics.Add(new AnalyticsChartPoint { Label = "In Progress", Value = maintenance.Count(m => m.Status == "In Progress") });
                MaintenanceMetrics.Add(new AnalyticsChartPoint { Label = "Completed", Value = maintenance.Count(m => m.Status == "Completed") });

                var fuel = (await _fuelService.GetAllFuelRecordsAsync()).ToList();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var recent = fuel.Where(f => f.FuelDate >= cutoff).ToList();
                FuelAnalytics.Clear();
                FuelAnalytics.Add(new AnalyticsChartPoint
                {
                    Label = "Gallons (30d)",
                    Value = (double)recent.Sum(f => f.Gallons)
                });
                FuelAnalytics.Add(new AnalyticsChartPoint
                {
                    Label = "Records",
                    Value = recent.Count
                });

                StatusMessage = $"Analytics updated — {buses.Count} buses, {fuel.Count} fuel records";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading analytics: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class AnalyticsChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
