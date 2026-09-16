namespace BusBuddy.Core.Models;

/// <summary>
/// Canonical location types from specs/locations.md.
/// A Trip uses locations; it is not a location type.
/// </summary>
public static class LocationTypes
{
    public const string School = "School";
    public const string PickupStop = "PickupStop";
    public const string StudentHome = "StudentHome";
    public const string Depot = "Depot";
    public const string Maintenance = "Maintenance";
    public const string Fuel = "Fuel";
    public const string TripDestination = "TripDestination";

    public static readonly string[] All =
    {
        School, PickupStop, StudentHome, Depot, Maintenance, Fuel, TripDestination
    };

    public const string PickupModeHome = "Home";
    public const string PickupModeCatalogStop = "CatalogStop";

    /// <summary>
    /// Map leftover DestinationType labels onto the spec type.
    /// Field trip / sports / etc. are TripDestination, not a second place model.
    /// </summary>
    public static string FromDestinationType(string? destinationType)
    {
        if (string.IsNullOrWhiteSpace(destinationType))
        {
            return TripDestination;
        }

        if (destinationType.Equals(School, StringComparison.OrdinalIgnoreCase)
            || DestinationTypes.IsSchool(destinationType))
        {
            return School;
        }

        if (destinationType.Equals(Depot, StringComparison.OrdinalIgnoreCase))
        {
            return Depot;
        }

        if (destinationType.Equals(Maintenance, StringComparison.OrdinalIgnoreCase))
        {
            return Maintenance;
        }

        if (destinationType.Equals(Fuel, StringComparison.OrdinalIgnoreCase))
        {
            return Fuel;
        }

        if (destinationType.Equals(PickupStop, StringComparison.OrdinalIgnoreCase))
        {
            return PickupStop;
        }

        if (destinationType.Equals(StudentHome, StringComparison.OrdinalIgnoreCase))
        {
            return StudentHome;
        }

        return TripDestination;
    }

    public static bool IsDistrictFacility(string? locationType)
    {
        var type = FromDestinationType(locationType);
        return type is School or Depot or Maintenance or Fuel or PickupStop;
    }

    /// <summary>District-owned School, Depot, Maintenance, Fuel, and published PickupStop are year-stable.</summary>
    public static bool IsSchoolYearStable(string? locationType)
    {
        var type = FromDestinationType(locationType);
        return type is not StudentHome and not TripDestination;
    }

    /// <summary>School or trip site — valid Confirmed-trip origin/destination once geocoded.</summary>
    public static bool CanBeTripPlace(string? destinationType)
    {
        var type = FromDestinationType(destinationType);
        return type is School or TripDestination;
    }

    /// <summary>
    /// Board strings that are not a place yet. Stay DestinationName on the trip; do not invent a pin.
    /// </summary>
    public static bool IsUnresolvedPlaceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var text = name.Trim();
        if (text.Equals("TBD", StringComparison.OrdinalIgnoreCase)
            || text.Equals("TBA", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return text.Contains("See Trip Notes", StringComparison.OrdinalIgnoreCase)
            || text.Contains("TBD", StringComparison.OrdinalIgnoreCase);
    }

    public static string ValidationStatus(bool hasValidatedCoordinates) =>
        hasValidatedCoordinates ? "validated" : "needs validation";
}

/// <summary>
/// Shared lat/lng gate: no pin, waypoint, or Confirmed trip place without a real geocoded point.
/// Rejects missing, out of range, 0,0, and the MapDefaults US centroid.
/// </summary>
public static class LocationCoordinate
{
    public const double UsCentroidLatitude = 39.8283;
    public const double UsCentroidLongitude = -98.5795;
    public const string NeedsValidation = "needs validation";

    public static bool IsValidated(decimal? latitude, decimal? longitude)
    {
        if (latitude is not decimal lat || longitude is not decimal lon)
        {
            return false;
        }

        return IsValidated((double)lat, (double)lon);
    }

    public static bool IsValidated(double? latitude, double? longitude) =>
        latitude is double lat && longitude is double lon && IsValidated(lat, lon);

    public static bool IsValidated(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return false;
        }

        if (Math.Abs(latitude) < 1e-6 && Math.Abs(longitude) < 1e-6)
        {
            return false;
        }

        return Math.Abs(latitude - UsCentroidLatitude) >= 0.01
            || Math.Abs(longitude - UsCentroidLongitude) >= 0.01;
    }

    /// <summary>
    /// Persistable pair, or (null, null) when the point cannot be a pin.
    /// Student homes and schools may save without GPS; catalog stops still require a validated point.
    /// </summary>
    public static (decimal? Latitude, decimal? Longitude) ValidatedOrNull(
        decimal? latitude,
        decimal? longitude) =>
        IsValidated(latitude, longitude) ? (latitude, longitude) : (null, null);
}
