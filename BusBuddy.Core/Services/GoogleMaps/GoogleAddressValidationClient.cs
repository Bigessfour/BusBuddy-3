using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;
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
        string? possibleNextAction = null;
        var validationGranularity = "unknown";
        var geocodeGranularity = "unknown";
        if (result.TryGetProperty("verdict", out var verdict))
        {
            if (verdict.TryGetProperty("addressComplete", out var ac) && ac.ValueKind == JsonValueKind.True)
            {
                complete = true;
            }

            possibleNextAction = ReadString(verdict, "possibleNextAction");
            validationGranularity = ReadString(verdict, "validationGranularity") ?? validationGranularity;
            geocodeGranularity = ReadString(verdict, "geocodeGranularity") ?? geocodeGranularity;
        }

        string? formatted = null;
        List<string>? missingTypes = null;
        if (result.TryGetProperty("address", out var address))
        {
            formatted = ReadString(address, "formattedAddress");
            missingTypes = ReadStringArray(address, "missingComponentTypes");
        }

        string? street = null;
        string? city = null;
        string? state = null;
        string? zip = null;
        if (result.TryGetProperty("address", out address)
            && address.TryGetProperty("postalAddress", out var postal)
            && postal.ValueKind == JsonValueKind.Object)
        {
            street = ReadFirstAddressLine(postal);
            city = ReadString(postal, "locality");
            state = ReadString(postal, "administrativeArea");
            zip = ReadString(postal, "postalCode");
        }

        double? lat = null;
        double? lon = null;
        string? placeId = null;
        List<string>? placeTypes = null;
        if (result.TryGetProperty("geocode", out var geocode))
        {
            placeId = ReadString(geocode, "placeId");
            placeTypes = ReadStringArray(geocode, "placeTypes");
            if (geocode.TryGetProperty("location", out var location))
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
        }

        var dpv = ReadUspsDpv(result);
        var poBox = ReadMetadataPoBox(result);
        var unconfirmedTypes = result.TryGetProperty("address", out address)
            ? ReadStringArray(address, "unconfirmedComponentTypes")
            : null;
        var accepted = AddressValidationPinPolicy.TryAcceptAddressValidationPin(
            possibleNextAction,
            validationGranularity,
            geocodeGranularity,
            complete,
            dpv,
            missingTypes,
            poBox,
            unconfirmedTypes,
            placeTypes,
            out var rejectReason);

        Logger.Information(
            "Address validated Complete={Complete} NextAction={NextAction} ValidationGranularity={ValidationGranularity} GeocodeGranularity={GeocodeGranularity} Dpv={Dpv} Accepted={Accepted} ElapsedMs={ElapsedMs}",
            complete,
            possibleNextAction,
            validationGranularity,
            geocodeGranularity,
            dpv,
            accepted,
            elapsedMs);

        if (!accepted || !lat.HasValue || !lon.HasValue)
        {
            return new MapsGeocodeResult
            {
                Ok = false,
                FormattedAddress = formatted,
                PlaceId = placeId,
                Precision = geocodeGranularity,
                ValidationGranularity = validationGranularity,
                GeocodeGranularity = geocodeGranularity,
                PossibleNextAction = possibleNextAction,
                DpvConfirmation = dpv,
                Street = street,
                City = city,
                State = state,
                Zip = zip,
                ErrorMessage = string.IsNullOrWhiteSpace(rejectReason)
                    ? "Address could not be confirmed as deliverable."
                    : rejectReason
            };
        }

        return new MapsGeocodeResult
        {
            Ok = true,
            FormattedAddress = formatted,
            Latitude = lat,
            Longitude = lon,
            PlaceId = placeId,
            Precision = geocodeGranularity,
            ValidationGranularity = validationGranularity,
            GeocodeGranularity = geocodeGranularity,
            PossibleNextAction = possibleNextAction,
            DpvConfirmation = dpv,
            Street = street,
            City = city,
            State = state,
            Zip = zip
        };
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
        {
            return el.GetString();
        }

        return null;
    }

    private static List<string>? ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value);
                }
            }
        }

        return list.Count == 0 ? null : list;
    }

    private static string? ReadFirstAddressLine(JsonElement postal)
    {
        if (!postal.TryGetProperty("addressLines", out var lines) || lines.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var line in lines.EnumerateArray())
        {
            if (line.ValueKind == JsonValueKind.String)
            {
                var value = line.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ReadUspsDpv(JsonElement result)
    {
        if (!result.TryGetProperty("uspsData", out var usps) || usps.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(usps, "dpvConfirmation");
    }

    private static bool ReadMetadataPoBox(JsonElement result)
    {
        if (!result.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return metadata.TryGetProperty("poBox", out var poBox) && poBox.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Geocoding API v4 forward-geocode request for one unstructured address line.
    /// Docs: https://developers.google.com/maps/documentation/geocoding/geocoding — the API key and
    /// response field mask travel as <c>X-Goog-Api-Key</c> / <c>X-Goog-FieldMask</c> headers (documented for v4;
    /// the legacy <c>maps/api/geocode/json</c> endpoint only documents <c>?key=</c>, which would put the key in URL logs).
    /// </summary>
    internal static Uri BuildGeocodeV4Uri(string line, string? regionCode)
    {
        var region = string.IsNullOrWhiteSpace(regionCode) ? "US" : regionCode.Trim();
        return new Uri(
            "https://geocode.googleapis.com/v4/geocode/address/"
            + Uri.EscapeDataString(line)
            + "?regionCode=" + Uri.EscapeDataString(region));
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
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildGeocodeV4Uri(line, _options.RegionCode));
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", GeocodeV4FieldMask);
            if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var geocodeForbidden = ClassifyMapsForbidden(json);
                Logger.Warning(
                    "Geocoding v4 fallback HTTP {Status} Kind={Kind} Reason={Reason} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    geocodeForbidden.Kind,
                    geocodeForbidden.Reason,
                    sw.ElapsedMilliseconds);
                return new MapsGeocodeResult
                {
                    Ok = false,
                    MappingUnconfigured = false,
                    ErrorMessage = DescribeGeocodeFailure(response.StatusCode, geocodeForbidden, addressValidationForbidden)
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
                MappingUnconfigured = false,
                ErrorMessage = DescribeMapsForbidden(addressValidationForbidden)
            };
        }
    }

    /// <summary>
    /// Geocoding API v4 reverse geocode. Docs:
    /// <see href="https://developers.google.com/maps/documentation/geocoding/reference/rest/v4/geocode.location/geocodeLocation"/>.
    /// Used to suggest a catalog-stop name after a map click — does not move the pin.
    /// </summary>
    public async Task<MapsGeocodeResult> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var key = ResolvedApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return new MapsGeocodeResult
            {
                Ok = false,
                MappingUnconfigured = true,
                ErrorMessage = "Mapping is not configured (missing GOOGLE_MAPS_API_KEY)."
            };
        }

        if (!LocationCoordinate.IsValidated(latitude, longitude))
        {
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "Pin is not a validated coordinate." };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildReverseGeocodeV4Uri(latitude, longitude, _options.RegionCode));
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", GeocodeV4FieldMask);
            if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var forbidden = ClassifyMapsForbidden(json);
                Logger.Warning(
                    "Reverse geocode v4 HTTP {Status} Kind={Kind} Reason={Reason} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    forbidden.Kind,
                    forbidden.Reason,
                    sw.ElapsedMilliseconds);
                return new MapsGeocodeResult
                {
                    Ok = false,
                    ErrorMessage = DescribeGeocodeFailure(response.StatusCode, forbidden, forbidden)
                };
            }

            return ParseGeocodeJson(json, sw.ElapsedMilliseconds, requirePlotPrecision: false);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Reverse geocode failed");
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "Could not name that pin from Google." };
        }
    }

    /// <summary>GET <c>/v4/geocode/location?locationQuery=lat,lng</c>.</summary>
    internal static Uri BuildReverseGeocodeV4Uri(double latitude, double longitude, string? regionCode)
    {
        var region = string.IsNullOrWhiteSpace(regionCode) ? "US" : regionCode.Trim();
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"{latitude},{longitude}");
        return new Uri(
            "https://geocode.googleapis.com/v4/geocode/location?locationQuery="
            + Uri.EscapeDataString(query)
            + "&regionCode=" + Uri.EscapeDataString(region));
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
        else if (ContainsAny(
                     haystack,
                     "USER_PROJECT_DENIED",
                     "CONSUMER_INVALID",
                     "quota project",
                     "user project",
                     "X-Goog-User-Project",
                     "serviceUsageConsumer",
                     "serviceusage.services.use",
                     "required permission to use project"))
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
                "Maps quota project was rejected (X-Goog-User-Project). For API keys, leave GCP_BILLING_PROJECT unset — " +
                "billing follows the project that owns GOOGLE_MAPS_API_KEY (busbuddy-507301). Do not header Maps traffic " +
                "to the legacy Coursera project.",
            MapsForbiddenKind.KeyBlocked =>
                "Address Validation is blocked by this API key's restrictions. Enable Address Validation " +
                "(or Geocoding) for the key — https://developers.google.com/maps/get-started",
            _ =>
                "Address Validation returned HTTP 403 (permission denied). Check API enablement and key restrictions " +
                "on busbuddy-507301 — https://developers.google.com/maps/get-started"
        };
    }

    /// <summary>Only the fields the clerk record needs (Geocoding v4 "Choose fields to return").</summary>
    internal const string GeocodeV4FieldMask =
        "results.placeId,results.location,results.formattedAddress,results.granularity,results.types,results.postalAddress";

    /// <summary>
    /// v4 returns HTTP errors as <c>{"error":{code,status,message,details}}</c> (no legacy <c>status</c> field).
    /// A 403 on Geocoding usually shares the Address Validation root cause (key restrictions / project), so
    /// the Address Validation classification is reported unless Geocoding itself gave a more specific one.
    /// </summary>
    internal static string DescribeGeocodeFailure(
        HttpStatusCode statusCode,
        MapsForbiddenInfo geocodeForbidden,
        MapsForbiddenInfo addressValidationForbidden)
    {
        if (statusCode == HttpStatusCode.NotFound)
        {
            return "No geocode match for that address.";
        }

        if (statusCode == HttpStatusCode.Forbidden || statusCode == HttpStatusCode.Unauthorized)
        {
            var billingHint = geocodeForbidden.Message?.Contains("Billing", StringComparison.OrdinalIgnoreCase) == true;
            if (billingHint)
            {
                return "Google Maps requires billing on the Cloud project for this API key. " +
                       "Enable billing on busbuddy-507301: https://console.cloud.google.com/billing — then enable Geocoding / Address Validation " +
                       "(https://developers.google.com/maps/get-started).";
            }

            return geocodeForbidden.Kind == MapsForbiddenKind.PermissionDenied
                ? DescribeMapsForbidden(addressValidationForbidden)
                : DescribeMapsForbidden(geocodeForbidden)
                    .Replace("Address Validation API is not enabled", "Geocoding API is not enabled", StringComparison.Ordinal)
                    .Replace("Address Validation is blocked", "Geocoding is blocked", StringComparison.Ordinal);
        }

        return (int)statusCode == 429
            ? "Geocoding rate limited — try again later."
            : $"Geocoding failed (HTTP {(int)statusCode}).";
    }

    /// <summary>
    /// Parses a Geocoding API v4 <c>GeocodeAddressResponse</c>:
    /// <c>results[].placeId</c>, <c>results[].location.{latitude,longitude}</c>, <c>results[].formattedAddress</c>,
    /// <c>results[].granularity</c> (ROOFTOP / RANGE_INTERPOLATED / GEOMETRIC_CENTER / APPROXIMATE),
    /// <c>results[].types</c>.
    /// </summary>
    internal static MapsGeocodeResult ParseGeocodeJson(string json, long elapsedMs, bool requirePlotPrecision = true)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            Logger.Warning("Geocoding v4 fallback returned no results ElapsedMs={ElapsedMs}", elapsedMs);
            return new MapsGeocodeResult { Ok = false, ErrorMessage = "No geocode match for that address." };
        }

        var first = results[0];
        string? formatted = null;
        if (first.TryGetProperty("formattedAddress", out var fa) && fa.ValueKind == JsonValueKind.String)
        {
            formatted = fa.GetString();
        }

        string? placeId = null;
        if (first.TryGetProperty("placeId", out var placeEl) && placeEl.ValueKind == JsonValueKind.String)
        {
            placeId = placeEl.GetString();
        }

        var precision = "geocode";
        if (first.TryGetProperty("granularity", out var granularityEl) && granularityEl.ValueKind == JsonValueKind.String)
        {
            precision = granularityEl.GetString() ?? precision;
        }

        var types = ReadStringArray(first, "types") ?? new List<string>();

        double? lat = null;
        double? lon = null;
        if (first.TryGetProperty("location", out var location))
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

        if (!lat.HasValue || !lon.HasValue)
        {
            if (!requirePlotPrecision && !string.IsNullOrWhiteSpace(formatted))
            {
                return new MapsGeocodeResult
                {
                    Ok = true,
                    FormattedAddress = formatted,
                    PlaceId = placeId,
                    Precision = precision
                };
            }

            return new MapsGeocodeResult
            {
                Ok = false,
                FormattedAddress = formatted,
                PlaceId = placeId,
                Precision = precision,
                ErrorMessage = "Geocode response missing coordinates."
            };
        }

        if (requirePlotPrecision
            && !AddressValidationPinPolicy.TryAcceptGeocodeFallbackPin(precision, types, out var rejectReason))
        {
            Logger.Information(
                "Geocoding v4 fallback rejected Precision={Precision} Types={Types} ElapsedMs={ElapsedMs}",
                precision,
                string.Join(",", types),
                elapsedMs);
            return new MapsGeocodeResult
            {
                Ok = false,
                FormattedAddress = formatted,
                PlaceId = placeId,
                Precision = precision,
                ErrorMessage = rejectReason
            };
        }

        Logger.Information(
            "Geocoding v4 fallback OK Precision={Precision} ElapsedMs={ElapsedMs}",
            precision,
            elapsedMs);

        return new MapsGeocodeResult
        {
            Ok = true,
            FormattedAddress = formatted,
            Latitude = lat,
            Longitude = lon,
            PlaceId = placeId,
            Precision = precision
        };
    }
}
