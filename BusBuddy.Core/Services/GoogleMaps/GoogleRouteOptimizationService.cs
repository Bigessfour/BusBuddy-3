using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Route Optimization API <c>optimizeTours</c> client.
/// https://developers.google.com/maps/documentation/route-optimization/make-first-request
/// </summary>
public sealed class GoogleRouteOptimizationService : IRouteOptimizationService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GoogleRouteOptimizationService>();

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly bool _ownsHttpClient;
    private readonly IGoogleCloudAccessTokenSource _accessTokens;

    public GoogleRouteOptimizationService(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        bool ownsHttpClient = false)
        : this(httpClient, options, ownsHttpClient, accessTokens: null)
    {
    }

    internal GoogleRouteOptimizationService(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        bool ownsHttpClient,
        IGoogleCloudAccessTokenSource? accessTokens)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
        _accessTokens = accessTokens ?? GoogleCloudPlatformAccessTokenSource.Instance;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GoogleAddressValidationClient.ResolveApiKey(_options))
        || GoogleCloudPlatformAccessTokenSource.CredentialPaths().Any(File.Exists);

    public async Task<OptimizeToursResult> OptimizeToursAsync(
        OptimizeToursProblem problem,
        CancellationToken cancellationToken = default)
    {
        if (problem.Shipments.Count == 0 || problem.Vehicles.Count == 0)
        {
            return OptimizeToursResult.Fail("Need at least one shipment and one vehicle.");
        }

        var accessToken = await _accessTokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return OptimizeToursResult.Fail(
                "Route Optimization needs a Google sign-in on this PC. Run: gcloud auth application-default login");
        }

        var project = string.IsNullOrWhiteSpace(_options.QuotaProject)
            ? GoogleMapsOptions.CanonicalProjectId
            : _options.QuotaProject;
        var uri = new Uri($"https://routeoptimization.googleapis.com/v1/projects/{Uri.EscapeDataString(project)}:optimizeTours");

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(BuildRequestJson(problem), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Logger.Warning(
                    "Route Optimization HTTP {Status}. The bearer token was rejected. ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return OptimizeToursResult.Fail(
                    "Route Optimization rejected the Google sign-in. That account needs roles/routeoptimization.editor on busbuddy-507301.");
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Route Optimization HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return OptimizeToursResult.Fail($"Route Optimization HTTP {(int)response.StatusCode}");
            }

            var parsed = ParseResponse(json, problem);
            Logger.Information(
                "Route Optimization completed Visits={VisitCount} Skipped={Skipped} ElapsedMs={ElapsedMs}",
                parsed.Visits.Count,
                parsed.SkippedShipmentLabels.Count,
                sw.ElapsedMilliseconds);
            return parsed;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Route Optimization request failed");
            return OptimizeToursResult.Fail(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    internal static string BuildRequestJson(OptimizeToursProblem problem)
    {
        var shipments = problem.Shipments.Select(s =>
        {
            var body = new Dictionary<string, object?>
            {
                ["label"] = s.Label,
                ["pickups"] = new[]
                {
                    new
                    {
                        arrivalWaypoint = Waypoint(s.Pickup.Latitude, s.Pickup.Longitude),
                    },
                },
                ["loadDemands"] = new Dictionary<string, object>
                {
                    ["students"] = new { amount = Math.Max(1, s.Load) },
                },
            };
            if (s.Delivery is { } delivery)
            {
                body["deliveries"] = new[]
                {
                    new
                    {
                        arrivalWaypoint = Waypoint(delivery.Latitude, delivery.Longitude),
                    },
                };
            }

            return body;
        }).ToArray();

        var vehicles = problem.Vehicles.Select(v => new Dictionary<string, object?>
        {
            ["label"] = v.Label,
            ["startWaypoint"] = Waypoint(v.StartLatitude, v.StartLongitude),
            ["endWaypoint"] = Waypoint(v.EndLatitude, v.EndLongitude),
            ["costPerHour"] = 1,
            ["costPerKilometer"] = 1,
            ["loadLimits"] = new Dictionary<string, object>
            {
                ["students"] = new { maxLoad = Math.Max(1, v.Capacity) },
            },
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["timeout"] = problem.Timeout,
            ["searchMode"] = "RETURN_FAST",
            ["considerRoadTraffic"] = false,
            ["populatePolylines"] = false,
            ["model"] = new Dictionary<string, object?>
            {
                ["shipments"] = shipments,
                ["vehicles"] = vehicles,
                ["globalStartTime"] = ToRfc3339(problem.GlobalStartUtc),
                ["globalEndTime"] = ToRfc3339(problem.GlobalEndUtc),
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    internal static OptimizeToursResult ParseResponse(string json, OptimizeToursProblem problem)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var shipmentLabels = problem.Shipments.Select(s => s.Label).ToList();
        var vehicleLabels = problem.Vehicles.Select(v => v.Label).ToList();
        var visits = new List<OptimizedVisit>();

        if (root.TryGetProperty("routes", out var routesEl) && routesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var route in routesEl.EnumerateArray())
            {
                var vehicleLabel = ReadString(route, "vehicleLabel");
                if (string.IsNullOrWhiteSpace(vehicleLabel) &&
                    route.TryGetProperty("vehicleIndex", out var vIdx) &&
                    vIdx.TryGetInt32(out var vi) &&
                    vi >= 0 && vi < vehicleLabels.Count)
                {
                    vehicleLabel = vehicleLabels[vi];
                }

                if (!route.TryGetProperty("visits", out var visitsEl) || visitsEl.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var visit in visitsEl.EnumerateArray())
                {
                    var shipmentLabel = ReadString(visit, "shipmentLabel");
                    if (string.IsNullOrWhiteSpace(shipmentLabel) &&
                        visit.TryGetProperty("shipmentIndex", out var sIdx) &&
                        sIdx.TryGetInt32(out var si) &&
                        si >= 0 && si < shipmentLabels.Count)
                    {
                        shipmentLabel = shipmentLabels[si];
                    }

                    if (string.IsNullOrWhiteSpace(shipmentLabel))
                    {
                        continue;
                    }

                    var isPickup = !visit.TryGetProperty("isPickup", out var pickupEl)
                        || pickupEl.ValueKind != JsonValueKind.False;
                    visits.Add(new OptimizedVisit
                    {
                        VehicleLabel = vehicleLabel,
                        ShipmentLabel = shipmentLabel,
                        IsPickup = isPickup,
                    });
                }
            }
        }

        var skipped = new List<string>();
        if (root.TryGetProperty("skippedShipments", out var skippedEl) && skippedEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var skippedItem in skippedEl.EnumerateArray())
            {
                var label = ReadString(skippedItem, "label");
                if (string.IsNullOrWhiteSpace(label) &&
                    skippedItem.TryGetProperty("index", out var idx) &&
                    idx.TryGetInt32(out var i) &&
                    i >= 0 && i < shipmentLabels.Count)
                {
                    label = shipmentLabels[i];
                }

                if (!string.IsNullOrWhiteSpace(label))
                {
                    skipped.Add(label);
                }
            }
        }

        return OptimizeToursResult.Ok(visits, skipped);
    }

    private static object Waypoint(double latitude, double longitude) =>
        new
        {
            location = new
            {
                latLng = new { latitude, longitude },
            },
        };

    private static string ToRfc3339(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
