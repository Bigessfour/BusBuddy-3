using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Google Routes API <c>computeRoutes</c> client.</summary>
public sealed class GoogleRoutingService : IRoutingService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GoogleRoutingService>();
    private static readonly Uri ComputeRoutesUri = new("https://routes.googleapis.com/directions/v2:computeRoutes");
    private static readonly Uri ComputeRouteMatrixUri = new("https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix");
    private const string FieldMask = "routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline";
    private const string MatrixFieldMask =
        "originIndex,destinationIndex,duration,distanceMeters,condition,status";

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly bool _ownsHttpClient;

    public GoogleRoutingService(HttpClient httpClient, IOptions<GoogleMapsOptions> options, bool ownsHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<DrivePathResult> ComputeDrivePathAsync(
        (double Latitude, double Longitude) origin,
        (double Latitude, double Longitude) destination,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints,
        CancellationToken cancellationToken = default)
    {
        var key = GoogleAddressValidationClient.ResolveApiKey(_options);
        if (string.IsNullOrWhiteSpace(key))
        {
            Logger.Warning("Drive path skipped — GOOGLE_MAPS_API_KEY not configured");
            return new DrivePathResult { Error = "Mapping is not configured." };
        }

        var stopCount = 2 + (waypoints?.Count ?? 0);
        if (stopCount < 2)
        {
            Logger.Information("Drive path skipped — fewer than 2 points");
            return new DrivePathResult { Error = "Need at least origin and destination." };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var intermediates = (waypoints ?? Array.Empty<(double, double)>())
                .Select(w => new
                {
                    location = new
                    {
                        latLng = new { latitude = w.Latitude, longitude = w.Longitude }
                    }
                })
                .ToArray();

            var bodyJson = JsonSerializer.Serialize(new
            {
                origin = new { location = new { latLng = new { latitude = origin.Latitude, longitude = origin.Longitude } } },
                destination = new { location = new { latLng = new { latitude = destination.Latitude, longitude = destination.Longitude } } },
                intermediates,
                travelMode = "DRIVE",
                routingPreference = "TRAFFIC_UNAWARE"
            });

            var (status, json) = await PostRoutesAsync(
                    ComputeRoutesUri,
                    key,
                    FieldMask,
                    bodyJson,
                    includeQuotaProject: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IsSuccess(status)
                && !string.IsNullOrWhiteSpace(_options.QuotaProject)
                && GoogleAddressValidationClient.ClassifyMapsForbidden(json).Kind
                    == GoogleAddressValidationClient.MapsForbiddenKind.QuotaProjectDenied)
            {
                Logger.Warning(
                    "Routes API quota project denied ({QuotaProject}) — retrying without X-Goog-User-Project",
                    _options.QuotaProject);
                (status, json) = await PostRoutesAsync(
                        ComputeRoutesUri,
                        key,
                        FieldMask,
                        bodyJson,
                        includeQuotaProject: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            sw.Stop();

            if (status == HttpStatusCode.Forbidden || !IsSuccess(status))
            {
                Logger.Warning(
                    "Routes API HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)status,
                    sw.ElapsedMilliseconds);
                return new DrivePathResult { Error = $"Routes API failed (HTTP {(int)status})." };
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("routes", out var routes) ||
                routes.ValueKind != JsonValueKind.Array ||
                routes.GetArrayLength() == 0)
            {
                return new DrivePathResult { Error = "No route returned." };
            }

            var route = routes[0];
            int? distance = null;
            if (route.TryGetProperty("distanceMeters", out var dm) && dm.TryGetInt32(out var meters))
            {
                distance = meters;
            }

            string? duration = null;
            if (route.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.String)
            {
                duration = dur.GetString();
            }

            string? encoded = null;
            if (route.TryGetProperty("polyline", out var poly) &&
                poly.TryGetProperty("encodedPolyline", out var enc) &&
                enc.ValueKind == JsonValueKind.String)
            {
                encoded = enc.GetString();
            }

            if (string.IsNullOrWhiteSpace(encoded))
            {
                return new DrivePathResult { Error = "Empty polyline." };
            }

            var points = EncodedPolylineCodec.Decode(encoded);
            Logger.Information(
                "Drive path computed Stops={StopCount} DistanceMeters={M} ElapsedMs={ElapsedMs}",
                stopCount,
                distance,
                sw.ElapsedMilliseconds);

            return new DrivePathResult
            {
                EncodedPolyline = encoded,
                Points = points,
                DistanceMeters = distance,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Routes computeRoutes failed");
            return new DrivePathResult { Error = "Routing request failed." };
        }
    }

    public async Task<IReadOnlyList<RouteMatrixElement>> ComputeRouteMatrixAsync(
        (double Latitude, double Longitude) origin,
        IReadOnlyList<(double Latitude, double Longitude)> destinations,
        CancellationToken cancellationToken = default)
    {
        var key = GoogleAddressValidationClient.ResolveApiKey(_options);
        if (string.IsNullOrWhiteSpace(key))
        {
            Logger.Warning("Route matrix skipped — GOOGLE_MAPS_API_KEY not configured");
            return Array.Empty<RouteMatrixElement>();
        }

        if (destinations is null || destinations.Count == 0)
        {
            return Array.Empty<RouteMatrixElement>();
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var bodyJson = JsonSerializer.Serialize(new
            {
                origins = new[]
                {
                    new { waypoint = new { location = new { latLng = new { latitude = origin.Latitude, longitude = origin.Longitude } } } }
                },
                destinations = destinations.Select(d => new
                {
                    waypoint = new { location = new { latLng = new { latitude = d.Latitude, longitude = d.Longitude } } }
                }).ToArray(),
                travelMode = "DRIVE",
                routingPreference = "TRAFFIC_UNAWARE"
            });

            var (status, json) = await PostRoutesAsync(
                    ComputeRouteMatrixUri,
                    key,
                    MatrixFieldMask,
                    bodyJson,
                    includeQuotaProject: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IsSuccess(status)
                && !string.IsNullOrWhiteSpace(_options.QuotaProject)
                && GoogleAddressValidationClient.ClassifyMapsForbidden(json).Kind
                    == GoogleAddressValidationClient.MapsForbiddenKind.QuotaProjectDenied)
            {
                Logger.Warning(
                    "Route matrix quota project denied ({QuotaProject}) — retrying without X-Goog-User-Project",
                    _options.QuotaProject);
                (status, json) = await PostRoutesAsync(
                        ComputeRouteMatrixUri,
                        key,
                        MatrixFieldMask,
                        bodyJson,
                        includeQuotaProject: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            sw.Stop();

            if (!IsSuccess(status))
            {
                Logger.Warning(
                    "Route matrix HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)status,
                    sw.ElapsedMilliseconds);
                return Array.Empty<RouteMatrixElement>();
            }

            using var doc = JsonDocument.Parse(json);
            // REST computeRouteMatrix returns a JSON array of elements (not { "elements": [...] }).
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                Logger.Warning("Route matrix response was not a JSON array");
                return Array.Empty<RouteMatrixElement>();
            }

            var list = new List<RouteMatrixElement>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                int destIndex = el.TryGetProperty("destinationIndex", out var di) && di.TryGetInt32(out var idx)
                    ? idx
                    : list.Count;
                int? distance = null;
                if (el.TryGetProperty("distanceMeters", out var dm) && dm.TryGetInt32(out var meters))
                {
                    distance = meters;
                }

                string? duration = null;
                if (el.TryGetProperty("duration", out var dur) && dur.ValueKind == JsonValueKind.String)
                {
                    duration = dur.GetString();
                }

                string? condition = null;
                if (el.TryGetProperty("condition", out var cond) && cond.ValueKind == JsonValueKind.String)
                {
                    condition = cond.GetString();
                }

                var routeExists = string.Equals(condition, "ROUTE_EXISTS", StringComparison.OrdinalIgnoreCase);
                list.Add(new RouteMatrixElement
                {
                    DestinationIndex = destIndex,
                    DistanceMeters = distance,
                    Duration = duration,
                    Error = routeExists ? null : (condition ?? "ROUTE_NOT_FOUND"),
                });
            }

            Logger.Information(
                "Route matrix computed Destinations={Count} ElapsedMs={ElapsedMs}",
                list.Count,
                sw.ElapsedMilliseconds);
            return list;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Route matrix request failed");
            return Array.Empty<RouteMatrixElement>();
        }
    }

    private async Task<(HttpStatusCode Status, string Json)> PostRoutesAsync(
        Uri uri,
        string key,
        string fieldMask,
        string bodyJson,
        bool includeQuotaProject,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", fieldMask);
        if (includeQuotaProject && !string.IsNullOrWhiteSpace(_options.QuotaProject))
        {
            request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
        }

        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, json);
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and <= 299;

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
