namespace BusBuddy.Core.Mapping;

/// <summary>
/// Miles and minutes stored on <see cref="Models.Route"/> after a Google Routes refresh.
/// </summary>
public static class RoutePathMetrics
{
    /// <summary>Clerk caption such as <c>12.4 mi · 36 min</c>. Null when neither fact is stored.</summary>
    public static string? Caption(decimal? distanceMiles, int? durationMinutes)
    {
        var miles = distanceMiles is decimal milesValue && milesValue > 0
            ? $"{milesValue:0.0} mi"
            : null;
        var minutes = durationMinutes is int minuteValue && minuteValue > 0
            ? $"{minuteValue} min"
            : null;
        if (miles is null)
        {
            return minutes;
        }

        return minutes is null ? miles : $"{miles} · {minutes}";
    }

    public static string DriveTimeText(int? durationMinutes) =>
        durationMinutes is int minutes && minutes > 0 ? $"{minutes} min" : "—";
}
