using System;
using System.Globalization;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class FuelNumericTextTests
{
    [Test]
    public void IsIntermediateNumber_AllowsTrailingDecimal()
    {
        FuelNumericText.IsIntermediateNumber("6.", CultureInfo.InvariantCulture).Should().BeTrue();
        FuelNumericText.IsIntermediateNumber(".", CultureInfo.InvariantCulture).Should().BeTrue();
        FuelNumericText.IsIntermediateNumber("6.99", CultureInfo.InvariantCulture).Should().BeFalse();
    }

    [Test]
    public void TryParseRounded_RoundsGallonsAndPrice()
    {
        FuelNumericText.TryParseRounded("102.4567", 3, allowBlankAsNull: true, out var gallons, CultureInfo.InvariantCulture)
            .Should().BeTrue();
        gallons.Should().Be(102.457m);

        FuelNumericText.TryParseRounded("6.999", 3, allowBlankAsNull: true, out var price, CultureInfo.InvariantCulture)
            .Should().BeTrue();
        price.Should().Be(6.999m);
    }

    [Test]
    public void TryParseRounded_BlankBecomesNull()
    {
        FuelNumericText.TryParseRounded("  ", 3, allowBlankAsNull: true, out var value, CultureInfo.InvariantCulture)
            .Should().BeTrue();
        value.Should().BeNull();
    }
}

[TestFixture]
[Category("Unit")]
public class FuelRecordValidatorFieldTests
{
    [Test]
    public void GetLocationError_RequiresNonEmpty()
    {
        FuelRecordValidator.GetLocationError("").Should().NotBeNullOrEmpty();
        FuelRecordValidator.GetLocationError("Yard").Should().BeNull();
    }

    [Test]
    public void GetGallonsError_RejectsZeroAndInvalid()
    {
        FuelRecordValidator.GetGallonsError("0", 0m).Should().Contain("greater than zero");
        FuelRecordValidator.GetGallonsError("abc", null).Should().Contain("valid number");
        FuelRecordValidator.GetGallonsError("102", 102m).Should().BeNull();
    }

    [Test]
    public void AllowedFuelTypes_AreGasolineAndDiesel()
    {
        FuelRecordValidator.AllowedFuelTypes.Should().Equal("Gasoline", "Diesel");
    }

    [Test]
    public void ValidateForPersist_StillAcceptsValidRecord()
    {
        var fuel = new Fuel
        {
            FuelDate = new DateTime(2026, 9, 11),
            FuelLocation = "Acme",
            VehicleFueledId = 1,
            VehicleOdometerReading = 1000,
            FuelType = "diesel",
            Gallons = 102m,
            PricePerGallon = 6.99m,
            TotalCost = 712.98m
        };

        Action act = () => FuelRecordValidator.ValidateForPersist(fuel);
        act.Should().NotThrow();
        fuel.FuelType.Should().Be("Diesel");
        fuel.FuelDate.Kind.Should().Be(DateTimeKind.Utc);
    }
}
