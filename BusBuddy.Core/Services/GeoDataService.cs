using System.Diagnostics;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Database-backed route geography for SfMap. Earth Engine is not used.
    /// Street geocoding/routing is Google Maps Platform (spec 007).
    /// </summary>
    public class GeoDataService : IGeoDataService
    {
        private static readonly ILogger Logger = Log.ForContext<GeoDataService>();
        private readonly IBusBuddyDbContextFactory? _contextFactory;

        public GeoDataService(IBusBuddyDbContextFactory? contextFactory = null)
        {
            _contextFactory = contextFactory;
            Logger.Information("GeoDataService constructed HasDbContext={HasDbContext}", contextFactory is not null);
        }

        public async Task<List<Route>> GetRoutesWithGeoDataAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            if (_contextFactory is null)
            {
                Logger.Warning("GetRoutesWithGeoDataAsync skipped — no DbContext factory");
                return [];
            }

            Logger.Information("Loading active routes with geo data from database");
            using var context = _contextFactory.CreateDbContext();
            var routes = await context.Routes.AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RouteName)
                .ToListAsync();

            if (routes.Count == 0)
            {
                stopwatch.Stop();
                Logger.Information("No active routes found ElapsedMs={ElapsedMs}", stopwatch.ElapsedMilliseconds);
                return routes;
            }

            var routeIds = routes.Select(r => r.RouteId).ToList();
            var stops = await context.RouteStops.AsNoTracking()
                .Where(s => routeIds.Contains(s.RouteId) && s.Latitude != null && s.Longitude != null)
                .OrderBy(s => s.RouteId)
                .ThenBy(s => s.StopOrder)
                .ToListAsync();

            var derived = new List<(int RouteId, string Json)>();
            foreach (var route in routes)
            {
                if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    continue;
                }

                var pts = stops
                    .Where(s => s.RouteId == route.RouteId)
                    .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value));
                var json = RouteWaypointSerializer.FromPairs(pts);
                if (json != "[]")
                {
                    route.WaypointsJson = json;
                    derived.Add((route.RouteId, json));
                }
            }

            await PersistDerivedWaypointsAsync(derived).ConfigureAwait(false);

            stopwatch.Stop();
            Logger.Information(
                "Loaded routes with geo data Routes={RouteCount} StopsWithCoords={StopCount} DerivedWaypoints={Derived} ElapsedMs={ElapsedMs}",
                routes.Count, stops.Count, derived.Count, stopwatch.ElapsedMilliseconds);

            return routes;
        }

        public async Task<Route?> GetRouteGeoDataAsync(int routeId)
        {
            Logger.Information("Loading geo data for route {RouteId}", routeId);
            if (_contextFactory is null)
            {
                Logger.Warning("GetRouteGeoDataAsync skipped — no DbContext factory RouteId={RouteId}", routeId);
                return null;
            }

            using var context = _contextFactory.CreateDbContext();
            var route = await context.Routes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RouteId == routeId);
            if (route is null)
            {
                Logger.Warning("Route {RouteId} not found for geo data", routeId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(route.WaypointsJson))
            {
                var stops = await context.RouteStops.AsNoTracking()
                    .Where(s => s.RouteId == routeId && s.Latitude != null && s.Longitude != null)
                    .OrderBy(s => s.StopOrder)
                    .ToListAsync();
                var json = RouteWaypointSerializer.FromPairs(
                    stops.Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value)));
                if (json != "[]")
                {
                    route.WaypointsJson = json;
                    await PersistDerivedWaypointsAsync([(route.RouteId, json)]).ConfigureAwait(false);
                    Logger.Information(
                        "Derived and persisted {StopCount} waypoints for route {RouteId} {RouteName}",
                        stops.Count,
                        routeId,
                        route.RouteName);
                }
                else
                {
                    Logger.Information("Route {RouteId} {RouteName} has no stored or stop-derived waypoints", routeId, route.RouteName);
                }
            }
            else
            {
                Logger.Information("Route {RouteId} {RouteName} already has WaypointsJson", routeId, route.RouteName);
            }

            return route;
        }

        private async Task PersistDerivedWaypointsAsync(IReadOnlyList<(int RouteId, string Json)> derived)
        {
            if (derived.Count == 0 || _contextFactory is null)
            {
                return;
            }

            try
            {
                await using var write = _contextFactory.CreateWriteDbContext();
                var ids = derived.Select(d => d.RouteId).ToList();
                var tracked = await write.Routes.Where(r => ids.Contains(r.RouteId)).ToListAsync();
                var byId = derived.ToDictionary(d => d.RouteId, d => d.Json);
                var updated = 0;
                foreach (var route in tracked)
                {
                    if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
                    {
                        // Drive-path / stored geometry wins — never overwrite.
                        continue;
                    }

                    if (!byId.TryGetValue(route.RouteId, out var json))
                    {
                        continue;
                    }

                    route.WaypointsJson = json;
                    updated++;
                }

                if (updated > 0)
                {
                    await write.SaveChangesAsync();
                }

                Logger.Information("Persisted derived WaypointsJson Routes={Updated}", updated);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Persisting derived waypoints failed — in-memory JSON kept");
            }
        }
    }
}
