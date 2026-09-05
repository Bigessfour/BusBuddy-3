using System.Text.RegularExpressions;

namespace BusBuddy.WPF.Utilities;

/// <summary>Local US address format checks when Google Maps is unavailable.</summary>
public static class StudentAddressValidator
{
    private static readonly HashSet<string> ValidStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
        "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
        "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
        "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
        "DC",
    };

    public static (bool IsValid, string? ErrorMessage) ValidateComponents(
        string street,
        string city,
        string state,
        string zipCode)
    {
        var validationMessages = new List<string>();

        if (string.IsNullOrWhiteSpace(street))
        {
            validationMessages.Add("Street address is required");
        }
        else if (!Regex.IsMatch(street.Trim(), @"^\d+\s+[\w\s\.,#-]+$"))
        {
            validationMessages.Add("Street address must start with a number followed by street name");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            validationMessages.Add("City is required");
        }
        else if (!Regex.IsMatch(city.Trim(), @"^[A-Za-z\s\.-]+$"))
        {
            validationMessages.Add("City name can only contain letters, spaces, periods, and hyphens");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            validationMessages.Add("State is required");
        }
        else if (!ValidStates.Contains(state.Trim()))
        {
            validationMessages.Add("State must be a valid 2-letter US state abbreviation");
        }

        if (string.IsNullOrWhiteSpace(zipCode))
        {
            validationMessages.Add("ZIP code is required");
        }
        else if (!Regex.IsMatch(zipCode.Trim(), @"^\d{5}(-\d{4})?$"))
        {
            validationMessages.Add("ZIP code must be 5 digits or 5+4 format (e.g., 12345 or 12345-6789)");
        }

        return validationMessages.Count > 0
            ? (false, string.Join("; ", validationMessages))
            : (true, null);
    }
}
