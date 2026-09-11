using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Shared persist rules for fuel records (dialog + <see cref="FuelService"/>).
/// Lengths match <see cref="FuelConstraints"/> and EF Fuel configuration.
/// </summary>
public static class FuelRecordValidator
{
    private static readonly HashSet<string> AllowedFuelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gasoline",
        "Diesel"
    };

    /// <summary>Throws <see cref="ArgumentException"/> when the record cannot be saved.</summary>
    public static void ValidateForPersist(Fuel fuel)
    {
        ArgumentNullException.ThrowIfNull(fuel);

        // Date-only calendar day at midnight UTC (matches Postgres timestamptz + app UTC habit).
        fuel.FuelDate = DateTime.SpecifyKind(fuel.FuelDate.Date, DateTimeKind.Utc);

        if (fuel.VehicleFueledId <= 0)
        {
            throw new ArgumentException("A bus must be selected.", nameof(fuel));
        }

        var location = fuel.FuelLocation?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("Fuel location is required.", nameof(fuel));
        }

        if (location.Length > FuelConstraints.MaxLocationLength)
        {
            throw new ArgumentException(
                $"Fuel location cannot exceed {FuelConstraints.MaxLocationLength} characters.",
                nameof(fuel));
        }

        fuel.FuelLocation = location;

        var fuelType = fuel.FuelType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fuelType))
        {
            throw new ArgumentException("Fuel type is required.", nameof(fuel));
        }

        if (fuelType.Length > FuelConstraints.MaxFuelTypeLength)
        {
            throw new ArgumentException(
                $"Fuel type cannot exceed {FuelConstraints.MaxFuelTypeLength} characters.",
                nameof(fuel));
        }

        if (!AllowedFuelTypes.Contains(fuelType))
        {
            throw new ArgumentException(
                "Fuel type must be Gasoline or Diesel.",
                nameof(fuel));
        }

        fuel.FuelType = AllowedFuelTypes.First(t =>
            string.Equals(t, fuelType, StringComparison.OrdinalIgnoreCase));

        if (!fuel.Gallons.HasValue || fuel.Gallons.Value <= 0)
        {
            throw new ArgumentException("Gallons must be greater than zero.", nameof(fuel));
        }

        if (fuel.Gallons.Value > 9999.999m)
        {
            throw new ArgumentException("Gallons exceeds the allowed maximum.", nameof(fuel));
        }

        if (fuel.PricePerGallon.HasValue && fuel.PricePerGallon.Value < 0)
        {
            throw new ArgumentException("Price per gallon cannot be negative.", nameof(fuel));
        }

        if (fuel.TotalCost.HasValue && fuel.TotalCost.Value < 0)
        {
            throw new ArgumentException("Total cost cannot be negative.", nameof(fuel));
        }

        if (fuel.VehicleOdometerReading < 0)
        {
            throw new ArgumentException("Odometer reading cannot be negative.", nameof(fuel));
        }

        if (fuel.Notes is { Length: > FuelConstraints.MaxNotesLength })
        {
            throw new ArgumentException(
                $"Notes cannot exceed {FuelConstraints.MaxNotesLength} characters.",
                nameof(fuel));
        }
    }
}
