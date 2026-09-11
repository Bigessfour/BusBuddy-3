namespace BusBuddy.Core.Mapping;

/// <summary>
/// District map basemap labels and Google Map Tiles API URL helpers.
/// Syncfusion WPF custom tiles use <c>ImageryLayer.UrlTemplate</c> with this <c>{z}/{x}/{y}</c> session URL.
/// Do not use OpenStreetMap or undocumented <c>mt1.google.com/vt</c> scraper URLs.
/// </summary>
public static class MapBasemap
{
    public const string GoogleRoad = "Google Road";
    public const string GoogleSatellite = "Google Satellite";

    public const string MapTypeRoadmap = "roadmap";
    public const string MapTypeSatellite = "satellite";

    public static IReadOnlyList<string> All { get; } = [GoogleRoad, GoogleSatellite];

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

    /// <summary>Expands a <c>{z}/{x}/{y}</c> (or <c>{zoom}</c>) template for one tile.</summary>
    public static string ResolveTileUrl(string urlTemplate, int zoom, int x, int y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        var z = zoom.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return urlTemplate
            .Replace("{z}", z, StringComparison.Ordinal)
            .Replace("{zoom}", z, StringComparison.Ordinal)
            .Replace("{x}", x.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{y}", y.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    public static string Attribution => "Google Maps";
}
