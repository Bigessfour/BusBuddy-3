using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Dashboard
{
    /// <summary>
    /// Dashboard ViewModel — route/fleet metrics, grids, and chart series for Syncfusion controls.
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        private static readonly ILogger Logger = Log.ForContext<DashboardViewModel>();
        private readonly IRouteService _routeService;
        private readonly IDashboardMetricsService _metricsService;
        private readonly IFleetMonitoringService _fleetMonitoringService;
        private readonly IBusService _busService;
        private readonly IStudentRouteOptimizer? _routeOptimizer;
        private readonly IOperationalReportService? _reportService;

        public DashboardViewModel(
            IRouteService routeService,
            IDashboardMetricsService metricsService,
            IFleetMonitoringService fleetMonitoringService,
            IBusService busService,
            IStudentRouteOptimizer? routeOptimizer = null,
            IOperationalReportService? reportService = null)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _fleetMonitoringService = fleetMonitoringService ?? throw new ArgumentNullException(nameof(fleetMonitoringService));
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));
            _routeOptimizer = routeOptimizer;
            _reportService = reportService;

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            OptimizeCommand = new RelayCommand(async () => await OptimizeRoutesAsync());
            GenerateReportCommand = new RelayCommand(async () => await GenerateReportAsync());

            RouteSummaries = new ObservableCollection<DashboardRouteRow>();
            Buses = new ObservableCollection<BusBuddy.Core.Models.Bus>();
            AssignmentDistribution = new ObservableCollection<DashboardChartPoint>();
            RouteHealthDistribution = new ObservableCollection<DashboardChartPoint>();

            Logger.Information("DashboardViewModel constructed — starting initial refresh");
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

        public async Task RefreshDataAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                IsLoading = true;
                SystemStatus = "Loading data...";
                Logger.Information("Dashboard refresh started");

                var metrics = await _metricsService.GetDashboardMetricsAsync();
                TotalRoutes = metrics.GetValueOrDefault("RouteCount");
                ActiveBuses = metrics.GetValueOrDefault("BusCount");
                AvailableDrivers = metrics.GetValueOrDefault("DriverCount");
                Logger.Debug(
                    "Dashboard metrics RouteCount={RouteCount} BusCount={BusCount} DriverCount={DriverCount}",
                    TotalRoutes, ActiveBuses, AvailableDrivers);

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
                else
                {
                    Logger.Warning("Dashboard route load failed: {Error}", routesResult.Error);
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
                stopwatch.Stop();
                SystemStatus = $"Data loaded — {TotalRoutes} routes, {Buses.Count} buses";
                Logger.Information(
                    "Dashboard refresh completed Routes={RouteCount} Buses={BusCount} Drivers={DriverCount} Utilization={Utilization} ElapsedMs={ElapsedMs}",
                    TotalRoutes, Buses.Count, AvailableDrivers, AverageUtilizationPercent, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Dashboard refresh failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
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
                Logger.Information("Dashboard optimize routes started HasInjectedOptimizer={HasOptimizer}", _routeOptimizer is not null);
                var optimizer = _routeOptimizer ?? new StudentRouteOptimizer(_routeService);
                var result = await optimizer.OptimizeUnassignedAsync();
                await RefreshDataAsync();
                SystemStatus = result.Status;
                Logger.Information(
                    "Dashboard optimize routes completed Assigned={Assigned} Remaining={Remaining} MockAi={MockAi} Status={Status}",
                    result.AssignedCount, result.RemainingUnassigned, result.UsedMockAi, result.Status);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard optimize routes failed");
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
                if (_reportService is null)
                {
                    Logger.Warning("Dashboard generate report skipped — IOperationalReportService not registered");
                    SystemStatus = "Report service is not available";
                    return;
                }

                Logger.Information("Dashboard generate report started Kind={Kind}", OperationalReportKind.RouteSummary);
                var result = await _reportService.GenerateAsync(OperationalReportKind.RouteSummary);
                SystemStatus = result.Status;
                Logger.Information("Dashboard generate report completed Path={Path} MockAi={MockAi}", result.FilePath, result.UsedMockAi);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard generate report failed");
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
