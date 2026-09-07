namespace BusBuddy.Core.Models.Trips;

/// <summary>
/// Board / clerk trip status. Stored on <see cref="TripEvent.Status"/>.
/// </summary>
public static class TripStatus
{
    public const string MissingInfo = "MissingInfo";
    public const string Draft = "Draft";
    public const string Scheduled = "Draft";
    public const string Assigned = "Assigned";
    public const string Confirmed = "Confirmed";
    public const string Changed = "Changed";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";

    public static readonly string[] All =
    {
        MissingInfo, Draft, Assigned, Confirmed, Changed, Cancelled, Completed
    };

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Draft;
        }

        if (status.Equals("Scheduled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Scheduled/Draft", StringComparison.OrdinalIgnoreCase))
        {
            return Draft;
        }

        foreach (var known in All)
        {
            if (string.Equals(known, status, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return Draft;
    }
}
