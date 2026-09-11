using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Shared persist rules for maintenance records (grid + <see cref="MaintenanceService"/>).
/// Lengths match <see cref="MaintenanceConstraints"/> and EF Maintenance configuration.
/// </summary>
public static class MaintenanceRecordValidator
{
    private static readonly HashSet<string> AllowedStatusSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "Scheduled",
        "In Progress",
        "Completed"
    };

    private static readonly HashSet<string> AllowedPrioritySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low",
        "Normal",
        "High",
        "Emergency"
    };

    /// <summary>Canonical status labels for closed ComboBox lists.</summary>
    public static IReadOnlyList<string> AllowedStatuses { get; } =
        new[] { "Scheduled", "In Progress", "Completed" };

    /// <summary>Canonical priority labels for closed ComboBox lists.</summary>
    public static IReadOnlyList<string> AllowedPriorities { get; } =
        new[] { "Low", "Normal", "High", "Emergency" };

    /// <summary>Throws <see cref="ArgumentException"/> when the record cannot be saved.</summary>
    public static void ValidateForPersist(Maintenance maintenance)
    {
        ArgumentNullException.ThrowIfNull(maintenance);

        // Date-only calendar day at midnight UTC (matches Postgres timestamptz + app UTC habit).
        maintenance.Date = DateTime.SpecifyKind(maintenance.Date.Date, DateTimeKind.Utc);

        if (maintenance.VehicleId <= 0)
        {
            throw new ArgumentException("A bus must be selected.", nameof(maintenance));
        }

        var workError = GetWorkError(maintenance.MaintenanceCompleted);
        if (workError != null)
        {
            throw new ArgumentException(workError, nameof(maintenance));
        }

        maintenance.MaintenanceCompleted = maintenance.MaintenanceCompleted.Trim();

        var vendorError = GetVendorError(maintenance.Vendor);
        if (vendorError != null)
        {
            throw new ArgumentException(vendorError, nameof(maintenance));
        }

        maintenance.Vendor = maintenance.Vendor.Trim();

        if (maintenance.OdometerReading < 0)
        {
            throw new ArgumentException("Odometer reading cannot be negative.", nameof(maintenance));
        }

        if (maintenance.RepairCost < 0)
        {
            throw new ArgumentException("Repair cost cannot be negative.", nameof(maintenance));
        }

        var status = maintenance.Status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required.", nameof(maintenance));
        }

        if (!AllowedStatusSet.Contains(status))
        {
            throw new ArgumentException(
                "Status must be Scheduled, In Progress, or Completed.",
                nameof(maintenance));
        }

        maintenance.Status = AllowedStatuses.First(s =>
            string.Equals(s, status, StringComparison.OrdinalIgnoreCase));

        var priority = maintenance.Priority?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException("Priority is required.", nameof(maintenance));
        }

        if (!AllowedPrioritySet.Contains(priority))
        {
            throw new ArgumentException(
                "Priority must be Low, Normal, High, or Emergency.",
                nameof(maintenance));
        }

        maintenance.Priority = AllowedPriorities.First(p =>
            string.Equals(p, priority, StringComparison.OrdinalIgnoreCase));

        if (maintenance.Notes is { Length: > MaintenanceConstraints.MaxNotesLength })
        {
            throw new ArgumentException(
                $"Notes cannot exceed {MaintenanceConstraints.MaxNotesLength} characters.",
                nameof(maintenance));
        }

        if (maintenance.Description is { Length: > MaintenanceConstraints.MaxDescriptionLength })
        {
            throw new ArgumentException(
                $"Description cannot exceed {MaintenanceConstraints.MaxDescriptionLength} characters.",
                nameof(maintenance));
        }
    }

    public static string? GetWorkError(string? work)
    {
        var trimmed = work?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Work completed is required.";
        }

        if (trimmed.Length > MaintenanceConstraints.MaxWorkLength)
        {
            return $"Max {MaintenanceConstraints.MaxWorkLength} characters.";
        }

        return null;
    }

    public static string? GetVendorError(string? vendor)
    {
        var trimmed = vendor?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Vendor is required.";
        }

        if (trimmed.Length > MaintenanceConstraints.MaxVendorLength)
        {
            return $"Max {MaintenanceConstraints.MaxVendorLength} characters.";
        }

        return null;
    }
}
