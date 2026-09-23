using BusBuddy.Core.Models;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Clerk map nudge for a student home. Address Validation still proves the street exists.
/// Once the clerk moves the blue pickup pin, that point stays until the street address changes.
/// Clerks do not type lat/lng.
/// </summary>
public static class HomePickupPin
{
    public static string AddressKey(Student student) =>
        string.Join(
            "|",
            new[] { student.HomeAddress, student.City, student.State, student.Zip }
                .Select(part => (part ?? string.Empty).Trim().ToUpperInvariant()));

    public static bool AddressMatches(Student left, Student right) =>
        string.Equals(AddressKey(left), AddressKey(right), StringComparison.Ordinal);

    /// <summary>
    /// Same street: keep a stored clerk pin if the form dropped the flag.
    /// New street: drop the flag so the next geocode can place the new home.
    /// An incoming flag of true is the pin window's latest click — keep those coordinates.
    /// </summary>
    public static void ApplyOnSave(Student incoming, Student? stored)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (stored is not { HomePickupClerkAdjusted: true })
        {
            return;
        }

        if (AddressMatches(incoming, stored))
        {
            if (!incoming.HomePickupClerkAdjusted)
            {
                incoming.Latitude = stored.Latitude;
                incoming.Longitude = stored.Longitude;
                incoming.HomePickupClerkAdjusted = true;
            }

            return;
        }

        incoming.HomePickupClerkAdjusted = false;
        if (incoming.Latitude == stored.Latitude && incoming.Longitude == stored.Longitude)
        {
            incoming.Latitude = null;
            incoming.Longitude = null;
        }
    }
}
