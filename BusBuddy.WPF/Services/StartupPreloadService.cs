using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Serilog;
using BusBuddy.Core.Utilities;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service to preload essential data during application startup for better performance
    /// </summary>
    public class StartupPreloadService : IStartupPreloadService
    {
        private readonly IBusService _busService;
        private readonly IDriverService _driverService;
        private readonly IRouteService _routeService;
        private readonly IStudentService _studentService;
        private static readonly ILogger Logger = Log.ForContext<StartupPreloadService>();

        public StartupPreloadService(
            IBusService busService,
            IDriverService driverService,
            IRouteService routeService,
            IStudentService studentService)
        {
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
        /// Indicates whether essential data is available after preload.
        /// This method always returns false since caching has been removed and data is loaded on demand.
        /// </summary>
        public bool IsEssentialDataCached()
        {
            // Since we removed caching, this always returns false to indicate data needs to be loaded
            return false;
        }

        /// <summary>
        /// Get preload statistics
        /// </summary>
        public async Task<PreloadStatistics> GetPreloadStatisticsAsync()
        {
            var stats = new PreloadStatistics();

            try
            {
                var busesTask = _busService.GetAllBusesAsync();
                var driversTask = _driverService.GetAllDriversAsync();
                var routesResultTask = _routeService.GetAllActiveRoutesAsync();
                var studentsTask = _studentService.GetAllStudentsAsync();

                await Task.WhenAll(busesTask, driversTask, routesResultTask, studentsTask);

                var buses = await busesTask.ConfigureAwait(false);
                var drivers = await driversTask.ConfigureAwait(false);
                var routesResult = await routesResultTask.ConfigureAwait(false);
                var students = await studentsTask.ConfigureAwait(false);

                var routes = routesResult.IsSuccess ? routesResult.Value : Enumerable.Empty<Core.Domain.Route>();
                var routeList = routes.ToList();
                stats.BusCount = buses.Count();
                stats.DriverCount = drivers.Count;
                stats.RouteCount = routeList.Count;
                stats.StudentCount = students.Count;  // Changed from students.Count() to students.Count for better performance
                stats.IsDataAvailable = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting preload statistics");
                stats.IsDataAvailable = false;
            }

            return stats;
        }

        private async Task PreloadBusesAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _busService.GetAllBusesAsync();
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
                await _driverService.GetAllDriversAsync();
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
                await _routeService.GetAllActiveRoutesAsync();
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
                await _studentService.GetAllStudentsAsync();
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
    /// Statistics about preloaded data.
    /// The IsDataAvailable property indicates whether the preload operation successfully retrieved data.
    /// </summary>
    public class PreloadStatistics
    {
        public int BusCount { get; set; }
        public int DriverCount { get; set; }
        public int RouteCount { get; set; }
        public int StudentCount { get; set; }
        /// <summary>
        /// Indicates whether the preload operation successfully retrieved data.
        /// </summary>
        public bool IsDataAvailable { get; set; }
    }
}
