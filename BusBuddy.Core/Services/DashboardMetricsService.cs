using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Data.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Core.Services
{
    public interface IDashboardMetricsService
    {
        Task<Dictionary<string, int>> GetDashboardMetricsAsync();
    }

    public class DashboardMetricsService : IDashboardMetricsService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly ILogger Logger = Log.ForContext<DashboardMetricsService>();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        public DashboardMetricsService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<Dictionary<string, int>> GetDashboardMetricsAsync()
        {
            Logger.Information("Fetching dashboard metrics with optimized query");
            System.Diagnostics.Debug.WriteLine("[DEBUG] DashboardMetricsService.GetDashboardMetricsAsync START");
            var result = new Dictionary<string, int>();
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Use semaphore to prevent concurrent access issues
            await _semaphore.WaitAsync();

            try
            {
                // Create a new scoped DbContext for this operation to avoid threading issues
                using var scope = _serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<BusBuddyDbContext>();

                // Use EF Core async methods to avoid DbContext threading issues
                System.Diagnostics.Debug.WriteLine("[DEBUG] DashboardMetricsService: Starting bus count query");
                var busStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var busCount = await context.Vehicles.CountAsync(b => b.Status == "Active");
                busStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Query result BusCount: {busCount} (took {busStopwatch.ElapsedMilliseconds}ms)");

                System.Diagnostics.Debug.WriteLine("[DEBUG] DashboardMetricsService: Starting driver count query");
                var driverStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var driverCount = await context.Drivers.CountAsync(d => d.Status == "Active");
                driverStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Query result DriverCount: {driverCount} (took {driverStopwatch.ElapsedMilliseconds}ms)");

                System.Diagnostics.Debug.WriteLine("[DEBUG] DashboardMetricsService: Starting route count query");
                var routeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                // Routes are considered active based on IsActive flag
                var routeCount = await context.Routes.CountAsync(r => r.IsActive);
                routeStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Query result RouteCount: {routeCount} (took {routeStopwatch.ElapsedMilliseconds}ms)");

                result["BusCount"] = busCount;
                result["DriverCount"] = driverCount;
                result["RouteCount"] = routeCount;

                // Placeholders for future metrics
                result["StudentCount"] = 0;
                result["OpenTicketCount"] = 0;

                Logger.Information("Successfully fetched dashboard metrics: {Metrics}",
                    string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value}")));

                totalStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] DashboardMetricsService.GetDashboardMetricsAsync SUCCESS - Total execution time: {totalStopwatch.ElapsedMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Logger.Error(ex, "Error fetching dashboard metrics");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] DashboardMetricsService.GetDashboardMetricsAsync EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Failed after {totalStopwatch.ElapsedMilliseconds}ms");

                // Return empty results in case of error
                result["BusCount"] = 0;
                result["DriverCount"] = 0;
                result["RouteCount"] = 0;
                result["StudentCount"] = 0;
                result["OpenTicketCount"] = 0;

                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _semaphore?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
