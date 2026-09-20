using BusBuddy.Core.Configuration;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Resolves the clerk map camera from configured district geography — never a baked-in town.
/// Priority: school pin (caller) → depot → bounding-box centroid → unconfigured US overview.
/// </summary>
public static class DistrictMapAnchor
{
    public static bool TryGetBboxCentroid(RoutingDistrictSettings? settings, out double latitude, out double longitude)
    {
        if (settings is not null && settings.TryGetBoundingBox(out var minLat, out var maxLat, out var minLon, out var maxLon))
        {
            latitude = (minLat + maxLat) / 2d;
            longitude = (minLon + maxLon) / 2d;
            return true;
        }

        latitude = default;
        longitude = default;
        return false;
    }

    public static bool TryGetConfiguredCenter(
        RoutingDistrictSettings? settings,
        out double latitude,
        out double longitude)
    {
        if (DistrictDepot.TryGetCoordinates(settings, out latitude, out longitude))
        {
            return true;
        }

        return TryGetBboxCentroid(settings, out latitude, out longitude);
    }

    public static (double Latitude, double Longitude, int ZoomLevel) ResolveCamera(
        RoutingDistrictSettings? settings,
        double? schoolLatitude = null,
        double? schoolLongitude = null)
    {
        if (schoolLatitude is double schoolLat &&
            schoolLongitude is double schoolLon &&
            IsValidLatitude(schoolLat) &&
            IsValidLongitude(schoolLon))
        {
            return (schoolLat, schoolLon, MapDefaults.SchoolZoomLevel);
        }

        if (TryGetConfiguredCenter(settings, out var lat, out var lon))
        {
            return (lat, lon, MapDefaults.DistrictZoomLevel);
        }

        return (
            MapDefaults.UnconfiguredLatitude,
            MapDefaults.UnconfiguredLongitude,
            MapDefaults.UnconfiguredZoomLevel);
    }

    /// <summary>
    /// District Map Home / first-open camera: depot or bbox, then a school pin, then unconfigured overview.
    /// Pick-map dialogs keep <see cref="ResolveCamera"/> (school first).
    /// </summary>
    public static (double Latitude, double Longitude, int ZoomLevel) ResolveHomeCamera(
        RoutingDistrictSettings? settings,
        double? schoolLatitude = null,
        double? schoolLongitude = null)
    {
        if (TryGetConfiguredCenter(settings, out var lat, out var lon))
        {
            return (lat, lon, MapDefaults.DistrictZoomLevel);
        }

        if (schoolLatitude is double schoolLat &&
            schoolLongitude is double schoolLon &&
            IsValidLatitude(schoolLat) &&
            IsValidLongitude(schoolLon))
        {
            return (schoolLat, schoolLon, MapDefaults.SchoolZoomLevel);
        }

        return (
            MapDefaults.UnconfiguredLatitude,
            MapDefaults.UnconfiguredLongitude,
            MapDefaults.UnconfiguredZoomLevel);
    }

    public static bool IsValidLatitude(double latitude) => latitude is >= -90 and <= 90;

    public static bool IsValidLongitude(double longitude) => longitude is >= -180 and <= 180;
}
