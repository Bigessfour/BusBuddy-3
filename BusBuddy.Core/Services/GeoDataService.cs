using System.Diagnostics;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Database-backed route geography for SfMap. Earth Engine is not used.
    /// Street geocoding/routing will use Google Maps Platform (spec 007) when resumed.
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
                var sample = SampleRoutes();
                Logger.Warning("GetRoutesWithGeoDataAsync using sample routes — no DbContext factory (Count={Count})", sample.Count);
                return sample;
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

            var derivedWaypoints = 0;
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
                    derivedWaypoints++;
                }
            }

            stopwatch.Stop();
            Logger.Information(
                "Loaded routes with geo data Routes={RouteCount} StopsWithCoords={StopCount} DerivedWaypoints={Derived} ElapsedMs={ElapsedMs}",
                routes.Count, stops.Count, derivedWaypoints, stopwatch.ElapsedMilliseconds);

            return routes;
        }

        public async Task<Route?> GetRouteGeoDataAsync(int routeId)
        {
            Logger.Information("Loading geo data for route {RouteId}", routeId);
            if (_contextFactory is null)
            {
                var sample = SampleRoutes().FirstOrDefault(r => r.RouteId == routeId);
                Logger.Warning("GetRouteGeoDataAsync using sample route {RouteId} Found={Found}", routeId, sample is not null);
                return sample;
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
                    Logger.Information("Derived {StopCount} waypoints for route {RouteId} {RouteName}", stops.Count, routeId, route.RouteName);
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

        private static List<Route> SampleRoutes() =>
        [
            new Route
            {
                RouteId = 1,
                RouteName = "Route 1 - Elementary",
                Description = "Elementary school morning route",
                Date = DateTime.Today,
                IsActive = true,
                School = "Wiley School RE-13JT",
                WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
                {
                    (WileyMapDefaults.SchoolLatitude, WileyMapDefaults.SchoolLongitude),
                    (38.1600, -102.7000)
                })
            }
        ];
    }
}
