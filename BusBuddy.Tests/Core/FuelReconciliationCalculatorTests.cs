using System;
using System.Collections.Generic;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class FuelReconciliationCalculatorTests
{
    [Test]
    public void Build_WithoutBulk_HasNoDiscrepancyOrDetails()
    {
        var records = new List<Fuel>
        {
            new()
            {
                FuelId = 1,
                FuelDate = new DateTime(2026, 9, 1),
                VehicleFueledId = 7,
                Gallons = 100m,
                FuelLocation = "Key Pumps",
                VehicleOdometerReading = 1000
            },
            new()
            {
                FuelId = 2,
                FuelDate = new DateTime(2026, 9, 2),
                VehicleFueledId = 7,
                Gallons = 50m,
                FuelLocation = "Key Pumps",
                VehicleOdometerReading = 1200
            }
        };

        var snapshot = FuelReconciliationCalculator.Build(records, bulkStationGallons: null);

        snapshot.HasBulkReading.Should().BeFalse();
        snapshot.VehicleUsageGallons.Should().Be(150m);
        snapshot.DiscrepancyGallons.Should().Be(0m);
        snapshot.Details.Should().BeEmpty();
        snapshot.Daily.Should().HaveCount(2);
        snapshot.Daily[0].VehicleGallons.Should().Be(100m);
    }

    [Test]
    public void Build_WithBulkVariance_ListsFillUpsToVerify()
    {
        var records = new List<Fuel>
        {
            new()
            {
                FuelId = 1,
                FuelDate = new DateTime(2026, 9, 1),
                VehicleFueledId = 3,
                Gallons = 100m,
                FuelLocation = "Yard",
                VehicleOdometerReading = 500
            }
        };

        var buses = new Dictionary<int, string> { [3] = "Bus-03" };
        var snapshot = FuelReconciliationCalculator.Build(
            records,
            bulkStationGallons: 120m,
            busNumbers: buses);

        snapshot.HasBulkReading.Should().BeTrue();
        snapshot.DiscrepancyGallons.Should().Be(20m);
        snapshot.DiscrepancyRatio.Should().BeApproximately(0.20, 0.0001);
        snapshot.Details.Should().ContainSingle();
        snapshot.Details[0].BusNumber.Should().Be("Bus-03");
        snapshot.Details[0].DiscrepancyType.Should().Be("Bulk surplus");
    }

    [Test]
    public void Build_NeverUsesRandomBulkFromVehicleGallons()
    {
        var records = new List<Fuel>
        {
            new()
            {
                FuelId = 1,
                FuelDate = new DateTime(2026, 9, 10),
                VehicleFueledId = 1,
                Gallons = 80m,
                FuelLocation = "A",
                VehicleOdometerReading = 1
            }
        };

        var a = FuelReconciliationCalculator.Build(records, null);
        var b = FuelReconciliationCalculator.Build(records, null);
        a.VehicleUsageGallons.Should().Be(b.VehicleUsageGallons);
        a.Daily[0].VehicleGallons.Should().Be(80m);
        a.HasBulkReading.Should().BeFalse();
    }
}
