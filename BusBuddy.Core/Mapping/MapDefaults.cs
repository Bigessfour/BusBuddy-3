namespace BusBuddy.Core.Mapping;

/// <summary>
/// Camera constants that are not a school district.
/// Configured geography comes from clerk Settings (depot / bbox) and school destinations.
/// </summary>
public static class MapDefaults
{
    /// <summary>Contiguous-US overview used only when no school, depot, or bbox is configured.</summary>
    public const double UnconfiguredLatitude = 39.8283;

    public const double UnconfiguredLongitude = -98.5795;

    public const int UnconfiguredZoomLevel = 5;

    /// <summary>Zoom when a district depot or bounding box is configured.</summary>
    public const int DistrictZoomLevel = 11;

    public const int SchoolZoomLevel = 13;

    /// <summary>Alias for district zoom (existing map bindings / tests).</summary>
    public const int DefaultZoomLevel = DistrictZoomLevel;
}
