using System;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class FuelRecordValidatorTests
{
    private static Fuel ValidFuel() => new()
    {
        FuelDate = new DateTime(2026, 9, 10),
        FuelLocation = "Acme Fuel Bid",
        VehicleFueledId = 1,
        VehicleOdometerReading = 1000,
        FuelType = "Diesel",
        Gallons = 102m,
        PricePerGallon = 6.99m,
        TotalCost = 712.98m
    };

    [Test]
    public void ValidateForPersist_AcceptsValidRecord()
    {
        var fuel = ValidFuel();
        Action act = () => FuelRecordValidator.ValidateForPersist(fuel);
        act.Should().NotThrow();
        fuel.FuelType.Should().Be("Diesel");
        fuel.FuelDate.Kind.Should().Be(DateTimeKind.Utc);
        fuel.FuelDate.Should().Be(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ValidateForPersist_RequiresGallonsGreaterThanZero()
    {
        var fuel = ValidFuel();
        fuel.Gallons = 0;
        Action act = () => FuelRecordValidator.ValidateForPersist(fuel);
        act.Should().Throw<ArgumentException>().WithMessage("*Gallons*");
    }

    [Test]
    public void ValidateForPersist_EnforcesLocationMaxLength()
    {
        var fuel = ValidFuel();
        fuel.FuelLocation = new string('x', FuelConstraints.MaxLocationLength + 1);
        Action act = () => FuelRecordValidator.ValidateForPersist(fuel);
        act.Should().Throw<ArgumentException>().WithMessage("*location*");
    }

    [Test]
    public void ValidateForPersist_RejectsUnknownFuelType()
    {
        var fuel = ValidFuel();
        fuel.FuelType = "Kerosene";
        Action act = () => FuelRecordValidator.ValidateForPersist(fuel);
        act.Should().Throw<ArgumentException>().WithMessage("*Gasoline or Diesel*");
    }

    [Test]
    public void FuelConstraints_LocationMatchesEfHundred()
    {
        FuelConstraints.MaxLocationLength.Should().Be(100);
        FuelConstraints.MaxFuelTypeLength.Should().Be(20);
        FuelConstraints.MaxNotesLength.Should().Be(500);
    }
}
