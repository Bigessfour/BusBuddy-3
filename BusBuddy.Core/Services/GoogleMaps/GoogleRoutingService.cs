using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Google Routes API <c>computeRoutes</c> client.</summary>
public sealed class GoogleRoutingService : IRoutingService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GoogleRoutingService>();
    private static readonly Uri ComputeRoutesUri = new("https://routes.googleapis.com/directions/v2:computeRoutes");
    private const string FieldMask = "routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline";

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
            using var request = new HttpRequestMessage(HttpMethod.Post, ComputeRoutesUri);
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", FieldMask);
            if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
            }

            var intermediates = (waypoints ?? Array.Empty<(double, double)>())
                .Select(w => new
                {
                    location = new
                    {
                        latLng = new { latitude = w.Latitude, longitude = w.Longitude }
                    }
                })
                .ToArray();

            var body = new
            {
                origin = new { location = new { latLng = new { latitude = origin.Latitude, longitude = origin.Longitude } } },
                destination = new { location = new { latLng = new { latitude = destination.Latitude, longitude = destination.Longitude } } },
                intermediates,
                travelMode = "DRIVE",
                routingPreference = "TRAFFIC_UNAWARE"
            };
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (response.StatusCode == HttpStatusCode.Forbidden || !response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Routes API HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return new DrivePathResult { Error = $"Routes API failed (HTTP {(int)response.StatusCode})." };
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

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

/// <summary>Google encoded polyline codec (precision 1e-5).</summary>
public static class EncodedPolylineCodec
{
    public static IReadOnlyList<(double Latitude, double Longitude)> Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return Array.Empty<(double, double)>();
        }

        var points = new List<(double, double)>();
        int index = 0;
        int lat = 0;
        int lng = 0;

        while (index < encoded.Length)
        {
            lat += DecodeNext(encoded, ref index);
            lng += DecodeNext(encoded, ref index);
            points.Add((lat / 1e5, lng / 1e5));
        }

        return points;
    }

    private static int DecodeNext(string encoded, ref int index)
    {
        int result = 0;
        int shift = 0;
        int b;
        do
        {
            b = encoded[index++] - 63;
            result |= (b & 0x1f) << shift;
            shift += 5;
        }
        while (b >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
