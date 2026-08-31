namespace BusBuddy.Core.Configuration;

/// <summary>
/// District routing / density planner settings. Bound from the <c>RoutingDistrict</c> config section.
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
}
