namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>Map Tiles API session used by Syncfusion <c>ImageryLayer.UrlTemplate</c>.</summary>
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
}
