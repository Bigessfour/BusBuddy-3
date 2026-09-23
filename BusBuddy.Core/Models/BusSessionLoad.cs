namespace BusBuddy.Core.Models;

/// <summary>
/// Year-default route for a bus. Read from Route.AMVehicleId / PMVehicleId.
/// Writes stay on RouteService.AssignVehicleToRouteAsync.
/// </summary>
public sealed class BusHomeRoute
{
    public int BusId { get; init; }
    public int RouteId { get; init; }
    public string RouteName { get; init; } = string.Empty;
    public RouteTimeSlot Slot { get; init; }
}

/// <summary>
/// Roster on a route slot measured against one bus. Overflow is a warning string.
/// Seated hard-stop stays with the caller (student assign). specs/buses.md.
/// </summary>
public sealed class BusSessionLoad
{
    public int BusId { get; init; }
    public int RouteId { get; init; }
    public RouteTimeSlot Slot { get; init; }
    public int SeatedUsed { get; init; }
    public int SeatedCapacity { get; init; }
    public int WheelchairUsed { get; init; }
    public int WheelchairStations { get; init; }
    public string? OverflowWarning { get; init; }
}
