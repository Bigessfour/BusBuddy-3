using System.Globalization;

namespace BusBuddy.Core.Services.Trips;

/// <summary>
/// Calendar chip for a trip. The board grid keeps the full row; the scheduler shows date, place, and driver.
/// </summary>
public static class TripCalendarDisplay
{
    public enum Tone
    {
        /// <summary>Green. Driver is assigned, or the date is more than 14 days out.</summary>
        Scheduled,

        /// <summary>Yellow. No driver, and the trip date is 8–14 days from today.</summary>
        DriverDueWithinTwoWeeks,

        /// <summary>Red. No driver, and the trip date is within 7 days, including overdue dates.</summary>
        DriverDueWithinOneWeek
    }

    public static string Subject(DateTime tripDate, string? location, string? driverName)
    {
        var date = tripDate == default
            ? "Date TBD"
            : tripDate.ToString("M/d", CultureInfo.InvariantCulture);
        var place = string.IsNullOrWhiteSpace(location) ? "Location TBD" : location.Trim();
        var driver = string.IsNullOrWhiteSpace(driverName) ? "No driver" : driverName.Trim();
        return $"{date} · {place} · {driver}";
    }

    public static Tone Resolve(DateTime tripDate, bool hasDriver, DateTime today)
    {
        if (hasDriver)
        {
            return Tone.Scheduled;
        }

        var daysUntil = (tripDate.Date - today.Date).Days;
        if (daysUntil <= 7)
        {
            return Tone.DriverDueWithinOneWeek;
        }

        if (daysUntil <= 14)
        {
            return Tone.DriverDueWithinTwoWeeks;
        }

        return Tone.Scheduled;
    }
}
