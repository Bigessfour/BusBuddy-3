using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class TripEventServiceTests
{
    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext()
        {
            var ctx = new BusBuddyDbContext(_options);
            ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            return ctx;
        }
    }

    private static TestDbContextFactory CreateFactory()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        return new TestDbContextFactory(
            new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"Trips_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
    }

    [Test]
    public async Task CancelTrip_CancelsAndKeepsTheRow()
    {
        var factory = CreateFactory();
        var sut = new TripEventService(factory);
        await sut.AddTripAsync(new TripEvent
        {
            Type = TripType.Field,
            DestinationName = "Museum",
            LeaveTime = DateTime.UtcNow.Date.AddHours(10),
            PickupTime = TimeSpan.FromHours(10),
            ReturnClockTime = TimeSpan.FromHours(14),
            PlannedHeadcount = 20
        });

        var id = (await sut.GetAllTripsAsync()).Single().TripEventId;
        await sut.CancelTripAsync(id);

        var loaded = await sut.GetTripByIdAsync(id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Status, Is.EqualTo(TripStatus.Cancelled));
        Assert.That((await sut.GetAllTripsAsync()).Select(t => t.TripEventId), Does.Contain(id));
    }

    [Test]
    public async Task CancelTrip_CancelledTripDoesNotConflict()
    {
        var factory = CreateFactory();
        int busId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var bus = new Bus
            {
                BusNumber = "B9",
                SeatingCapacity = 72,
                Status = "Active",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                VINNumber = "1HGBH41JXMN109199",
                LicenseNumber = "TEST9",
                CreatedDate = DateTime.UtcNow
            };
            ctx.Buses.Add(bus);
            await ctx.SaveChangesAsync();
            busId = bus.BusId;
        }

        var sut = new TripEventService(factory);
        var day = DateTime.UtcNow.Date;
        await sut.AddTripAsync(new TripEvent
        {
            Type = TripType.Field,
            DestinationName = "Museum",
            LeaveTime = day.AddHours(10),
            PickupTime = TimeSpan.FromHours(10),
            ReturnClockTime = TimeSpan.FromHours(14),
            VehicleId = busId,
            PlannedHeadcount = 20
        });

        var id = (await sut.GetAllTripsAsync()).Single().TripEventId;
        await sut.CancelTripAsync(id);

        var conflict = await sut.HasConflictsAsync(
            busId,
            null,
            day.AddHours(10),
            day.AddHours(14));
        Assert.That(conflict, Is.False);
    }
}
