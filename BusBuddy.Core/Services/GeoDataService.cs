using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services
{
    public class GeoDataService : IGeoDataService, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<GeoDataService>();
        private readonly HttpClient _httpClient;
        private readonly string _geeApiBaseUrl;
        private readonly string _geeAccessToken;
        private readonly IBusBuddyDbContextFactory? _contextFactory;
        private string? _cachedToken;
        private DateTime _tokenExpiresUtc;
        private bool _disposed;

        public GeoDataService(string geeApiBaseUrl, string geeAccessToken, IBusBuddyDbContextFactory? contextFactory = null)
        {
            _httpClient = new HttpClient();
            _geeApiBaseUrl = geeApiBaseUrl;
            _geeAccessToken = geeAccessToken;
            _contextFactory = contextFactory;
            var tokenKind = string.IsNullOrWhiteSpace(geeAccessToken) || geeAccessToken == "placeholder_token"
                ? "placeholder"
                : "live";
            Logger.Information(
                "GeoDataService constructed BaseUrl={BaseUrl} TokenKind={TokenKind} HasDbContext={HasDbContext}",
                geeApiBaseUrl, tokenKind, contextFactory is not null);
        }

        public async Task<string> GetGeoJsonAsync(string assetId)
        {
            var stopwatch = Stopwatch.StartNew();
            var url = $"{_geeApiBaseUrl}/v1beta/projects/earthengine-public/assets/{assetId}:exportGeoJson";
            Logger.Information("Requesting GEE GeoJSON for asset {AssetId}", assetId);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await ResolveAccessTokenAsync());
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "GEE GeoJSON request failed AssetId={AssetId} Status={StatusCode} ElapsedMs={ElapsedMs} Bytes={Bytes}",
                    assetId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, body.Length);
            }
            else
            {
                Logger.Information(
                    "GEE GeoJSON received AssetId={AssetId} Status={StatusCode} ElapsedMs={ElapsedMs} Bytes={Bytes}",
                    assetId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, body.Length);
            }

            response.EnsureSuccessStatusCode();
            return body;
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

        private async Task<string> ResolveAccessTokenAsync()
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTime.UtcNow < _tokenExpiresUtc)
            {
                return _cachedToken;
            }

            var refreshed = await GcpCredentialBootstrap.TryGetEarthEngineAccessTokenAsync();
            var token = !string.IsNullOrWhiteSpace(refreshed)
                ? refreshed
                : _geeAccessToken;
            _cachedToken = token;
            _tokenExpiresUtc = DateTime.UtcNow.AddMinutes(50);
            Logger.Debug("Resolved GEE access token TokenKind={TokenKind} ExpiresUtc={ExpiresUtc:o}",
                string.IsNullOrWhiteSpace(token) || token == "placeholder_token" ? "placeholder" : "live",
                _tokenExpiresUtc);
            return token ?? string.Empty;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _httpClient.Dispose();
                _disposed = true;
                Logger.Debug("GeoDataService disposed");
            }
        }
    }
}
