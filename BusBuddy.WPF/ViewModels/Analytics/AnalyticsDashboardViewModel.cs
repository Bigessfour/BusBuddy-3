using System;
using System.Collections.Generic;
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
        private static readonly string[] KnownBusStatuses = ["Active", "Maintenance", "Out of Service"];
        private static readonly string[] KnownMaintenanceStatuses = ["Scheduled", "In Progress", "Completed"];

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

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            _ = RefreshCommand.ExecuteAsync(null);
        }

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> fleetPerformance = new();

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> routeEfficiency = new();

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> maintenanceMetrics = new();

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> fuelGallons = new();

        [ObservableProperty]
        private ObservableCollection<AnalyticsChartPoint> fuelRecords = new();

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
                FleetPerformance = CountByStatus(
                    buses.Select(b => b.Status),
                    KnownBusStatuses);

                var utilization = await _routeService.GetRouteUtilizationStatsAsync();
                if (utilization.IsSuccess && utilization.Value != null)
                {
                    var stats = utilization.Value;
                    RouteEfficiency = CreatePoints(
                        ("Assigned", stats.TotalAssignedStudents),
                        ("Unassigned", stats.TotalUnassignedStudents));
                }
                else
                {
                    RouteEfficiency = new ObservableCollection<AnalyticsChartPoint>();
                }

                var maintenance = (await _maintenanceService.GetAllMaintenanceRecordsAsync()).ToList();
                MaintenanceMetrics = CountByStatus(
                    maintenance.Select(m => m.Status),
                    KnownMaintenanceStatuses);

                var fuel = (await _fuelService.GetAllFuelRecordsAsync()).ToList();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var recent = fuel.Where(f => f.FuelDate >= cutoff).ToList();
                FuelGallons = CreatePoints(("30 days", (double)recent.Sum(f => f.Gallons ?? 0)));
                FuelRecords = CreatePoints(("30 days", recent.Count));

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

        private static ObservableCollection<AnalyticsChartPoint> CountByStatus(
            IEnumerable<string> statuses,
            IReadOnlyList<string> knownStatuses)
        {
            var list = statuses.ToList();
            var points = knownStatuses
                .Select(status => new AnalyticsChartPoint
                {
                    Label = status,
                    Value = list.Count(item => string.Equals(item, status, StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

            var counted = points.Sum(point => point.Value);
            var remainder = list.Count - counted;
            if (remainder > 0)
            {
                points.Add(new AnalyticsChartPoint { Label = "Other", Value = remainder });
            }

            return new ObservableCollection<AnalyticsChartPoint>(points);
        }

        private static ObservableCollection<AnalyticsChartPoint> CreatePoints(
            params (string Label, double Value)[] items)
        {
            return new ObservableCollection<AnalyticsChartPoint>(
                items.Select(item => new AnalyticsChartPoint { Label = item.Label, Value = item.Value }));
        }
    }

    public class AnalyticsChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
