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

    /// <inheritdoc />
    public async Task<string?> GetViewportCopyrightAsync(
        string mapType,
        int zoom,
        MapViewportBounds bounds,
        CancellationToken cancellationToken = default)
    {
        var key = GoogleAddressValidationClient.ResolveApiKey(_options);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var session = await GetSessionAsync(mapType, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildViewportUri(session.SessionToken, key, zoom, bounds));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Debug("Map Tiles viewport HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseViewportCopyright(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Map Tiles viewport request failed");
            return null;
        }
    }

    /// <summary>
    /// <c>GET https://tile.googleapis.com/tile/v1/viewport?session=...&amp;key=...&amp;zoom=...&amp;north=...&amp;south=...&amp;east=...&amp;west=...</c>
    /// (2D Tiles "Viewport information requests").
    /// </summary>
    internal static Uri BuildViewportUri(string sessionToken, string apiKey, int zoom, MapViewportBounds bounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var z = MapDefaults.ClampZoom(zoom).ToString(inv);
        return new Uri(
            "https://tile.googleapis.com/tile/v1/viewport"
            + "?session=" + Uri.EscapeDataString(sessionToken)
            + "&key=" + Uri.EscapeDataString(apiKey)
            + "&zoom=" + z
            + "&north=" + Math.Clamp(bounds.North, -90, 90).ToString(inv)
            + "&south=" + Math.Clamp(bounds.South, -90, 90).ToString(inv)
            + "&east=" + Math.Clamp(bounds.East, -180, 180).ToString(inv)
            + "&west=" + Math.Clamp(bounds.West, -180, 180).ToString(inv));
    }

    /// <summary>Viewport response body is <c>{"copyright": "Map data ©2026 Google", "maxZoomRects": [...]}</c>.</summary>
    internal static string? ParseViewportCopyright(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("copyright", out var copyrightEl)
                && copyrightEl.ValueKind == JsonValueKind.String)
            {
                var text = copyrightEl.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }
        catch (JsonException)
        {
            // Fall through: attribution simply stays on the static Google label.
        }

        return null;
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
            var bodyJson = JsonSerializer.Serialize(new
            {
                mapType,
                language = "en-US",
                region = string.IsNullOrWhiteSpace(_options.RegionCode) ? "US" : _options.RegionCode
            });

            var (status, json) = await PostCreateSessionAsync(
                    key,
                    bodyJson,
                    includeQuotaProject: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IsSuccessStatusCode(status)
                && !string.IsNullOrWhiteSpace(_options.QuotaProject)
                && GoogleAddressValidationClient.ClassifyMapsForbidden(json).Kind
                    == GoogleAddressValidationClient.MapsForbiddenKind.QuotaProjectDenied)
            {
                Logger.Warning(
                    "Map Tiles createSession quota project denied ({QuotaProject}) — retrying without X-Goog-User-Project",
                    _options.QuotaProject);
                (status, json) = await PostCreateSessionAsync(
                        key,
                        bodyJson,
                        includeQuotaProject: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!IsSuccessStatusCode(status))
            {
                var forbidden = GoogleAddressValidationClient.ClassifyMapsForbidden(json);
                Logger.Warning(
                    "Map Tiles createSession HTTP {Status} Kind={Kind} Message={Message}",
                    (int)status,
                    forbidden.Kind,
                    TruncateForLog(forbidden.Message ?? json));
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

    private async Task<(System.Net.HttpStatusCode Status, string Json)> PostCreateSessionAsync(
        string key,
        string bodyJson,
        bool includeQuotaProject,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(CreateSessionUri, "?key=" + Uri.EscapeDataString(key));
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (includeQuotaProject && !string.IsNullOrWhiteSpace(_options.QuotaProject))
        {
            request.Headers.TryAddWithoutValidation("X-Goog-User-Project", _options.QuotaProject);
        }

        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (response.StatusCode, json);
    }

    private static bool IsSuccessStatusCode(System.Net.HttpStatusCode status) =>
        (int)status is >= 200 and <= 299;

    private static string TruncateForLog(string? text, int max = 240)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
