namespace BusBuddy.Core.Models;

/// <summary>
/// Shared gate for assigning a driver to a student-carrying route or trip (#114 / specs/drivers.md).
/// Class A or B, school-bus endorsement, Active, training complete, and unexpired license.
/// </summary>
public static class DriverStudentRouteEligibility
{
    /// <summary>
    /// Returns false with a lowercase reason fragment for
    /// <c>Driver is not qualified for assignment: {reason}</c> style messages.
    /// </summary>
    public static bool TryGetAssignmentBlockReason(Driver driver, out string reasonFragment)
    {
        ArgumentNullException.ThrowIfNull(driver);

        if (!string.Equals(driver.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            reasonFragment = "inactive status";
            return true;
        }

        if (!driver.TrainingComplete)
        {
            reasonFragment = "training incomplete";
            return true;
        }

        if (!driver.LicenseExpiryDate.HasValue)
        {
            reasonFragment = "license expiry not set";
            return true;
        }

        if (driver.LicenseExpiryDate.Value.Date < DateTime.Today)
        {
            reasonFragment = "expired license";
            return true;
        }

        if (!IsClassAOrB(driver.LicenseClass))
        {
            reasonFragment = "CDL class must be A or B";
            return true;
        }

        if (!driver.SchoolBusEndorsement)
        {
            reasonFragment = "missing school-bus endorsement";
            return true;
        }

        reasonFragment = string.Empty;
        return false;
    }

    public static bool MeetsAssignmentEligibility(Driver driver) =>
        !TryGetAssignmentBlockReason(driver, out _);

    public static bool IsClassAOrB(string? licenseClass)
    {
        if (string.IsNullOrWhiteSpace(licenseClass))
        {
            return false;
        }

        var compact = new string(licenseClass.Where(char.IsLetterOrDigit).ToArray());
        return compact.Equals("A", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("B", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("ClassA", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("ClassB", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("CDLA", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("CDLB", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Conservative backfill helper for migration / one-time import: treat S / school-bus text as true.
    /// Assignment still reads <see cref="Driver.SchoolBusEndorsement"/> only.
    /// </summary>
    public static bool LooksLikeSchoolBusEndorsementText(string? endorsements)
    {
        if (string.IsNullOrWhiteSpace(endorsements))
        {
            return false;
        }

        var text = endorsements.Trim();
        if (text.Equals("S", StringComparison.OrdinalIgnoreCase)
            || text.Equals("School", StringComparison.OrdinalIgnoreCase)
            || text.Contains("school bus", StringComparison.OrdinalIgnoreCase)
            || text.Contains("school-bus", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var part in text.Split([',', ';', '/', ' ', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("S", StringComparison.OrdinalIgnoreCase)
                || part.Equals("SB", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
