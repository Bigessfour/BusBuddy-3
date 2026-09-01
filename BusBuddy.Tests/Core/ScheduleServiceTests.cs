using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class ScheduleServiceTests
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
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"Schedules_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    private static async Task<(int RouteId, int BusId, int DriverId)> SeedRouteBusDriverAsync(
        TestDbContextFactory factory)
    {
        await using var ctx = factory.CreateWriteDbContext();
        var route = new Route { RouteName = "AM-1", Date = DateTime.Today, IsActive = true };
        var bus = new Bus
        {
            BusNumber = "B1",
            SeatingCapacity = 72,
            Status = "Active",
            Year = 2020,
            Make = "IC",
            Model = "CE",
            VINNumber = "1HGBH41JXMN109186",
            LicenseNumber = "TEST1",
            CreatedDate = DateTime.UtcNow
        };
        var driver = new Driver { DriverName = "Jane Doe", DriversLicenceType = "CDL", Status = "Active" };
        ctx.Routes.Add(route);
        ctx.Buses.Add(bus);
        ctx.Drivers.Add(driver);
        await ctx.SaveChangesAsync();
        return (route.RouteId, bus.BusId, driver.DriverId);
    }

    [Test]
    public void DeriveTripDetails_AwayLocation_SetsDestinationTown()
    {
        var sut = new ScheduleService(CreateFactory());
        var schedule = new Schedule
        {
            SportsCategory = "Football",
            Location = "Away @ Lamar"
        };

        sut.DeriveTripDetails(schedule);

        Assert.That(schedule.DestinationTown, Is.Not.Null.And.Not.Empty);
        Assert.That(schedule.DestinationTown, Does.Contain("Lamar").IgnoreCase);
    }

    [Test]
    public void DeriveTripDetails_Activity_Skips()
    {
        var sut = new ScheduleService(CreateFactory());
        var schedule = new Schedule
        {
            SportsCategory = "Activity",
            Location = "Away @ Lamar",
            DestinationTown = null
        };

        sut.DeriveTripDetails(schedule);

        Assert.That(schedule.DestinationTown, Is.Null);
    }

    [Test]
    public async Task AddSchedule_DepartureAfterArrival_Throws()
    {
        var factory = CreateFactory();
        var ids = await SeedRouteBusDriverAsync(factory);
        var sut = new ScheduleService(factory);
        var when = DateTime.Today.AddHours(10);

        Assert.ThrowsAsync<ArgumentException>((Func<Task>)(() => sut.AddScheduleAsync(new Schedule
        {
            RouteId = ids.RouteId,
            BusId = ids.BusId,
            DriverId = ids.DriverId,
            ScheduleDate = DateTime.Today,
            DepartureTime = when,
            ArrivalTime = when.AddMinutes(-30)
        })));
    }

    [Test]
    public async Task AddThenGetSchedules_RoundTrip()
    {
        var factory = CreateFactory();
        var ids = await SeedRouteBusDriverAsync(factory);
        var sut = new ScheduleService(factory);
        var depart = DateTime.Today.AddHours(14);
        var arrive = depart.AddHours(3);

        await sut.AddScheduleAsync(new Schedule
        {
            RouteId = ids.RouteId,
            BusId = ids.BusId,
            DriverId = ids.DriverId,
            ScheduleDate = DateTime.Today,
            DepartureTime = depart,
            ArrivalTime = arrive,
            SportsCategory = "Football",
            Location = "Away @ Lamar"
        });

        var all = (await sut.GetSchedulesAsync()).ToList();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].RouteId, Is.EqualTo(ids.RouteId));
        Assert.That(all[0].DestinationTown, Does.Contain("Lamar").IgnoreCase);

        var byId = await sut.GetScheduleByIdAsync(all[0].ScheduleId);
        Assert.That(byId, Is.Not.Null);
        Assert.That(byId!.DriverId, Is.EqualTo(ids.DriverId));
    }
}
