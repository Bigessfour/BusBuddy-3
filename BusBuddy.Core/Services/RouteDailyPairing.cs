using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Effective bus/driver for a published route session on <see cref="Route.Date"/>.
/// Year defaults stay on <c>Route.AM*</c>/<c>PM*</c>; dated substitutes live on <see cref="Schedule"/>.
/// </summary>
public static class RouteDailyPairing
{
    public readonly record struct Pairing(int? BusId, int? DriverId, bool FromSchedule);

    /// <summary>Noon boundary: AM departures are before this; PM are at/after.</summary>
    public static readonly TimeSpan SessionSplit = TimeSpan.FromHours(12);

    /// <summary>UTC calendar day used for sheet print and rider exceptions.</summary>
    public static DateTime ServiceDateUtc(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var day = route.Date == default ? DateTime.UtcNow.Date : route.Date.Date;
        return DateTime.SpecifyKind(day, DateTimeKind.Utc);
    }

    /// <summary>
    /// Prefer a non-cancelled <see cref="Schedule"/> for the route on <see cref="ServiceDateUtc"/>;
    /// otherwise the year-default AM/PM FKs. Never mutates <see cref="Route"/>.
    /// Stays inside the requested session window so a newer PM spare cannot win an AM resolve.
    /// </summary>
    public static Pairing Resolve(
        Route route,
        RouteTimeSlot slot,
        IEnumerable<Schedule>? schedulesForRouteDate)
    {
        ArgumentNullException.ThrowIfNull(route);

        var day = ServiceDateUtc(route).Date;
        var candidates = (schedulesForRouteDate ?? Array.Empty<Schedule>())
            .Where(s =>
                s.RouteId == route.RouteId
                && s.ScheduleDate.Date == day
                && !string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var preferredBegin = slot == RouteTimeSlot.PM
            ? route.PMBeginTime
            : route.AMBeginTime;

        var scoped = candidates.Where(s => MatchesSessionWindow(s.DepartureTime.TimeOfDay, slot)).ToList();

        // Newest exact clock, else nearest clock in-session, else newest in-session.
        Schedule? match = null;
        if (preferredBegin is TimeSpan begin)
        {
            match = Newest(scoped.Where(s => s.DepartureTime.TimeOfDay == begin));
            match ??= NearestThenNewest(scoped, begin);
        }

        match ??= Newest(scoped);
        if (match is not null)
        {
            return new Pairing(match.BusId, match.DriverId, FromSchedule: true);
        }

        if (slot == RouteTimeSlot.PM)
        {
            return new Pairing(route.PMVehicleId, route.PMDriverId, FromSchedule: false);
        }

        // AM and Both fall back to AM year-default pair.
        return new Pairing(route.AMVehicleId, route.AMDriverId, FromSchedule: false);
    }

    /// <summary>
    /// Builds a <see cref="Schedule"/> row for a dated substitute/spare without touching Route FKs.
    /// </summary>
    public static Schedule BuildSessionRow(
        Route route,
        RouteTimeSlot slot,
        int busId,
        int driverId)
    {
        ArgumentNullException.ThrowIfNull(route);

        var day = ServiceDateUtc(route);
        var begin = slot == RouteTimeSlot.PM
            ? route.PMBeginTime ?? TimeSpan.FromHours(14)
            : route.AMBeginTime ?? TimeSpan.FromHours(7);
        var departure = day.Add(begin);
        var arrival = departure.AddMinutes(route.EstimatedDuration ?? 45);
        if (arrival <= departure)
        {
            arrival = departure.AddMinutes(45);
        }

        var session = slot == RouteTimeSlot.PM ? "PM" : "AM";
        return new Schedule
        {
            RouteId = route.RouteId,
            BusId = busId,
            DriverId = driverId,
            ScheduleDate = day,
            DepartureTime = departure,
            ArrivalTime = arrival,
            Location = route.School,
            Notes = $"Substitute {session} for {route.RouteName} on {day:yyyy-MM-dd}",
            Status = "Scheduled",
            CreatedDate = DateTime.UtcNow
        };
    }

    /// <summary>True when <paramref name="departureClock"/> belongs to the AM or PM session window.</summary>
    public static bool MatchesSessionWindow(TimeSpan departureClock, RouteTimeSlot slot) =>
        slot == RouteTimeSlot.PM
            ? departureClock >= SessionSplit
            : departureClock < SessionSplit;

    private static Schedule? Newest(IEnumerable<Schedule> rows) =>
        rows
            .OrderByDescending(s => s.UpdatedDate ?? s.CreatedDate)
            .ThenByDescending(s => s.ScheduleId)
            .FirstOrDefault();

    private static Schedule? NearestThenNewest(IEnumerable<Schedule> rows, TimeSpan preferredBegin) =>
        rows
            .OrderBy(s => Math.Abs((s.DepartureTime.TimeOfDay - preferredBegin).Ticks))
            .ThenByDescending(s => s.UpdatedDate ?? s.CreatedDate)
            .ThenByDescending(s => s.ScheduleId)
            .FirstOrDefault();
}
