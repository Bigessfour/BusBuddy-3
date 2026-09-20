using BusBuddy.Core.Mapping;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Map Tiles API session used by the district imagery layer <c>UrlTemplate</c>.</summary>
public sealed record GoogleMapTileSession(
    string MapType,
    string SessionToken,
    DateTimeOffset ExpiresAt,
    string UrlTemplate);

public interface IGoogleMapTileSessionService
{
    bool IsConfigured { get; }

    Task<GoogleMapTileSession?> GetSessionAsync(
        string mapType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Map Tiles API viewport request — returns the <c>copyright</c> attribution string that the
    /// Map Tiles API Policies require next to roadmap/satellite tiles, or null when unavailable.
    /// Docs: https://developers.google.com/maps/documentation/tile/2d-tiles-overview#viewport-information-requests
    /// </summary>
    Task<string?> GetViewportCopyrightAsync(
        string mapType,
        int zoom,
        MapViewportBounds bounds,
        CancellationToken cancellationToken = default);
}
