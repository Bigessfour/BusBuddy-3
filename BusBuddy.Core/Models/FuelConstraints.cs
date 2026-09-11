namespace BusBuddy.Core.Models;

/// <summary>Length / domain limits for <see cref="Fuel"/> — keep in sync with EF Fuel config.</summary>
public static class FuelConstraints
{
    public const int MaxLocationLength = 100;
    public const int MaxFuelTypeLength = 20;
    public const int MaxNotesLength = 500;
}
