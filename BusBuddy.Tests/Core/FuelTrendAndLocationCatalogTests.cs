using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class FuelTrendAggregatorTests
{
    [Test]
    public void AggregateMonthly_ComputesGallonsAndConsecutiveTripMpg()
    {
        var records = new List<Fuel>
        {
            new()
            {
                FuelId = 1,
                VehicleFueledId = 10,
                FuelDate = new DateTime(2026, 3, 1),
                VehicleOdometerReading = 1000,
                Gallons = 50,
                TotalCost = 150,
                FuelLocation = "Vendor A"
            },
            new()
            {
                FuelId = 2,
                VehicleFueledId = 10,
                FuelDate = new DateTime(2026, 3, 15),
                VehicleOdometerReading = 1400,
                Gallons = 50,
                TotalCost = 160,
                FuelLocation = "Vendor A"
            },
            new()
            {
                FuelId = 3,
                VehicleFueledId = 10,
                FuelDate = new DateTime(2026, 4, 2),
                VehicleOdometerReading = 1800,
                Gallons = 40,
                TotalCost = 140,
                FuelLocation = "Vendor A"
            }
        };

        var trends = FuelTrendAggregator.AggregateMonthly(records);

        trends.Should().HaveCount(2);
        trends[0].Period.Should().Be(new DateTime(2026, 3, 1));
        trends[0].TotalGallons.Should().Be(100m);
        trends[0].FillCount.Should().Be(2);
        trends[0].AvgMpg.Should().Be(8.0); // 400 miles / 50 gal
        trends[0].TripMpgSampleCount.Should().Be(1);

        trends[1].Period.Should().Be(new DateTime(2026, 4, 1));
        trends[1].TotalGallons.Should().Be(40m);
        trends[1].AvgMpg.Should().Be(10.0); // 400 miles / 40 gal
    }

    [Test]
    public void AggregateMonthly_IgnoresImplausibleMpgAndZeroGallons()
    {
        var records = new List<Fuel>
        {
            new()
            {
                FuelId = 1,
                VehicleFueledId = 1,
                FuelDate = new DateTime(2026, 1, 1),
                VehicleOdometerReading = 100,
                Gallons = 10,
                FuelLocation = "X"
            },
            new()
            {
                FuelId = 2,
                VehicleFueledId = 1,
                FuelDate = new DateTime(2026, 1, 2),
                VehicleOdometerReading = 5000, // would be 490 MPG — discarded
                Gallons = 10,
                FuelLocation = "X"
            },
            new()
            {
                FuelId = 3,
                VehicleFueledId = 1,
                FuelDate = new DateTime(2026, 1, 3),
                VehicleOdometerReading = 5050,
                Gallons = 0,
                FuelLocation = "X"
            }
        };

        var trends = FuelTrendAggregator.AggregateMonthly(records);
        trends.Should().HaveCount(1);
        trends[0].TotalGallons.Should().Be(20m);
        trends[0].AvgMpg.Should().BeNaN();
        trends[0].TripMpgSampleCount.Should().Be(0);
    }
}

[TestFixture]
[Category("Unit")]
public class FuelLocationCatalogTests
{
    [Test]
    public async Task RememberAsync_PersistsNewVendorAcrossReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-fuel-loc-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new UserSettingsService(path);
            var fuel = new StubFuelService(["Existing Pump"]);
            var catalog = new FuelLocationCatalog(settings, fuel);

            await catalog.RememberAsync("Acme Fuel Bid 2026-27");

            var reloadedSettings = new UserSettingsService(path);
            await reloadedSettings.LoadSettingsAsync();
            var reloaded = new FuelLocationCatalog(reloadedSettings, fuel);
            var locations = await reloaded.GetLocationsAsync();

            locations.Should().Contain("Acme Fuel Bid 2026-27");
            locations.Should().Contain("Existing Pump");
            locations.Should().Contain("Key Pumps");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class StubFuelService : IFuelService
    {
        private readonly IReadOnlyList<string> _locations;

        public StubFuelService(IReadOnlyList<string> locations) => _locations = locations;

        public Task<IReadOnlyList<string>> GetDistinctFuelLocationsAsync() =>
            Task.FromResult(_locations);

        public Task<IEnumerable<Fuel>> GetAllFuelRecordsAsync() => throw new NotSupportedException();
        public Task<Fuel?> GetFuelRecordByIdAsync(int id) => throw new NotSupportedException();
        public Task<Fuel> CreateFuelRecordAsync(Fuel fuel) => throw new NotSupportedException();
        public Task<Fuel> UpdateFuelRecordAsync(Fuel fuel) => throw new NotSupportedException();
        public Task<bool> DeleteFuelRecordAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId) => throw new NotSupportedException();
        public Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate) => throw new NotSupportedException();
        public Task<decimal> GetTotalFuelCostAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null) => throw new NotSupportedException();
        public Task<decimal> GetTotalGallonsAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null) => throw new NotSupportedException();
        public Task<decimal> GetAverageMPGAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null) => throw new NotSupportedException();
    }
}
