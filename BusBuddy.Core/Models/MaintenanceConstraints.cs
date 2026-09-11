namespace BusBuddy.Core.Models;

/// <summary>Length / domain limits for <see cref="Maintenance"/> — keep in sync with EF config.</summary>
public static class MaintenanceConstraints
{
    public const int MaxWorkLength = 100;
    public const int MaxVendorLength = 100;
    public const int MaxStatusLength = 20;
    public const int MaxPriorityLength = 20;
    public const int MaxNotesLength = 1000;
    public const int MaxDescriptionLength = 500;
}
