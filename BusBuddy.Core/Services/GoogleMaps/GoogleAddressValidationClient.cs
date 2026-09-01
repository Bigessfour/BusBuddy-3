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
                Logger.Warning(
                    "Address Validation forbidden (API not enabled for key?) — falling back to Geocoding API. ElapsedMs={ElapsedMs}",
                    sw.ElapsedMilliseconds);
                return await GeocodeFallbackAsync(key!, line, cancellationToken).ConfigureAwait(false);
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
    /// </summary>
    private async Task<MapsGeocodeResult> GeocodeFallbackAsync(
        string key,
        string line,
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
                    MappingUnconfigured = true,
                    ErrorMessage =
                        "Address Validation is blocked for this API key, and Geocoding fallback also failed. " +
                        "Enable Address Validation API (or Geocoding API) in Google Cloud — see https://developers.google.com/maps/get-started"
                };
            }

            return ParseGeocodeJson(json, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Geocoding fallback failed");
            return new MapsGeocodeResult
            {
                Ok = false,
                MappingUnconfigured = true,
                ErrorMessage =
                    "Maps Address Validation is blocked for this key. Enable it in Cloud Console, or ensure Geocoding API works."
            };
        }
    }

    private static MapsGeocodeResult ParseGeocodeJson(string json, long elapsedMs)
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
            return new MapsGeocodeResult
            {
                Ok = false,
                MappingUnconfigured = billingHint ||
                                      string.Equals(status, "REQUEST_DENIED", StringComparison.OrdinalIgnoreCase),
                ErrorMessage = status switch
                {
                    "ZERO_RESULTS" => "No geocode match for that address.",
                    "REQUEST_DENIED" when billingHint =>
                        "Google Maps requires billing on the Cloud project for this API key. " +
                        "Enable billing: https://console.cloud.google.com/billing — then enable Geocoding / Address Validation " +
                        "(https://developers.google.com/maps/get-started).",
                    "REQUEST_DENIED" =>
                        "Geocoding API denied this key — enable Geocoding (and billing) in Google Cloud: " +
                        "https://developers.google.com/maps/get-started",
                    _ => $"Geocoding failed ({status ?? "unknown"})."
                }
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
