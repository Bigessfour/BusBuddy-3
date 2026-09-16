using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class BusServiceTests : IDisposable
{
    private DbContextOptions<BusBuddyDbContext> _options = null!;
    private BusService _busService = null!;
    private FuelService _fuelService = null!;
    private bool _disposed;

    private sealed class PassThroughBusCache : IBusCachingService
    {
        public Task<List<Bus>> GetAllBusesAsync(Func<Task<List<Bus>>> factory) => factory();

        public Task<Bus?> GetBusByIdAsync(int busId, Func<int, Task<Bus?>> factory) => factory(busId);

        public void InvalidateBusCache(int busId)
        {
        }

        public void InvalidateAllBusCache()
        {
        }
    }

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"Buses_{Guid.NewGuid()}")
            .Options;
        var factory = new InMemoryContextFactory(_options);
        _busService = new BusService(factory, new PassThroughBusCache());
        _fuelService = new FuelService(factory);
    }

    [TearDown]
    public void TearDown() => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task DeleteBusAsync_EmptyBus_HardDeletes()
    {
        var bus = await SeedBusAsync("7");

        var result = await _busService.DeleteBusAsync(bus.BusId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Error.Should().BeEmpty();
        await using var verify = new BusBuddyDbContext(_options);
        (await verify.Buses.FindAsync(bus.BusId)).Should().BeNull();
    }

    [Test]
    public async Task DeleteBusAsync_WithFuelHistory_RetiresAndKeepsTheRow()
    {
        var bus = await SeedBusAsync("5");
        var fuel = await _fuelService.CreateFuelRecordAsync(new Fuel
        {
            FuelDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            FuelLocation = "Test Pump",
            VehicleFueledId = bus.BusId,
            VehicleOdometerReading = 1000,
            FuelType = "Diesel",
            Gallons = 10m,
            PricePerGallon = 3.50m,
            TotalCost = 35m
        });
        fuel.IsSuccess.Should().BeTrue(fuel.Error);

        var result = await _busService.DeleteBusAsync(bus.BusId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Error.Should().Be("Bus 5 retired; 1 fuel record still reference it.");
        await using var verify = new BusBuddyDbContext(_options);
        var kept = await verify.Buses.AsNoTracking().SingleAsync(b => b.BusId == bus.BusId);
        kept.Status.Should().Be("Retired");
        kept.FleetLabel.Should().Be("5 (retired)");
        (await verify.FuelRecords.CountAsync(f => f.VehicleFueledId == bus.BusId)).Should().Be(1);
    }

    [Test]
    public async Task CreateFuelRecordAsync_AllowsARetiredBus()
    {
        var bus = await SeedBusAsync("8");
        await using (var write = new BusBuddyDbContext(_options))
        {
            var tracked = await write.Buses.FindAsync(bus.BusId);
            tracked!.Status = "Retired";
            await write.SaveChangesAsync();
        }

        var created = await _fuelService.CreateFuelRecordAsync(new Fuel
        {
            FuelDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            FuelLocation = "Shop Pump",
            VehicleFueledId = bus.BusId,
            VehicleOdometerReading = 2000,
            FuelType = "Diesel",
            Gallons = 5m
        });

        created.IsSuccess.Should().BeTrue(created.Error);
        created.Value.VehicleFueledId.Should().Be(bus.BusId);
    }

    private async Task<Bus> SeedBusAsync(string number)
    {
        await using var context = new BusBuddyDbContext(_options);
        var bus = new Bus
        {
            BusNumber = number,
            Year = 2020,
            Make = "IC",
            Model = "CE",
            SeatingCapacity = 40,
            VINNumber = $"VIN{number}",
            LicenseNumber = $"L{number}",
            Status = "Active"
        };
        context.Buses.Add(bus);
        await context.SaveChangesAsync();
        return bus;
    }
}
