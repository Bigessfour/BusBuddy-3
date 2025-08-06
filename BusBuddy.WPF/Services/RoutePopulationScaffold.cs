using System.Threading.Tasks;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using System.Collections.Generic;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Serilog;

namespace BusBuddy.WPF.Services
{
    public class RoutePopulationScaffold : IRoutePopulationScaffold
    {
        private readonly BusBuddy.Core.Services.IRouteService _routeService;
        private static readonly ILogger Logger = Log.ForContext<RoutePopulationScaffold>();
        private static readonly object _cacheLock = new object();
        private static List<BusBuddy.Core.Models.Route>? _cachedRoutes;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // Cache for 5 minutes

        public RoutePopulationScaffold(BusBuddy.Core.Services.IRouteService routeService)
        {
            _routeService = routeService;
        }

        public async Task<System.Collections.Generic.List<BusBuddy.Core.Models.Route>> GetOptimizedRoutesAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Logger.Debug("RoutePopulationScaffold.GetOptimizedRoutesAsync START");

            try
            {
                // Check cache first for performance optimization
                lock (_cacheLock)
                {
                    if (_cachedRoutes != null && DateTime.Now - _lastCacheUpdate < _cacheExpiry)
                    {
                        Logger.Debug("Returning cached routes ({Count} routes) in {ElapsedMs}ms",
                            _cachedRoutes.Count, stopwatch.ElapsedMilliseconds);
                        return new List<BusBuddy.Core.Models.Route>(_cachedRoutes);
                    }
                }

                // Cache miss or expired - fetch from service
                var result = await _routeService.GetAllActiveRoutesAsync();
                var routeList = result.IsSuccess && result.Value != null
                    ? result.Value.ToList()
                    : new List<BusBuddy.Core.Models.Route>();

                // Update cache
                lock (_cacheLock)
                {
                    _cachedRoutes = new List<BusBuddy.Core.Models.Route>(routeList);
                    _lastCacheUpdate = DateTime.Now;
                }

                stopwatch.Stop();
                Logger.Information("Retrieved {Count} routes from service in {ElapsedMs}ms",
                    routeList.Count, stopwatch.ElapsedMilliseconds);

                return routeList;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in GetOptimizedRoutesAsync after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return new List<BusBuddy.Core.Models.Route>();
            }
        }

        public async Task PopulateRoutesAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Logger.Debug("RoutePopulationScaffold.PopulateRoutesAsync START");

            try
            {
                // Use the optimized route retrieval with caching
                var routes = await GetOptimizedRoutesAsync();

                stopwatch.Stop();
                Logger.Information("PopulateRoutesAsync completed with {Count} routes in {ElapsedMs}ms",
                    routes.Count, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Error in PopulateRoutesAsync after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task PopulateRouteMetadataAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Logger.Debug("RoutePopulationScaffold.PopulateRouteMetadataAsync START");

            try
            {
                // OPTIMIZATION: For startup performance, just ensure cache is warmed up
                // without doing heavy processing. The actual route data will be loaded
                // on-demand when needed by the UI components.

                // Check if we have cached data, if not, do a lightweight load
                bool needsRefresh;
                lock (_cacheLock)
                {
                    needsRefresh = _cachedRoutes == null || DateTime.Now - _lastCacheUpdate >= _cacheExpiry;
                }

                if (needsRefresh)
                {
                    Logger.Debug("Cache miss - warming up route cache");
                    await GetOptimizedRoutesAsync();
                }
                else
                {
                    Logger.Debug("Using existing cached route data");
                }

                stopwatch.Stop();
                Logger.Information("PopulateRouteMetadataAsync completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Error in PopulateRouteMetadataAsync after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                // Don't throw to avoid startup failures
            }
        }
    }
}
