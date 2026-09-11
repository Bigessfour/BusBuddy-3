using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Shared persist rules for fuel records (dialog + <see cref="FuelService"/>).
/// Lengths match <see cref="FuelConstraints"/> and EF Fuel configuration.
/// </summary>
public static class FuelRecordValidator
{
    private static readonly HashSet<string> AllowedFuelTypeSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gasoline",
        "Diesel"
    };

    /// <summary>Canonical fuel type labels for closed ComboBox lists.</summary>
    public static IReadOnlyList<string> AllowedFuelTypes { get; } = new[] { "Gasoline", "Diesel" };

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

        var locationError = GetLocationError(fuel.FuelLocation);
        if (locationError != null)
        {
            throw new ArgumentException(locationError, nameof(fuel));
        }

        fuel.FuelLocation = fuel.FuelLocation!.Trim();

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

        if (!AllowedFuelTypeSet.Contains(fuelType))
        {
            throw new ArgumentException(
                "Fuel type must be Gasoline or Diesel.",
                nameof(fuel));
        }

        fuel.FuelType = AllowedFuelTypes.First(t =>
            string.Equals(t, fuelType, StringComparison.OrdinalIgnoreCase));

        var gallonsError = GetGallonsError(text: null, gallons: fuel.Gallons);
        if (gallonsError != null)
        {
            throw new ArgumentException(gallonsError, nameof(fuel));
        }

        if (fuel.Gallons!.Value > 9999.999m)
        {
            throw new ArgumentException("Gallons exceeds the allowed maximum.", nameof(fuel));
        }

        var priceError = GetPriceError(text: null, price: fuel.PricePerGallon);
        if (priceError != null)
        {
            throw new ArgumentException(priceError, nameof(fuel));
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

    public static string? GetLocationError(string? location)
    {
        var trimmed = location?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Location is required.";
        }

        if (trimmed.Length > FuelConstraints.MaxLocationLength)
        {
            return $"Max {FuelConstraints.MaxLocationLength} characters.";
        }

        return null;
    }

    public static string? GetBusError(bool busSelected) =>
        busSelected ? null : "Select a bus.";

    public static string? GetGallonsError(string? text, decimal? gallons)
    {
        if (text != null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Gallons are required.";
            }

            if (FuelNumericText.IsIntermediateNumber(text))
            {
                return null;
            }

            if (!FuelNumericText.TryParseDecimal(text, out _))
            {
                return "Enter a valid number (e.g. 102 or 102.5).";
            }
        }

        if (!gallons.HasValue || gallons.Value <= 0)
        {
            return "Gallons must be greater than zero.";
        }

        return null;
    }

    public static string? GetPriceError(string? text, decimal? price)
    {
        if (text != null
            && !string.IsNullOrWhiteSpace(text)
            && !FuelNumericText.IsIntermediateNumber(text)
            && !FuelNumericText.TryParseDecimal(text, out _))
        {
            return "Enter a valid price (e.g. 6.99).";
        }

        if (price.HasValue && price.Value < 0)
        {
            return "Price cannot be negative.";
        }

        return null;
    }

    public static string? GetOdometerError(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || FuelNumericText.IsIntermediateNumber(text))
        {
            return null;
        }

        return int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out _)
            ? null
            : "Enter a whole number for odometer.";
    }
}
