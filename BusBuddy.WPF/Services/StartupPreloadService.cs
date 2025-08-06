using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Serilog;
using BusBuddy.Core.Utilities;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service to preload essential data during application startup for better performance
    /// </summary>
    public class StartupPreloadService : IStartupPreloadService
    {
        private readonly IEnhancedCachingService _cachingService;
        private readonly IBusService _busService;
        private readonly IDriverService _driverService;
        private readonly IRouteService _routeService;
        private readonly IStudentService _studentService;
        private static readonly ILogger Logger = Log.ForContext<StartupPreloadService>();

        public StartupPreloadService(
            IEnhancedCachingService cachingService,
            IBusService busService,
            IDriverService driverService,
            IRouteService routeService,
            IStudentService studentService)
        {
            _cachingService = cachingService;
            _busService = busService;
            _driverService = driverService;
            _routeService = routeService;
            _studentService = studentService;
        }

        /// <summary>
        /// Preload all essential data needed for the application dashboard
        /// </summary>
        public async Task PreloadEssentialDataAsync()
        {
            Logger.Information("Starting essential data preload");
            var overallStopwatch = Stopwatch.StartNew();

            try
            {
                // Load data in parallel for better performance
                var preloadTasks = new[]
                {
                    PreloadBusesAsync(),
                    PreloadDriversAsync(),
                    PreloadRoutesAsync(),
                    PreloadStudentsAsync()
                };

                await Task.WhenAll(preloadTasks);

                overallStopwatch.Stop();
                Logger.Information("Essential data preload completed in {ElapsedMs}ms", overallStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                overallStopwatch.Stop();
                Logger.Error(ex, "Error during essential data preload after {ElapsedMs}ms", overallStopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Preload dashboard-specific data
        /// </summary>
        public async Task PreloadDashboardDataAsync()
        {
            Logger.Information("Starting dashboard data preload");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Load essential data for dashboard metrics
                await PreloadEssentialDataAsync();

                stopwatch.Stop();
                Logger.Information("Dashboard data preload completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Error during dashboard data preload after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Check if essential data is already cached
        /// </summary>
        public bool IsEssentialDataCached()
        {
            // This is a simple check - in a real implementation, you might want to be more sophisticated
            try
            {
                var busesTask = _cachingService.GetAllBusesAsync(async () => await _busService.GetAllBusesAsync());
                var driversTask = _cachingService.GetAllDriversAsync(async () => await _driverService.GetAllDriversAsync());
                var routesTask = _cachingService.GetAllRoutesAsync(async () => {
                    var result = await _routeService.GetAllActiveRoutesAsync();
                    return result.IsSuccess ? result.Value : Enumerable.Empty<BusBuddy.Core.Models.Route>();
                });
                var studentsTask = _cachingService.GetAllStudentsAsync(async () => await _studentService.GetAllStudentsAsync());

                // Check if any of these complete immediately (indicating cached data)
                return busesTask.IsCompletedSuccessfully || driversTask.IsCompletedSuccessfully ||
                       routesTask.IsCompletedSuccessfully || studentsTask.IsCompletedSuccessfully;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get preload statistics
        /// </summary>
        public async Task<PreloadStatistics> GetPreloadStatisticsAsync()
        {
            var stats = new PreloadStatistics();

            try
            {
                var buses = await _cachingService.GetAllBusesAsync(async () => await _busService.GetAllBusesAsync());
                var drivers = await _cachingService.GetAllDriversAsync(async () => await _driverService.GetAllDriversAsync());
                var routes = await _cachingService.GetAllRoutesAsync(async () => {
                    var result = await _routeService.GetAllActiveRoutesAsync();
                    return result.IsSuccess ? result.Value : Enumerable.Empty<BusBuddy.Core.Models.Route>();
                });
                var students = await _cachingService.GetAllStudentsAsync(async () => await _studentService.GetAllStudentsAsync());

                var routeList = routes as IList<BusBuddy.Core.Models.Route> ?? routes.ToList();
                stats.BusCount = buses.Count;
                stats.DriverCount = drivers.Count;
                stats.RouteCount = routeList.Count;
                stats.StudentCount = students.Count;
                stats.IsDataCached = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting preload statistics");
                stats.IsDataCached = false;
            }

            return stats;
        }

        private async Task PreloadBusesAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _cachingService.GetAllBusesAsync(async () => await _busService.GetAllBusesAsync());
                stopwatch.Stop();
                Logger.Debug("Preloaded buses in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error preloading buses");
            }
        }

        private async Task PreloadDriversAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _cachingService.GetAllDriversAsync(async () => await _driverService.GetAllDriversAsync());
                stopwatch.Stop();
                Logger.Debug("Preloaded drivers in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error preloading drivers");
            }
        }

        private async Task PreloadRoutesAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _cachingService.GetAllRoutesAsync(async () => {
                    var result = await _routeService.GetAllActiveRoutesAsync();
                    return result.Value;
                });
                stopwatch.Stop();
                Logger.Debug("Preloaded routes in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error preloading routes");
            }
        }

        private async Task PreloadStudentsAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _cachingService.GetAllStudentsAsync(async () => await _studentService.GetAllStudentsAsync());
                stopwatch.Stop();
                Logger.Debug("Preloaded students in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error preloading students");
            }
        }
    }

    /// <summary>
    /// Interface for startup preload service
    /// </summary>
    public interface IStartupPreloadService
    {
        Task PreloadEssentialDataAsync();
        Task PreloadDashboardDataAsync();
        bool IsEssentialDataCached();
        Task<PreloadStatistics> GetPreloadStatisticsAsync();
    }

    /// <summary>
    /// Statistics about preloaded data
    /// </summary>
    public class PreloadStatistics
    {
        public int BusCount { get; set; }
        public int DriverCount { get; set; }
        public int RouteCount { get; set; }
        public int StudentCount { get; set; }
        public bool IsDataCached { get; set; }
    }
}
