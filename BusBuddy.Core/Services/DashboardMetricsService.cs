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
            var result = new Dictionary<string, int>();
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Use semaphore to prevent concurrent access issues
            await _semaphore.WaitAsync();

            try
            {
                // Create a new scoped DbContext for this operation to avoid threading issues
                using var scope = _serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<BusBuddy.Core.Data.BusBuddyDbContext>();

                var busStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var busCount = await context.Buses.CountAsync(b => b.Status == "Active");
                busStopwatch.Stop();
                Logger.Debug("Dashboard bus count {BusCount} in {ElapsedMs}ms", busCount, busStopwatch.ElapsedMilliseconds);

                var driverStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var driverCount = await context.Drivers.CountAsync(d => d.Status == "Active");
                driverStopwatch.Stop();
                Logger.Debug("Dashboard driver count {DriverCount} in {ElapsedMs}ms", driverCount, driverStopwatch.ElapsedMilliseconds);

                var routeStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var routeCount = await context.Routes.CountAsync(r => r.IsActive);
                routeStopwatch.Stop();
                Logger.Debug("Dashboard route count {RouteCount} in {ElapsedMs}ms", routeCount, routeStopwatch.ElapsedMilliseconds);

                result["BusCount"] = busCount;
                result["DriverCount"] = driverCount;
                result["RouteCount"] = routeCount;

                result["StudentCount"] = 0;
                result["OpenTicketCount"] = 0;

                totalStopwatch.Stop();
                Logger.Information(
                    "Successfully fetched dashboard metrics BusCount={BusCount} DriverCount={DriverCount} RouteCount={RouteCount} ElapsedMs={ElapsedMs}",
                    busCount, driverCount, routeCount, totalStopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Logger.Error(ex, "Error fetching dashboard metrics after {ElapsedMs}ms", totalStopwatch.ElapsedMilliseconds);

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
