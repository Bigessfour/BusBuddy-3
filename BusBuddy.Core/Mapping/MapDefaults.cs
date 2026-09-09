using BusBuddy.Core.Models;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Camera constants that are not a school district.
/// Configured geography comes from clerk Settings (depot / bbox) and school destinations.
/// </summary>
public static class MapDefaults
{
    /// <summary>Contiguous-US overview used only when no school, depot, or bbox is configured. Never a pin.</summary>
    public const double UnconfiguredLatitude = LocationCoordinate.UsCentroidLatitude;

    public const double UnconfiguredLongitude = LocationCoordinate.UsCentroidLongitude;

    public const int UnconfiguredZoomLevel = 5;

    /// <summary>Zoom when a district depot or bounding box is configured.</summary>
    public const int DistrictZoomLevel = 11;

    public const int SchoolZoomLevel = 13;

    /// <summary>Alias for district zoom (existing map bindings / tests).</summary>
    public const int DefaultZoomLevel = DistrictZoomLevel;

    /// <summary>
    /// Floor for span-fit zoom. 8 still clips a large county; 6 is regional without becoming the US overview (5).
    /// </summary>
    public const int MinFitZoomLevel = 6;

    public const int MaxFitZoomLevel = 16;

    /// <summary>Lowest tile zoom the imagery layer accepts.</summary>
    public const int MinZoomLevel = 1;

    /// <summary>
    /// Syncfusion <c>ImageryLayer</c> clamps wheel zoom at 19 (OpenStreetMap's deepest level).
    /// Keep the view model on the same ceiling so a wheel zoom never round-trips to a different value.
    /// </summary>
    public const int MaxZoomLevel = 19;

    /// <summary>
    /// Home / pickup / waypoint captions are hidden below this zoom so a county-wide
    /// plot reads as dots instead of overlapping text. Schools and depots always keep their caption.
    /// </summary>
    public const int DetailLabelZoomLevel = 12;

    /// <summary>Viewport assumed when the view has not reported its pixel size yet.</summary>
    public const double DefaultViewportWidth = 1024;

    public const double DefaultViewportHeight = 768;

    /// <summary>Web Mercator tile edge in device-independent pixels.</summary>
    public const double TilePixels = 256;

    /// <summary>Fraction of the viewport the fitted bounds may occupy (keeps edge pins off the border).</summary>
    public const double FitPaddingFraction = 0.85;

    /// <summary>
    /// Span-fit zoom for an unknown viewport (uses <see cref="DefaultViewportWidth"/> x <see cref="DefaultViewportHeight"/>).
    /// </summary>
    public static int ZoomForBounds(double minLat, double maxLat, double minLon, double maxLon) =>
        ZoomForBounds(minLat, maxLat, minLon, maxLon, DefaultViewportWidth, DefaultViewportHeight);

    /// <summary>
    /// SfMap has no fit-bounds API (ImageryLayer is Center + ZoomLevel), and its <c>Radius</c> fit
    /// doubles the bounds before solving. This is the standard Web Mercator "bounds zoom" solve:
    /// the largest integer zoom where the bounds still fit inside the padded viewport on both axes.
    /// </summary>
    public static int ZoomForBounds(
        double minLat,
        double maxLat,
        double minLon,
        double maxLon,
        double viewportWidth,
        double viewportHeight)
    {
        var latSpan = Math.Abs(maxLat - minLat);
        var lonSpan = Math.Abs(maxLon - minLon);
        if (latSpan < 1e-8 && lonSpan < 1e-8)
        {
            return SchoolZoomLevel;
        }

        var width = IsUsableLength(viewportWidth) ? viewportWidth : DefaultViewportWidth;
        var height = IsUsableLength(viewportHeight) ? viewportHeight : DefaultViewportHeight;
        var usableWidth = width * FitPaddingFraction;
        var usableHeight = height * FitPaddingFraction;

        // Fractions of the whole Web Mercator world: Y spans [-π, π] (2π total), X spans 360°.
        var latFraction = Math.Abs(MercatorY(maxLat) - MercatorY(minLat)) / (2d * Math.PI);
        var lonFraction = lonSpan / 360d;

        var latZoom = latFraction > 1e-12
            ? Math.Log2(usableHeight / TilePixels / latFraction)
            : double.MaxValue;
        var lonZoom = lonFraction > 1e-12
            ? Math.Log2(usableWidth / TilePixels / lonFraction)
            : double.MaxValue;

        var zoom = Math.Floor(Math.Min(latZoom, lonZoom));
        return (int)Math.Clamp(zoom, MinFitZoomLevel, MaxFitZoomLevel);
    }

    /// <summary>Clamp any requested tile zoom into the imagery layer's range.</summary>
    public static int ClampZoom(int zoomLevel) => Math.Clamp(zoomLevel, MinZoomLevel, MaxZoomLevel);

    /// <summary>True when home/pickup/waypoint captions should render at this zoom.</summary>
    public static bool ShowsDetailLabels(int zoomLevel) => zoomLevel >= DetailLabelZoomLevel;

    /// <summary>
    /// Inverse of <see cref="ZoomForBounds(double,double,double,double,double,double)"/>: the geographic
    /// box the viewport shows for a Center + ZoomLevel camera (Web Mercator, 256px tiles). Used for the
    /// Map Tiles API viewport request that returns the required Google copyright string.
    /// </summary>
    public static MapViewportBounds BoundsForViewport(
        double centerLat,
        double centerLon,
        int zoomLevel,
        double viewportWidth,
        double viewportHeight)
    {
        var width = IsUsableLength(viewportWidth) ? viewportWidth : DefaultViewportWidth;
        var height = IsUsableLength(viewportHeight) ? viewportHeight : DefaultViewportHeight;
        var worldPixels = TilePixels * Math.Pow(2d, ClampZoom(zoomLevel));

        var halfLonSpan = Math.Min(180d, width / worldPixels * 180d);
        var halfMercatorSpan = Math.Min(Math.PI, height / worldPixels * Math.PI);

        var centerY = MercatorY(Math.Clamp(centerLat, -MaxMercatorLatitude, MaxMercatorLatitude));
        var north = InverseMercatorY(centerY + halfMercatorSpan);
        var south = InverseMercatorY(centerY - halfMercatorSpan);
        var east = Math.Clamp(centerLon + halfLonSpan, -180d, 180d);
        var west = Math.Clamp(centerLon - halfLonSpan, -180d, 180d);
        return new MapViewportBounds(north, south, east, west);
    }

    private const double MaxMercatorLatitude = 85.05112878;

    private static bool IsUsableLength(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static double MercatorY(double latitude)
    {
        var clamped = Math.Clamp(latitude, -MaxMercatorLatitude, MaxMercatorLatitude);
        var sin = Math.Sin(clamped * Math.PI / 180d);
        return Math.Log((1d + sin) / (1d - sin)) / 2d;
    }

    private static double InverseMercatorY(double y)
    {
        var clamped = Math.Clamp(y, -Math.PI, Math.PI);
        return Math.Clamp(Math.Atan(Math.Sinh(clamped)) * 180d / Math.PI, -MaxMercatorLatitude, MaxMercatorLatitude);
    }
}

/// <summary>Geographic bounds (degrees) of the tiles currently on screen.</summary>
public readonly record struct MapViewportBounds(double North, double South, double East, double West);
