namespace BusBuddy.Core.Mapping;

/// <summary>
/// District map basemap labels and Google Map Tiles API URL helpers.
/// Syncfusion WPF <c>ImageryLayer</c> has no <c>LayerType.Google</c> — tiles go through
/// <c>UrlTemplate</c> with <c>{z}/{x}/{y}</c> placeholders.
/// Do not use undocumented <c>mt1.google.com/vt</c> scraper URLs.
/// </summary>
public static class MapBasemap
{
    public const string OpenStreetMap = "OpenStreetMap";
    public const string GoogleRoad = "Google Road";
    public const string GoogleSatellite = "Google Satellite";

    public const string MapTypeRoadmap = "roadmap";
    public const string MapTypeSatellite = "satellite";

    public static IReadOnlyList<string> All { get; } = [OpenStreetMap, GoogleRoad, GoogleSatellite];

    public static bool IsGoogle(string? layer) =>
        string.Equals(layer, GoogleRoad, StringComparison.OrdinalIgnoreCase)
        || string.Equals(layer, GoogleSatellite, StringComparison.OrdinalIgnoreCase);

    public static string MapTypeFor(string? layer) =>
        string.Equals(layer, GoogleSatellite, StringComparison.OrdinalIgnoreCase)
            ? MapTypeSatellite
            : MapTypeRoadmap;

    public static string TileUrlTemplate(string sessionToken, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return "https://tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}"
            + "?session=" + Uri.EscapeDataString(sessionToken)
            + "&key=" + Uri.EscapeDataString(apiKey);
    }

    public static string Attribution => "Google Maps";
}
