using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Places API (New) autocomplete + place details for student address type-ahead.</summary>
public sealed class GooglePlacesAutocompleteService : IPlacesAutocompleteService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GooglePlacesAutocompleteService>();
    private static readonly Uri AutocompleteUri = new("https://places.googleapis.com/v1/places:autocomplete");
    private const string AutocompleteFieldMask =
        "suggestions.placePrediction.placeId,suggestions.placePrediction.text,suggestions.placePrediction.structuredFormat";
    private const string DetailsFieldMask = "id,formattedAddress,addressComponents,location";

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly bool _ownsHttpClient;

    public GooglePlacesAutocompleteService(HttpClient httpClient, IOptions<GoogleMapsOptions> options, bool ownsHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GoogleAddressValidationClient.ResolveApiKey(_options));

    public async Task<IReadOnlyList<PlaceAutocompleteSuggestion>> GetSuggestionsAsync(
        string input,
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var key = GoogleAddressValidationClient.ResolveApiKey(_options);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<PlaceAutocompleteSuggestion>();
        }

        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length < 3)
        {
            return Array.Empty<PlaceAutocompleteSuggestion>();
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, AutocompleteUri);
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", AutocompleteFieldMask);
            if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
            }

            var body = new Dictionary<string, object?>
            {
                ["input"] = trimmed,
                ["includedRegionCodes"] = new[] { "us" },
                ["includedPrimaryTypes"] = new[] { "street_address", "premise", "subpremise" },
                ["languageCode"] = "en",
                ["locationBias"] = new
                {
                    circle = new
                    {
                        center = new
                        {
                            latitude = _options.AutocompleteBiasLatitude,
                            longitude = _options.AutocompleteBiasLongitude,
                        },
                        radius = _options.AutocompleteBiasRadiusMeters,
                    },
                },
            };
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                body["sessionToken"] = sessionToken;
            }
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await SendWithRateLimitRetryAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Places autocomplete HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return Array.Empty<PlaceAutocompleteSuggestion>();
            }

            var suggestions = ParseAutocompleteResponse(json);
            Logger.Debug(
                "Places autocomplete returned {Count} suggestions ElapsedMs={ElapsedMs}",
                suggestions.Count,
                sw.ElapsedMilliseconds);
            return suggestions;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Places autocomplete request failed");
            return Array.Empty<PlaceAutocompleteSuggestion>();
        }
    }

    public async Task<PlaceAddressDetails?> GetPlaceDetailsAsync(
        string placeId,
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var key = GoogleAddressValidationClient.ResolveApiKey(_options);
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(placeId))
        {
            return null;
        }

        var normalizedId = NormalizePlaceId(placeId);
        var sw = Stopwatch.StartNew();
        try
        {
            var detailsUri = string.IsNullOrWhiteSpace(sessionToken)
                ? $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(normalizedId)}"
                : $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(normalizedId)}?sessionToken={Uri.EscapeDataString(sessionToken)}";
            var uri = new Uri(detailsUri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", key);
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", DetailsFieldMask);
            if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
            }

            using var response = await SendWithRateLimitRetryAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Warning("Places details not found PlaceId={PlaceIdPrefix}…", normalizedId[..Math.Min(8, normalizedId.Length)]);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Places details HTTP {Status} ElapsedMs={ElapsedMs}",
                    (int)response.StatusCode,
                    sw.ElapsedMilliseconds);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var details = PlaceAddressComponentParser.Parse(doc.RootElement);
            Logger.Information(
                "Places details resolved City={City} State={State} ElapsedMs={ElapsedMs}",
                details.City,
                details.State,
                sw.ElapsedMilliseconds);
            return details;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Places details request failed");
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public static string NormalizePlaceId(string placeId)
    {
        const string prefix = "places/";
        return placeId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? placeId[prefix.Length..]
            : placeId;
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != (HttpStatusCode)429)
        {
            return response;
        }

        Logger.Warning("Places API rate limited — retrying once");
        response.Dispose();
        await Task.Delay(400, cancellationToken).ConfigureAwait(false);
        using var retryRequest = await CloneRequestAsync(request).ConfigureAwait(false);
        return await _httpClient.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private static IReadOnlyList<PlaceAutocompleteSuggestion> ParseAutocompleteResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("suggestions", out var suggestionsEl) ||
            suggestionsEl.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PlaceAutocompleteSuggestion>();
        }

        var list = new List<PlaceAutocompleteSuggestion>();
        foreach (var suggestion in suggestionsEl.EnumerateArray())
        {
            if (!suggestion.TryGetProperty("placePrediction", out var prediction))
            {
                continue;
            }

            var placeId = ReadPlaceId(prediction);
            if (string.IsNullOrWhiteSpace(placeId))
            {
                continue;
            }

            var display = ReadText(prediction, "text");
            var primary = ReadStructuredText(prediction, "mainText") ?? display;
            var secondary = ReadStructuredText(prediction, "secondaryText") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(display))
            {
                display = string.IsNullOrWhiteSpace(secondary) ? primary : $"{primary}, {secondary}";
            }

            list.Add(new PlaceAutocompleteSuggestion
            {
                PlaceId = placeId,
                DisplayText = display,
                PrimaryText = primary,
                SecondaryText = secondary,
            });
        }

        return list;
    }

    private static string? ReadPlaceId(JsonElement prediction)
    {
        if (prediction.TryGetProperty("placeId", out var idEl) && idEl.ValueKind == JsonValueKind.String)
        {
            return NormalizePlaceId(idEl.GetString()!);
        }

        if (prediction.TryGetProperty("place", out var placeEl) && placeEl.ValueKind == JsonValueKind.String)
        {
            return NormalizePlaceId(placeEl.GetString()!);
        }

        return null;
    }

    private static string? ReadText(JsonElement prediction, string propertyName)
    {
        if (!prediction.TryGetProperty(propertyName, out var textEl))
        {
            return null;
        }

        if (textEl.ValueKind == JsonValueKind.String)
        {
            return textEl.GetString();
        }

        if (textEl.TryGetProperty("text", out var inner) && inner.ValueKind == JsonValueKind.String)
        {
            return inner.GetString();
        }

        return null;
    }

    private static string? ReadStructuredText(JsonElement prediction, string partName)
    {
        if (!prediction.TryGetProperty("structuredFormat", out var structured))
        {
            return null;
        }

        return ReadText(structured, partName);
    }
}
