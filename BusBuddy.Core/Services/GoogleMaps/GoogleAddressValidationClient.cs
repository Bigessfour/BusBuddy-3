using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Google Address Validation API client. Implements <see cref="IGeocodingService"/>; never uses hash coordinates.
/// </summary>
public sealed class GoogleAddressValidationClient : IGeocodingService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GoogleAddressValidationClient>();
    private static readonly Uri ValidateUri = new("https://addressvalidation.googleapis.com/v1:validateAddress");

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly bool _ownsHttpClient;

    public GoogleAddressValidationClient(HttpClient httpClient, IOptions<GoogleMapsOptions> options, bool ownsHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
    }

    public string? ResolvedApiKey => ResolveApiKey(_options);

    public async Task<MapsGeocodeResult> ValidateAndGeocodeAsync(
        string? street,
        string? city,
        string? state,
        string? zip,
        CancellationToken cancellationToken = default)
    {
        var key = ResolvedApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Logger.Warning("Address validation skipped — GOOGLE_MAPS_API_KEY not configured");
            return new MapsGeocodeResult
            {
                Ok = false,
                MappingUnconfigured = true,
                ErrorMessage = "Mapping is not configured (missing GOOGLE_MAPS_API_KEY)."
            };
        }

        var line = BuildAddressLine(street, city, state, zip);
        if (string.IsNullOrWhiteSpace(line))
        {
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "Address is required." };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = BuildValidateRequest(key!, line);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var forbidden = ClassifyMapsForbidden(json);
                Logger.Warning(
                    "Address Validation forbidden Kind={Kind} Reason={Reason} — falling back to Geocoding API. ElapsedMs={ElapsedMs}",
                    forbidden.Kind,
                    forbidden.Reason,
                    sw.ElapsedMilliseconds);
                return await GeocodeFallbackAsync(key!, line, forbidden, cancellationToken).ConfigureAwait(false);
            }

            if ((int)response.StatusCode == 429)
            {
                Logger.Warning("Address Validation rate limited ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                using var retryRequest = BuildValidateRequest(key, line);
                using var retry = await _httpClient.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
                var retryJson = await retry.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!retry.IsSuccessStatusCode)
                {
                    return new MapsGeocodeResult
                    {
                        Ok = false,
                        ErrorMessage = "Address validation rate limited — try again later."
                    };
                }

                return ParseValidateResponse(retryJson, sw.ElapsedMilliseconds);
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Address Validation HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return new MapsGeocodeResult
                {
                    Ok = false,
                    ErrorMessage = $"Address validation failed (HTTP {(int)response.StatusCode})."
                };
            }

            return ParseValidateResponse(json, sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException ex)
        {
            Logger.Warning(ex, "Address Validation timed out");
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "Address validation timed out." };
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Address Validation request failed");
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "Address validation failed." };
        }
    }

    public async Task<(double latitude, double longitude)?> GeocodeAsync(
        string? addressLine1,
        string? city,
        string? state,
        string? zip)
    {
        var result = await ValidateAndGeocodeAsync(addressLine1, city, state, zip).ConfigureAwait(false);
        if (!result.Ok || !result.Latitude.HasValue || !result.Longitude.HasValue)
        {
            return null;
        }

        return (result.Latitude.Value, result.Longitude.Value);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    internal static string? ResolveApiKey(GoogleMapsOptions options)
    {
        var env = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        var configured = options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(configured) ||
            configured.StartsWith("${", StringComparison.Ordinal) ||
            configured.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
            configured.Equals("REPLACE_ME", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return configured;
    }

    private static string BuildAddressLine(string? street, string? city, string? state, string? zip)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(street))
        {
            parts.Add(street.Trim());
        }

        var cityState = string.Join(", ", new[] { city?.Trim(), state?.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(cityState))
        {
            parts.Add(cityState!);
        }

        if (!string.IsNullOrWhiteSpace(zip))
        {
            parts.Add(zip.Trim());
        }

        return string.Join(" ", parts);
    }

    private HttpRequestMessage BuildValidateRequest(string key, string line)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ValidateUri);
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
        if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
        {
            request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
        }

        var body = new
        {
            address = new
            {
                regionCode = string.IsNullOrWhiteSpace(_options.RegionCode) ? "US" : _options.RegionCode,
                addressLines = new[] { line }
            },
            enableUspsCass = _options.EnableUspsCass
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return request;
    }

    private static MapsGeocodeResult ParseValidateResponse(string json, long elapsedMs)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("result", out var result))
        {
            Logger.Warning("Address Validation response missing result ElapsedMs={ElapsedMs}", elapsedMs);
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "Address could not be validated." };
        }

        var complete = false;
        var precision = "unknown";
        if (result.TryGetProperty("verdict", out var verdict))
        {
            if (verdict.TryGetProperty("addressComplete", out var ac) && ac.ValueKind == JsonValueKind.True)
            {
                complete = true;
            }

            if (verdict.TryGetProperty("validationGranularity", out var g) && g.ValueKind == JsonValueKind.String)
            {
                precision = g.GetString() ?? precision;
            }
            else if (verdict.TryGetProperty("geocodeGranularity", out var gg) && gg.ValueKind == JsonValueKind.String)
            {
                precision = gg.GetString() ?? precision;
            }
        }

        string? formatted = null;
        if (result.TryGetProperty("address", out var address) &&
            address.TryGetProperty("formattedAddress", out var fa) &&
            fa.ValueKind == JsonValueKind.String)
        {
            formatted = fa.GetString();
        }

        double? lat = null;
        double? lon = null;
        if (result.TryGetProperty("geocode", out var geocode) &&
            geocode.TryGetProperty("location", out var location))
        {
            if (location.TryGetProperty("latitude", out var latEl) && latEl.TryGetDouble(out var latVal))
            {
                lat = latVal;
            }

            if (location.TryGetProperty("longitude", out var lonEl) && lonEl.TryGetDouble(out var lonVal))
            {
                lon = lonVal;
            }
        }

        var deliverable = complete && lat.HasValue && lon.HasValue;
        Logger.Information(
            "Address validated Deliverable={Deliverable} Precision={Precision} ElapsedMs={ElapsedMs}",
            deliverable,
            precision,
            elapsedMs);

        if (!deliverable)
        {
            return new MapsGeocodeResult
            {
                Ok = false,
                FormattedAddress = formatted,
                Precision = precision,
                ErrorMessage = "Address could not be confirmed as deliverable."
            };
        }

        return new MapsGeocodeResult
        {
            Ok = true,
            FormattedAddress = formatted,
            Latitude = lat,
            Longitude = lon,
            Precision = precision
        };
    }

    /// <summary>
    /// Demo / restricted API keys often allow Geocoding but block Address Validation
    /// (<c>API_KEY_SERVICE_BLOCKED</c>). Fall back so clerk Validate Address still geocodes.
    /// HTTP 403 is not treated as "mapping unconfigured" (that flag is missing-key only).
    /// </summary>
    private async Task<MapsGeocodeResult> GeocodeFallbackAsync(
        string key,
        string line,
        MapsForbiddenInfo addressValidationForbidden,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var uri =
                $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(line)}&key={Uri.EscapeDataString(key)}";
            using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Geocoding fallback HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return new MapsGeocodeResult
                {
                    Ok = false,
                    MappingUnconfigured = false,
                    ErrorMessage = DescribeMapsForbidden(addressValidationForbidden)
                };
            }

            return ParseGeocodeJson(json, sw.ElapsedMilliseconds, addressValidationForbidden);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Geocoding fallback failed");
            return new MapsGeocodeResult
            {
                Ok = false,
                MappingUnconfigured = false,
                ErrorMessage = DescribeMapsForbidden(addressValidationForbidden)
            };
        }
    }

    internal enum MapsForbiddenKind
    {
        PermissionDenied,
        ApiNotEnabled,
        QuotaProjectDenied,
        KeyBlocked
    }

    internal readonly struct MapsForbiddenInfo
    {
        public MapsForbiddenKind Kind { get; init; }
        public string? Reason { get; init; }
        public string? Message { get; init; }
    }

    internal static MapsForbiddenInfo ClassifyMapsForbidden(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MapsForbiddenInfo { Kind = MapsForbiddenKind.PermissionDenied };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("error", out var error))
            {
                return ClassifyFromText(json, status: null, reason: null, message: null);
            }

            string? status = null;
            if (error.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String)
            {
                status = statusEl.GetString();
            }

            string? message = null;
            if (error.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String)
            {
                message = messageEl.GetString();
            }

            string? reason = null;
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String)
                    {
                        reason = reasonEl.GetString();
                        if (!string.IsNullOrWhiteSpace(reason))
                        {
                            break;
                        }
                    }
                }
            }

            return ClassifyFromText(json, status, reason, message);
        }
        catch (JsonException)
        {
            return ClassifyFromText(json, status: null, reason: null, message: null);
        }
    }

    private static MapsForbiddenInfo ClassifyFromText(string json, string? status, string? reason, string? message)
    {
        var haystack = $"{status} {reason} {message} {json}";
        var kind = MapsForbiddenKind.PermissionDenied;
        if (ContainsAny(haystack, "SERVICE_DISABLED", "not been used", "is not enabled", "has not been enabled"))
        {
            kind = MapsForbiddenKind.ApiNotEnabled;
        }
        else if (ContainsAny(haystack, "USER_PROJECT_DENIED", "CONSUMER_INVALID", "quota project", "user project", "X-Goog-User-Project"))
        {
            kind = MapsForbiddenKind.QuotaProjectDenied;
        }
        else if (ContainsAny(haystack, "API_KEY_SERVICE_BLOCKED", "API_KEY_INVALID", "API_KEY_HTTP_REFERRER_BLOCKED"))
        {
            kind = MapsForbiddenKind.KeyBlocked;
        }

        return new MapsForbiddenInfo { Kind = kind, Reason = reason, Message = message };
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string DescribeMapsForbidden(MapsForbiddenInfo forbidden)
    {
        return forbidden.Kind switch
        {
            MapsForbiddenKind.ApiNotEnabled =>
                "Address Validation API is not enabled for this API key's Google Cloud project. " +
                "Enable it on busbuddy-507301 — https://developers.google.com/maps/documentation/address-validation",
            MapsForbiddenKind.QuotaProjectDenied =>
                "Maps quota project was rejected (X-Goog-User-Project). Set GCP_BILLING_PROJECT to the project " +
                "that owns GOOGLE_MAPS_API_KEY (busbuddy-507301); do not header Maps traffic to the legacy Coursera project.",
            MapsForbiddenKind.KeyBlocked =>
                "Address Validation is blocked by this API key's restrictions. Enable Address Validation " +
                "(or Geocoding) for the key — https://developers.google.com/maps/get-started",
            _ =>
                "Address Validation returned HTTP 403 (permission denied). Check API enablement, key restrictions, " +
                "and GCP_BILLING_PROJECT=busbuddy-507301 — https://developers.google.com/maps/get-started"
        };
    }

    private static MapsGeocodeResult ParseGeocodeJson(
        string json,
        long elapsedMs,
        MapsForbiddenInfo? addressValidationForbidden = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
            ? st.GetString()
            : null;

        if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            string? apiError = null;
            if (root.TryGetProperty("error_message", out var em) && em.ValueKind == JsonValueKind.String)
            {
                apiError = em.GetString();
            }

            Logger.Warning(
                "Geocoding fallback status={Status} Err={Err} ElapsedMs={ElapsedMs}",
                status,
                apiError,
                elapsedMs);

            var billingHint = !string.IsNullOrWhiteSpace(apiError) &&
                              apiError.Contains("Billing", StringComparison.OrdinalIgnoreCase);
            string errorMessage;
            if (addressValidationForbidden is { } avForbidden &&
                !string.Equals(status, "ZERO_RESULTS", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = DescribeMapsForbidden(avForbidden);
            }
            else
            {
                errorMessage = status switch
                {
                    "ZERO_RESULTS" => "No geocode match for that address.",
                    "REQUEST_DENIED" when billingHint =>
                        "Google Maps requires billing on the Cloud project for this API key. " +
                        "Enable billing on busbuddy-507301: https://console.cloud.google.com/billing — then enable Geocoding / Address Validation " +
                        "(https://developers.google.com/maps/get-started).",
                    "REQUEST_DENIED" =>
                        "Geocoding API denied this key — enable Geocoding (and billing) on busbuddy-507301: " +
                        "https://developers.google.com/maps/get-started",
                    _ => $"Geocoding failed ({status ?? "unknown"})."
                };
            }

            return new MapsGeocodeResult
            {
                Ok = false,
                MappingUnconfigured = false,
                ErrorMessage = errorMessage
            };
        }

        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "No geocode match for that address." };
        }

        var first = results[0];
        string? formatted = null;
        if (first.TryGetProperty("formatted_address", out var fa) && fa.ValueKind == JsonValueKind.String)
        {
            formatted = fa.GetString();
        }

        double? lat = null;
        double? lon = null;
        if (first.TryGetProperty("geometry", out var geometry) &&
            geometry.TryGetProperty("location", out var location))
        {
            if (location.TryGetProperty("lat", out var latEl) && latEl.TryGetDouble(out var latVal))
            {
                lat = latVal;
            }

            if (location.TryGetProperty("lng", out var lonEl) && lonEl.TryGetDouble(out var lonVal))
            {
                lon = lonVal;
            }
        }

        if (!lat.HasValue || !lon.HasValue)
        {
            return new MapsGeocodeResult
            {
                Ok = false,
                FormattedAddress = formatted,
                ErrorMessage = "Geocode response missing coordinates."
            };
        }

        Logger.Information(
            "Geocoding fallback OK Precision=geocode ElapsedMs={ElapsedMs}",
            elapsedMs);

        return new MapsGeocodeResult
        {
            Ok = true,
            FormattedAddress = formatted,
            Latitude = lat,
            Longitude = lon,
            Precision = "geocode"
        };
    }
}
