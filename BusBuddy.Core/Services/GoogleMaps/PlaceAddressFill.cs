namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Fills missing US city/state/ZIP from a Places formatted line when addressComponents omitted them.
/// </summary>
public static class PlaceAddressFill
{
    public static void FillMissing(
        ref string? street,
        ref string? city,
        ref string? state,
        ref string? zip,
        string? formatted)
    {
        if (string.IsNullOrWhiteSpace(formatted))
        {
            return;
        }

        var parts = formatted
            .Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToList();
        if (parts.Count == 0)
        {
            return;
        }

        if (parts[^1].Equals("USA", StringComparison.OrdinalIgnoreCase)
            || parts[^1].Equals("United States", StringComparison.OrdinalIgnoreCase)
            || parts[^1].Equals("US", StringComparison.OrdinalIgnoreCase))
        {
            parts.RemoveAt(parts.Count - 1);
        }

        if (parts.Count == 0)
        {
            return;
        }

        var regionBits = parts[^1]
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(state)
            && regionBits.Length >= 1
            && regionBits[0].Length == 2
            && regionBits[0].All(char.IsLetter))
        {
            state = regionBits[0].ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(zip) && regionBits.Length >= 2)
        {
            zip = regionBits[1];
        }

        if (string.IsNullOrWhiteSpace(city) && parts.Count >= 2)
        {
            city = parts[^2];
        }

        if (string.IsNullOrWhiteSpace(street) && parts.Count >= 1)
        {
            street = parts[0];
        }
    }
}
