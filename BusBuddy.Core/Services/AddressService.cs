using System.Text.RegularExpressions;

namespace BusBuddy.Core.Services;

/// <summary>
/// Address validation service for MVP phase — basic US address validation
/// </summary>
public class AddressService
{
    /// <summary>
    /// Validates a US address format for MVP phase
    /// </summary>
    /// <param name="address">Full address string to validate</param>
    /// <returns>Validation result with success flag and error message</returns>
    public (bool IsValid, string Error) ValidateAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Address is required");
        }

        // MVP Simple validation - check for basic address components
        if (address.Length < 10)
        {
            return (false, "Address too short - please provide complete address");
        }

        // Check for basic address elements (number, street, city)
        if (!ContainsNumber(address))
        {
            return (false, "Address must include a street number");
        }

        if (!ContainsComma(address))
        {
            return (false, "Address should include city/state separation (use comma)");
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Validates individual address components
    /// </summary>
    public (bool IsValid, string Error) ValidateAddressComponents(
        string? street, string? city, string? state, string? zip)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            return (false, "Street address is required");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return (false, "City is required");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return (false, "State is required");
        }

        if (!string.IsNullOrWhiteSpace(state) && state.Length != 2)
        {
            return (false, "State must be 2 characters (e.g., CA, TX, NY)");
        }

        if (!string.IsNullOrWhiteSpace(zip) && !IsValidZip(zip))
        {
            return (false, "Invalid ZIP code format (use 12345 or 12345-6789)");
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Basic ZIP code validation
    /// </summary>
    private static bool IsValidZip(string zip)
    {
        // Support both 5-digit and 9-digit ZIP codes
        var zipPattern = @"^\d{5}(-\d{4})?$";
        return Regex.IsMatch(zip, zipPattern);
    }

    /// <summary>
    /// Check if address contains a street number
    /// </summary>
    private static bool ContainsNumber(string address)
    {
        return Regex.IsMatch(address, @"^\d+");
    }

    /// <summary>
    /// Check if address contains comma separation
    /// </summary>
    private static bool ContainsComma(string address)
    {
        return address.Contains(',');
    }

    /// <summary>
    /// Format address components into a single string
    /// </summary>
    public string FormatAddress(string? street, string? city, string? state, string? zip)
    {
        var components = new List<string>();

        if (!string.IsNullOrWhiteSpace(street))
        {
            components.Add(street.Trim());
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            components.Add(city.Trim());
        }

        if (!string.IsNullOrWhiteSpace(state) && !string.IsNullOrWhiteSpace(zip))
        {
            components.Add($"{state.Trim()} {zip.Trim()}");
        }
        else if (!string.IsNullOrWhiteSpace(state))
        {
            components.Add(state.Trim());
        }

        return string.Join(", ", components);
    }
}
