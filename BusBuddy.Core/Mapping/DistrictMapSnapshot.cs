namespace BusBuddy.Core.Mapping;

/// <summary>
/// One database read for the district map: schools, catalog stops, and — when a route
/// is selected — that run's stored path, published stops, and assigned homes.
/// Google HTTP stays in <c>IMapsGeoService</c> / <c>IRoutingService</c>.
/// </summary>
public sealed class DistrictMapSnapshot
{
    public static DistrictMapSnapshot Empty { get; } = new();

    public IReadOnlyList<DistrictMapPlace> Schools { get; init; } = [];

    public IReadOnlyList<DistrictMapPlace> CatalogStops { get; init; } = [];

    /// <summary>Active schools and catalog stops that have no validated coordinates.</summary>
    public IReadOnlyList<string> NeedsValidation { get; init; } = [];

    /// <summary>Null when no route id was requested, or the route row is missing.</summary>
    public DistrictMapRoute? SelectedRoute { get; init; }
}

public sealed class DistrictMapPlace
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

/// <summary>
/// One assigned rider and the pins <see cref="StudentPlotLocation"/> already chose.
/// </summary>
public sealed class DistrictMapHome
{
    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public IReadOnlyList<StudentPlotPoint> Pins { get; init; } = [];
}

public sealed class DistrictMapStop
{
    public int StopOrder { get; init; }

    public string Name { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

public sealed class DistrictMapRoute
{
    public int RouteId { get; init; }

    public string? RouteName { get; init; }

    /// <summary>Stored drive path when present; stop-derived pairs only when the stored JSON is empty.</summary>
    public string? WaypointsJson { get; init; }

    public decimal? DistanceMiles { get; init; }

    public int? DurationMinutes { get; init; }

    public IReadOnlyList<DistrictMapStop> PublishedStops { get; init; } = [];

    public IReadOnlyList<DistrictMapHome> Homes { get; init; } = [];
}
