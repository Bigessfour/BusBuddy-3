namespace BusBuddy.Core.Configuration;

/// <summary>
/// District routing / density planner settings. Bound from the <c>RoutingDistrict</c> config section
/// and overlaid by clerk Settings (no baked-in town). Empty bbox/depot means unconfigured.
/// </summary>
public sealed class RoutingDistrictSettings
{
    public const string SectionName = "RoutingDistrict";

    /// <summary>South edge of district frame (degrees). Null ⇒ derive from student homes.</summary>
    public double? BoundingBoxMinLat { get; set; }

    /// <summary>West edge of district frame (degrees).</summary>
    public double? BoundingBoxMinLon { get; set; }

    /// <summary>North edge of district frame (degrees).</summary>
    public double? BoundingBoxMaxLat { get; set; }

    /// <summary>East edge of district frame (degrees).</summary>
    public double? BoundingBoxMaxLon { get; set; }

    /// <summary>Target riders per density cell (hints N-cell grid). Default ~20.</summary>
    public int TargetRidersPerCell { get; set; } = 20;

    /// <summary>Estimated travel minutes between consecutive pickups that force an outlier split.</summary>
    public int MaxPickupGapMinutes { get; set; } = 12;

    /// <summary>Fallback speed for Haversine ETA when Maps routing is unavailable.</summary>
    public double AverageSpeedMph { get; set; } = 25.0;

    /// <summary>Soft comfort cap (minutes) — warn-and-allow when exceeded.</summary>
    public int? MaxRideMinutes { get; set; } = 45;

    /// <summary>When false, hard seating block has no UI override path.</summary>
    public bool AllowSeatingOverride { get; set; } = true;

    /// <summary>Max walk distance (meters) when suggesting a catalog pickup stop from a geocoded home.</summary>
    public double StopSuggestMaxMeters { get; set; } = 400;

    /// <summary>
    /// Minimum Home pickups (including the student on the form) inside
    /// <see cref="StopSuggestMaxMeters"/> before hinting the clerk to publish a catalog stop.
    /// </summary>
    public int CatalogStopClusterMinHomes { get; set; } = 2;

    /// <summary>Bus barn / depot display name (not a school destination).</summary>
    public string? DepotName { get; set; }

    /// <summary>Street address for the district bus barn.</summary>
    public string? DepotAddress { get; set; }

    public string? DepotCity { get; set; }

    public string? DepotState { get; set; }

    public string? DepotZipCode { get; set; }

    /// <summary>Depot latitude (degrees). When set with <see cref="DepotLongitude"/>, routes start/end here.</summary>
    public double? DepotLatitude { get; set; }

    /// <summary>Depot longitude (degrees).</summary>
    public double? DepotLongitude { get; set; }

    public bool TryGetBoundingBox(
        out double minLat,
        out double maxLat,
        out double minLon,
        out double maxLon)
    {
        minLat = maxLat = minLon = maxLon = default;
        if (BoundingBoxMinLat is not double south ||
            BoundingBoxMaxLat is not double north ||
            BoundingBoxMinLon is not double west ||
            BoundingBoxMaxLon is not double east ||
            north <= south ||
            east <= west ||
            south is < -90 or > 90 ||
            north is < -90 or > 90 ||
            west is < -180 or > 180 ||
            east is < -180 or > 180)
        {
            return false;
        }

        minLat = south;
        maxLat = north;
        minLon = west;
        maxLon = east;
        return true;
    }

    /// <summary>
    /// Replace non-positive planner knobs with the property defaults. Depot and bounding box are left alone
    /// so an unconfigured district stays unconfigured.
    /// </summary>
    public void CoercePlannerValues()
    {
        var defaults = new RoutingDistrictSettings();

        if (TargetRidersPerCell < 1)
        {
            TargetRidersPerCell = defaults.TargetRidersPerCell;
        }

        if (MaxPickupGapMinutes < 1)
        {
            MaxPickupGapMinutes = defaults.MaxPickupGapMinutes;
        }

        if (double.IsNaN(AverageSpeedMph) || double.IsInfinity(AverageSpeedMph) || AverageSpeedMph <= 0)
        {
            AverageSpeedMph = defaults.AverageSpeedMph;
        }

        if (MaxRideMinutes is <= 0)
        {
            MaxRideMinutes = defaults.MaxRideMinutes;
        }

        if (double.IsNaN(StopSuggestMaxMeters) || double.IsInfinity(StopSuggestMaxMeters) || StopSuggestMaxMeters <= 0)
        {
            StopSuggestMaxMeters = defaults.StopSuggestMaxMeters;
        }

        if (CatalogStopClusterMinHomes < 1)
        {
            CatalogStopClusterMinHomes = defaults.CatalogStopClusterMinHomes;
        }
    }
}
