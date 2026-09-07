using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using Microsoft.Extensions.Options;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Official Map Tiles API <c>createSession</c> + 2D tile URL template.
/// Docs: https://developers.google.com/maps/documentation/tile
/// </summary>
public sealed class GoogleMapTileSessionService : IGoogleMapTileSessionService, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GoogleMapTileSessionService>();
    private static readonly Uri CreateSessionUri = new("https://tile.googleapis.com/v1/createSession");
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly bool _ownsHttpClient;
    private readonly object _gate = new();
    private readonly Dictionary<string, GoogleMapTileSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public GoogleMapTileSessionService(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        bool ownsHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GoogleAddressValidationClient.ResolveApiKey(_options));

    public async Task<GoogleMapTileSession?> GetSessionAsync(
        string mapType,
        CancellationToken cancellationToken = default)
    {
        var type = NormalizeMapType(mapType);
        lock (_gate)
        {
            if (_sessions.TryGetValue(type, out var cached) && cached.ExpiresAt - RefreshSkew > DateTimeOffset.UtcNow)
            {
                return cached;
            }
        }

        var created = await CreateSessionAsync(type, cancellationToken).ConfigureAwait(false);
        if (created is null)
        {
            return null;
        }

        lock (_gate)
        {
            _sessions[type] = created;
        }

        return created;
    }

    internal static string NormalizeMapType(string? mapType) =>
        string.Equals(mapType, MapBasemap.MapTypeSatellite, StringComparison.OrdinalIgnoreCase)
            ? MapBasemap.MapTypeSatellite
            : MapBasemap.MapTypeRoadmap;

    internal static DateTimeOffset ParseExpiry(string? raw, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return utcNow.AddHours(12);
        }

        if (long.TryParse(raw, out var unix))
        {
            if (unix > 1_000_000_000_000)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unix);
            }

            if (unix > 1_000_000_000)
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix);
            }
        }

        if (DateTimeOffset.TryParse(raw, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return utcNow.AddHours(12);
    }

    private async Task<GoogleMapTileSession?> CreateSessionAsync(string mapType, CancellationToken cancellationToken)
    {
        var key = GoogleAddressValidationClient.ResolveApiKey(_options);
        if (string.IsNullOrWhiteSpace(key))
        {
            Logger.Information("Map Tiles session skipped — GOOGLE_MAPS_API_KEY not configured");
            return null;
        }

        try
        {
            var uri = new Uri(CreateSessionUri, "?key=" + Uri.EscapeDataString(key));
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(_options.QuotaProject))
            {
                request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
            }

            var body = new
            {
                mapType,
                language = "en-US",
                region = string.IsNullOrWhiteSpace(_options.RegionCode) ? "US" : _options.RegionCode
            };
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Map Tiles createSession HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("session", out var sessionEl)
                || sessionEl.ValueKind != JsonValueKind.String)
            {
                Logger.Warning("Map Tiles createSession missing session");
                return null;
            }

            var token = sessionEl.GetString();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            string? expiryRaw = null;
            if (doc.RootElement.TryGetProperty("expiry", out var expiryEl)
                && expiryEl.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                expiryRaw = expiryEl.ValueKind == JsonValueKind.Number
                    ? expiryEl.GetRawText()
                    : expiryEl.GetString();
            }

            var expires = ParseExpiry(expiryRaw, DateTimeOffset.UtcNow);
            Logger.Information("Map Tiles session created MapType={MapType} Expires={Expires}", mapType, expires);
            return new GoogleMapTileSession(
                mapType,
                token,
                expires,
                MapBasemap.TileUrlTemplate(token, key));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Map Tiles createSession failed");
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
}
