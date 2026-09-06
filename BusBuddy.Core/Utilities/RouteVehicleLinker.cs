using BusBuddy.Core.Models;

namespace BusBuddy.Core.Utilities;

/// <summary>Keeps <see cref="Route.BusNumber"/> aligned with AM/PM vehicle FKs.</summary>
public static class RouteVehicleLinker
{
    public static bool IsAssignableStatus(string? status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "InService", StringComparison.OrdinalIgnoreCase);

    public static void Apply(Route route, Bus bus, RouteTimeSlot timeSlot = RouteTimeSlot.Both)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(bus);

        switch (timeSlot)
        {
            case RouteTimeSlot.AM:
                route.AMVehicleId = bus.BusId;
                break;
            case RouteTimeSlot.PM:
                route.PMVehicleId = bus.BusId;
                break;
            default:
                route.AMVehicleId = bus.BusId;
                route.PMVehicleId = bus.BusId;
                break;
        }

        route.BusNumber = bus.BusNumber;
    }

    public static bool TrySyncFromBusNumber(Route route, IEnumerable<Bus> buses)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (buses is null || string.IsNullOrWhiteSpace(route.BusNumber))
        {
            return false;
        }

        var match = buses.FirstOrDefault(b =>
            string.Equals(b.BusNumber, route.BusNumber, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        // School / special-needs grid saves also persist BusNumber. Do not collapse
        // a split AM/PM assignment onto one bus unless the user picked a different bus.
        if (route.AMVehicleId.HasValue
            && route.PMVehicleId.HasValue
            && route.AMVehicleId != route.PMVehicleId
            && (route.AMVehicleId == match.BusId || route.PMVehicleId == match.BusId))
        {
            return false;
        }

        Apply(route, match, RouteTimeSlot.Both);
        return true;
    }
}
