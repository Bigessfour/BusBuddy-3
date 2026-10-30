using System.Globalization;

namespace BusBuddy.Core.Services.Trips;

/// <summary>
/// Ticket # on the trip board is a number. The next ticket is one past the
/// highest number already stored, including a suffix such as 319103486-Sat-24Oct.
/// </summary>
public static class TripTicketNumbers
{
    public static string Next(IEnumerable<string?> existing)
    {
        long max = 0;
        foreach (var ticket in existing)
        {
            if (TryLeadingNumber(ticket, out var number) && number > max)
            {
                max = number;
            }
        }

        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryLeadingNumber(string? ticket, out long number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        var end = 0;
        while (end < ticket.Length && char.IsDigit(ticket[end]))
        {
            end++;
        }

        return end > 0
               && long.TryParse(ticket.AsSpan(0, end), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
