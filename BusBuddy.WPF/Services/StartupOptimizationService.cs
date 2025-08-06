using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;  // Added for IScheduleService
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service for handling optimized application initialization
    /// </summary>
    public class StartupOptimizationService
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly ILogger Logger = Log.ForContext<StartupOptimizationService>();

        public StartupOptimizationService(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Preloads critical services in parallel to optimize startup time
        /// </summary>
        public async Task PreloadCriticalServicesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.Information("[STARTUP_OPT] Beginning parallel preload of critical services");

            try
            {
                // Create a list of tasks to run in parallel
                var tasks = new Task[]
                {
                    Task.Run(() => PreloadService<IBusService>("BusService")),
                    Task.Run(() => PreloadService<IRouteService>("RouteService")),
                    Task.Run(() => PreloadService<IDriverService>("DriverService")),
                    Task.Run(() => PreloadService<IScheduleService>("ScheduleService")),
                };

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                stopwatch.Stop();
                Logger.Information("[STARTUP_OPT] Critical services preloaded in {DurationMs}ms",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "[STARTUP_OPT] Error preloading critical services after {ElapsedMs}ms: {ErrorMessage}",
                    stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }

        /// <summary>
        /// Optimizes object creation time by lazily preloading a specific service
        /// </summary>
        private void PreloadService<T>(string serviceName) where T : class
        {
            var serviceStopwatch = Stopwatch.StartNew();

            try
            {
                // Use CreateScope to avoid potential lifetime issues
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetService<T>();

                serviceStopwatch.Stop();

                if (service != null)
                {
                    Logger.Information("[STARTUP_OPT] Preloaded {ServiceName} in {DurationMs}ms",
                        serviceName, serviceStopwatch.ElapsedMilliseconds);
                }
                else
                {
                    Logger.Warning("[STARTUP_OPT] Service {ServiceName} could not be preloaded (not registered)",
                        serviceName);
                }
            }
            catch (Exception ex)
            {
                serviceStopwatch.Stop();
                Logger.Error(ex, "[STARTUP_OPT] Error preloading {ServiceName} after {ElapsedMs}ms: {ErrorMessage}",
                    serviceName, serviceStopwatch.ElapsedMilliseconds, ex.Message);
            }
        }
    }
}
