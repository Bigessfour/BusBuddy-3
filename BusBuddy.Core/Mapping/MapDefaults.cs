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

    /// <summary>
    /// SfMap has no fit-bounds API (ImageryLayer is Center + ZoomLevel).
    /// Derive zoom from the lat/lon span so a county-wide pin set is not clipped at school zoom (13).
    /// </summary>
    public static int ZoomForBounds(double minLat, double maxLat, double minLon, double maxLon)
    {
        var latSpan = Math.Abs(maxLat - minLat);
        var lonSpan = Math.Abs(maxLon - minLon);
        if (latSpan < 1e-8 && lonSpan < 1e-8)
        {
            return SchoolZoomLevel;
        }

        var midLat = (minLat + maxLat) / 2d;
        var lonAdjusted = lonSpan * Math.Cos(midLat * Math.PI / 180d);
        var span = Math.Max(latSpan, lonAdjusted) * 1.6;
        if (span < 1e-8)
        {
            return SchoolZoomLevel;
        }

        var zoom = Math.Log2(360d / span);
        return (int)Math.Clamp(Math.Round(zoom), MinFitZoomLevel, MaxFitZoomLevel);
    }

    /// <summary>
    /// Half-diagonal of the bounds in kilometers (padding included) for
    /// <c>ImageryLayer.Radius</c> + <c>DistanceType.KiloMeter</c>.
    /// </summary>
    public static double RadiusKilometers(double minLat, double maxLat, double minLon, double maxLon)
    {
        var km = HaversineKm(minLat, minLon, maxLat, maxLon) / 2d * 1.25;
        if (km < 1e-3)
        {
            return 3d;
        }

        return Math.Clamp(km, 1d, 400d);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthKm = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;
        var a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d)
            + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d)
              * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);
        return earthKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }
}
