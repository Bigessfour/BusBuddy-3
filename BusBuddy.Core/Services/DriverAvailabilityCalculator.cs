using BusBuddy.Core.Models;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Derives driver free days from Schedule and ActivitySchedule rows (busy = any non-cancelled assignment that day).
/// </summary>
public static class DriverAvailabilityCalculator
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DriverAvailabilityCalculator));

    public static IReadOnlyList<DateTime> AvailableDates(
        IEnumerable<Schedule> schedules,
        int driverId,
        DateTime fromInclusive,
        int dayCount) =>
        AvailableDates(schedules, Array.Empty<ActivitySchedule>(), driverId, fromInclusive, dayCount);

    public static IReadOnlyList<DateTime> AvailableDates(
        IEnumerable<Schedule> schedules,
        IEnumerable<ActivitySchedule> activities,
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
            .Concat(activities
                .Where(a => a.ScheduledDriverId == driverId &&
                            !string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.ScheduledDate.Date))
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
