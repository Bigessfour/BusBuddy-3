namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Google Route Optimization API (<c>optimizeTours</c>). Proposes visit order / vehicle
/// assignment. Does not replace <see cref="Interfaces.IRoutingService"/> (polylines)
/// and must not run unattended — clerk initiates.
/// </summary>
public interface IRouteOptimizationService
{
    bool IsConfigured { get; }

    Task<OptimizeToursResult> OptimizeToursAsync(
        OptimizeToursProblem problem,
        CancellationToken cancellationToken = default);
}

public sealed class RouteOptimizationStop
{
    public string Label { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public int Load { get; init; } = 1;
}

public sealed class RouteOptimizationVehicle
{
    public string Label { get; init; } = "vehicle";

    public double StartLatitude { get; init; }

    public double StartLongitude { get; init; }

    public double EndLatitude { get; init; }

    public double EndLongitude { get; init; }

    public int Capacity { get; init; } = 70;
}

public sealed class RouteOptimizationShipment
{
    public string Label { get; init; } = string.Empty;

    public RouteOptimizationStop Pickup { get; init; } = new();

    public RouteOptimizationStop? Delivery { get; init; }

    public int Load { get; init; } = 1;
}

public sealed class OptimizeToursProblem
{
    public IReadOnlyList<RouteOptimizationShipment> Shipments { get; init; } =
        Array.Empty<RouteOptimizationShipment>();

    public IReadOnlyList<RouteOptimizationVehicle> Vehicles { get; init; } =
        Array.Empty<RouteOptimizationVehicle>();

    public DateTime GlobalStartUtc { get; init; }

    public DateTime GlobalEndUtc { get; init; }

    public string Timeout { get; init; } = "10s";
}

public sealed class OptimizedVisit
{
    public string VehicleLabel { get; init; } = string.Empty;

    public string ShipmentLabel { get; init; } = string.Empty;

    public bool IsPickup { get; init; }
}

public sealed class OptimizeToursResult
{
    public bool Succeeded { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<OptimizedVisit> Visits { get; init; } = Array.Empty<OptimizedVisit>();

    public IReadOnlyList<string> SkippedShipmentLabels { get; init; } = Array.Empty<string>();

    public static OptimizeToursResult Fail(string error) =>
        new() { Succeeded = false, Error = error };

    public static OptimizeToursResult Ok(
        IReadOnlyList<OptimizedVisit> visits,
        IReadOnlyList<string>? skipped = null) =>
        new()
        {
            Succeeded = true,
            Visits = visits,
            SkippedShipmentLabels = skipped ?? Array.Empty<string>(),
        };
}
