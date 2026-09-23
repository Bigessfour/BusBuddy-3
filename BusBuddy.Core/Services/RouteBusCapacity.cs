using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Services;

/// <summary>
/// Allow or block one more rider against a <see cref="BusSessionLoad"/> from IBusService.GetSessionLoadAsync.
/// </summary>
public static class RouteBusCapacity
{
    public readonly record struct Check(bool Blocked, string? Message, string? Warning);

    public static async Task<Check> ForCandidateAsync(
        BusBuddyDbContext context,
        Route route,
        RouteTimeSlot slot,
        int candidateStudentId,
        bool candidateRequiresWheelchair,
        bool overrideSeating,
        CancellationToken cancellationToken = default)
    {
        var vehicleId = slot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;
        if (vehicleId is not int busId)
        {
            return new Check(
                false,
                null,
                $"No default bus is assigned for this {slot} run, so seating capacity is unknown.");
        }

        var load = await BusSessionLoadReader.ReadAsync(context, busId, route.RouteId, slot, cancellationToken)
            .ConfigureAwait(false);
        if (!load.IsSuccess || load.Value is null)
        {
            return new Check(true, load.Error, null);
        }

        var day = BusSessionLoadReader.ServiceDay(route);
        var alreadyRiding = await context.Students.AsNoTracking()
            .Where(s => s.StudentId == candidateStudentId)
            .WhereOnSlot(route.RouteId, route.RouteName, slot)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (alreadyRiding)
        {
            var absent = await context.RouteRiderExceptions.AsNoTracking()
                .AnyAsync(
                    e => e.RouteId == route.RouteId && e.StudentId == candidateStudentId && e.ExceptionDate == day,
                    cancellationToken)
                .ConfigureAwait(false);
            alreadyRiding = !absent;
        }

        return ForCandidate(load.Value, alreadyRiding, candidateRequiresWheelchair, overrideSeating);
    }

    /// <summary>One more rider on a snapshot that already excludes same-day not-riding.</summary>
    public static Check ForCandidate(
        BusSessionLoad load,
        bool alreadyRiding,
        bool candidateRequiresWheelchair,
        bool overrideSeating)
    {
        if (load.SeatedCapacity <= 0)
        {
            return new Check(
                false,
                null,
                $"No default bus is assigned for this {load.Slot} run, so seating capacity is unknown.");
        }

        var ridingCount = load.SeatedUsed + (alreadyRiding ? 0 : 1);
        var wheelchairCount = load.WheelchairUsed + (!alreadyRiding && candidateRequiresWheelchair ? 1 : 0);
        var parts = new List<string>();
        if (ridingCount > load.SeatedCapacity)
        {
            parts.Add(
                $"Seating capacity {load.SeatedCapacity} would be exceeded ({ridingCount - 1} already riding)");
        }

        if (wheelchairCount > load.WheelchairStations)
        {
            parts.Add(
                $"Wheelchair capacity {load.WheelchairStations} stations would be exceeded ({wheelchairCount} wheelchair riders)");
        }

        if (parts.Count == 0)
        {
            return new Check(false, null, null);
        }

        var detail = string.Join("; ", parts);
        if (overrideSeating)
        {
            return new Check(false, detail + " (override recorded)", null);
        }

        return new Check(true, detail, null);
    }
}
