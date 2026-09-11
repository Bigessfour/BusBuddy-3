using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// UTC timestamp normalization for <see cref="Schedule"/> persist
/// (Postgres <c>timestamp with time zone</c>).
/// </summary>
public static class ScheduleTimestampNormalizer
{
    /// <summary>
    /// Coerce ScheduleDate / DepartureTime / ArrivalTime to UTC.
    /// Unspecified values are treated as already-UTC wall clock (Fuel/Maintenance habit).
    /// </summary>
    public static void NormalizeForPersist(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        schedule.ScheduleDate = AsUtcDate(schedule.ScheduleDate);
        schedule.DepartureTime = AsUtc(schedule.DepartureTime);
        schedule.ArrivalTime = AsUtc(schedule.ArrivalTime);
        schedule.CreatedDate = AsUtc(schedule.CreatedDate);
        if (schedule.UpdatedDate.HasValue)
        {
            schedule.UpdatedDate = AsUtc(schedule.UpdatedDate.Value);
        }
    }

    public static DateTime AsUtcDate(DateTime value) =>
        DateTime.SpecifyKind(AsUtc(value).Date, DateTimeKind.Utc);

    public static DateTime AsUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
