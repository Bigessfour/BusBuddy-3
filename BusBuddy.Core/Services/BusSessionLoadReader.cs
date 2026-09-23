using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Services;

/// <summary>
/// Seated and wheelchair counts for one bus against one route slot.
/// Same-day not-riding is excluded. Does not write Route.AMVehicleId or PMVehicleId.
/// </summary>
internal static class BusSessionLoadReader
{
    public static async Task<Result<BusSessionLoad>> ReadAsync(
        BusBuddyDbContext context,
        int busId,
        int routeId,
        RouteTimeSlot slot,
        CancellationToken cancellationToken = default)
    {
        if (busId <= 0 || routeId <= 0 || slot == RouteTimeSlot.Both)
        {
            return Result.FailureResult<BusSessionLoad>("Specify a bus, a route, and the AM or PM slot.");
        }

        var bus = await context.Buses.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BusId == busId, cancellationToken)
            .ConfigureAwait(false);
        if (bus is null)
        {
            return Result.FailureResult<BusSessionLoad>($"Bus {busId} was not found.");
        }

        var route = await context.Routes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RouteId == routeId, cancellationToken)
            .ConfigureAwait(false);
        if (route is null)
        {
            return Result.FailureResult<BusSessionLoad>($"Route {routeId} was not found.");
        }

        var day = ServiceDay(route);
        var assigned = await context.Students.AsNoTracking()
            .WhereOnSlot(route.RouteId, route.RouteName, slot)
            .Select(s => new Rider(s.StudentId, s.RequiresWheelchair))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var absent = await context.RouteRiderExceptions.AsNoTracking()
            .Where(e => e.RouteId == routeId && e.ExceptionDate == day)
            .Select(e => e.StudentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var absentIds = absent.ToHashSet();
        var riding = assigned.Where(s => !absentIds.Contains(s.StudentId)).ToList();

        var pairedId = slot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;
        var warnings = new List<string>();
        if (pairedId != busId)
        {
            warnings.Add($"Bus {bus.BusNumber} is not the default bus on this {slot} run.");
        }

        if (bus.SeatingCapacity <= 0)
        {
            warnings.Add($"Bus {bus.BusNumber} has no seating capacity on file.");
        }
        else if (riding.Count > bus.SeatingCapacity)
        {
            warnings.Add(
                $"Seating overflow: {riding.Count} riding, capacity {bus.SeatingCapacity}.");
        }

        var wheelchairUsed = riding.Count(s => s.RequiresWheelchair);
        if (wheelchairUsed > bus.WheelchairStations)
        {
            warnings.Add(
                $"Wheelchair overflow: {wheelchairUsed} riders, {bus.WheelchairStations} stations.");
        }

        var warning = warnings.Count == 0 ? null : string.Join(" ", warnings);
        var load = new BusSessionLoad
        {
            BusId = busId,
            RouteId = routeId,
            Slot = slot,
            SeatedUsed = riding.Count,
            SeatedCapacity = bus.SeatingCapacity,
            WheelchairUsed = wheelchairUsed,
            WheelchairStations = bus.WheelchairStations,
            OverflowWarning = warning
        };
        return warning is null
            ? Result.SuccessResult(load)
            : Result.SuccessResult(load, warning);
    }

    internal static DateTime ServiceDay(Route route)
    {
        var date = route.Date == default ? DateTime.UtcNow : route.Date;
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }

    private readonly record struct Rider(int StudentId, bool RequiresWheelchair);
}
