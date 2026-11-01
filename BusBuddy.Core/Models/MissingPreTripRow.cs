namespace BusBuddy.Core.Models;

/// <summary>
/// Supervisor visibility for a route session that lacks a PreTrip duty log that day.
/// Specs: drivers.md — visible only; does not block the route.
/// </summary>
public sealed class MissingPreTripRow
{
    public int RouteId { get; init; }
    public string RouteName { get; init; } = string.Empty;
    public DutySession Session { get; init; }
    public int BusId { get; init; }
    public string BusNumber { get; init; } = string.Empty;
    public int DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
}
