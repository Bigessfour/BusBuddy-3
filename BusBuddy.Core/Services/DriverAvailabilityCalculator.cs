using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Derives driver free days from published route Schedule rows and TripEvent loans
/// (busy = any non-cancelled assignment that day). Route != Trip.
/// </summary>
public static class DriverAvailabilityCalculator
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DriverAvailabilityCalculator));

    public static IReadOnlyList<DateTime> AvailableDates(
        IEnumerable<Schedule> schedules,
        int driverId,
        DateTime fromInclusive,
        int dayCount) =>
        AvailableDates(schedules, Array.Empty<TripEvent>(), driverId, fromInclusive, dayCount);

    public static IReadOnlyList<DateTime> AvailableDates(
        IEnumerable<Schedule> schedules,
        IEnumerable<TripEvent> trips,
        int driverId,
        DateTime fromInclusive,
        int dayCount)
    {
        var start = fromInclusive.Date;
        var window = Math.Max(0, dayCount);
        var busy = schedules
            .Where(s => s.DriverId == driverId &&
                        !string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ScheduleDate.Date)
            .Concat(trips
                .Where(t => t.DriverId == driverId &&
                            !string.Equals(t.Status, TripStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
                .Select(t => (t.TripDate == default ? t.LeaveTime : t.TripDate).Date))
            .ToHashSet();

        var available = Enumerable.Range(0, window)
            .Select(offset => start.AddDays(offset))
            .Where(day => !busy.Contains(day))
            .ToList();

        Logger.Debug(
            "Driver {DriverId} availability from {From:yyyy-MM-dd} windowDays={Window} busyDays={Busy} availableDays={Available}",
            driverId, start, window, busy.Count, available.Count);

        return available;
    }
}
