namespace BusBuddy.Core.Services;

/// <summary>Road routing via Google Routes API (drive path).</summary>
public interface IRoutingService
{
    Task<DrivePathResult> ComputeDrivePathAsync(
        (double Latitude, double Longitude) origin,
        (double Latitude, double Longitude) destination,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drive time/distance from one origin to many destinations (Routes API computeRouteMatrix).
    /// Fail-open: returns empty list when key missing or API errors.
    /// </summary>
    Task<IReadOnlyList<RouteMatrixElement>> ComputeRouteMatrixAsync(
        (double Latitude, double Longitude) origin,
        IReadOnlyList<(double Latitude, double Longitude)> destinations,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a drive-path computation.</summary>
public sealed class DrivePathResult
{
    public string? EncodedPolyline { get; init; }
    public IReadOnlyList<(double Latitude, double Longitude)> Points { get; init; } =
        Array.Empty<(double, double)>();
    public int? DistanceMeters { get; init; }
    public string? Duration { get; init; }

    /// <summary>Road instructions from the same computeRoutes response, in drive order.</summary>
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();

    /// <summary>Per-leg drive seconds from <c>routes.legs.duration</c>, in stop order. Empty when the response omitted them.</summary>
    public IReadOnlyList<int> LegDurationSeconds { get; init; } = Array.Empty<int>();

    public string? Error { get; init; }
    public bool Succeeded => string.IsNullOrEmpty(Error) && !string.IsNullOrWhiteSpace(EncodedPolyline);
}

/// <summary>One origin→destination cell from Routes API <c>computeRouteMatrix</c>.</summary>
public sealed class RouteMatrixElement
{
    public int DestinationIndex { get; init; }

    public int? DistanceMeters { get; init; }

    public string? Duration { get; init; }

    public string? Error { get; init; }

    public bool Succeeded => string.IsNullOrEmpty(Error) && DistanceMeters.HasValue;
}
