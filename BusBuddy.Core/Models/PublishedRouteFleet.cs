namespace BusBuddy.Core.Models;

/// <summary>
/// Year-default bus/driver/begin time on a published route row. Hop 5 copies this onto a daily
/// <c>Schedule</c>. Session slot only — do not scavenge the other session's columns.
/// </summary>
public static class PublishedRouteFleet
{
    public static int? VehicleId(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return RouteSession.ToAssignmentSlot(route) == RouteTimeSlot.PM
            ? route.PMVehicleId
            : route.AMVehicleId;
    }

    public static int? DriverId(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return RouteSession.ToAssignmentSlot(route) == RouteTimeSlot.PM
            ? route.PMDriverId
            : route.AMDriverId;
    }

    public static TimeSpan BeginTime(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return RouteSession.ToAssignmentSlot(route) == RouteTimeSlot.PM
            ? route.PMBeginTime ?? TimeSpan.FromHours(15)
            : route.AMBeginTime ?? TimeSpan.FromHours(7);
    }

    public static bool HasPairing(Route route) =>
        VehicleId(route).HasValue && DriverId(route).HasValue;
}
