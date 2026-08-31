namespace BusBuddy.Core.Mapping;

/// <summary>
/// Fallback map center when no school destination with coordinates exists.
/// Continental US centroid — clerks should add their school catalog for a real center.
/// </summary>
public static class MapDefaults
{
    public const double FallbackLatitude = 39.8283;
    public const double FallbackLongitude = -98.5795;
    public const int DefaultZoomLevel = 5;
    public const int SchoolZoomLevel = 13;
}
