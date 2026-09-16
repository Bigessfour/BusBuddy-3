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
    public async Task AddSchedule_DoesNotDeriveTripTownFromAwayLocation()
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
        Assert.That(all[0].DestinationTown, Is.Null);
    }

    [Test]
    public async Task AddSchedule_OmittedIdsDoNotCopyFleet()
    {
        var factory = CreateFactory();
        var ids = await SeedRouteBusDriverAsync(factory);
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var route = await ctx.Routes.FirstAsync(r => r.RouteId == ids.RouteId);
            route.AMVehicleId = ids.BusId;
            route.AMDriverId = ids.DriverId;
            ctx.Entry(route).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
        }

        var sut = new ScheduleService(factory);
        var depart = DateTime.Today.AddHours(7);
        Assert.ThrowsAsync<ArgumentException>((Func<Task>)(() => sut.AddScheduleAsync(new Schedule
        {
            RouteId = ids.RouteId,
            ScheduleDate = DateTime.Today,
            DepartureTime = depart,
            ArrivalTime = depart.AddMinutes(45)
        })));
        Assert.That(await sut.GetSchedulesAsync(), Is.Empty);
    }

    [Test]
    public async Task AddDailyFromPublishedRoute_CopiesSessionAmFleetAndBeginTime()
    {
        var factory = CreateFactory();
        var ids = await SeedRouteBusDriverAsync(factory);
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var route = await ctx.Routes.FirstAsync(r => r.RouteId == ids.RouteId);
            route.AMVehicleId = ids.BusId;
            route.AMDriverId = ids.DriverId;
            route.AMBeginTime = new TimeSpan(6, 45, 0);
            route.EstimatedDuration = 50;
            route.School = "Wiley Elementary";
            ctx.Entry(route).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
        }

        var sut = new ScheduleService(factory);
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        Assert.That(await sut.AddDailyFromPublishedRouteAsync(
            ids.RouteId,
            day), Is.True);

        var saved = (await sut.GetSchedulesAsync()).Single();
        Assert.That(saved.BusId, Is.EqualTo(ids.BusId));
        Assert.That(saved.DriverId, Is.EqualTo(ids.DriverId));
        Assert.That(saved.ScheduleDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(saved.DepartureTime, Is.EqualTo(day.Add(new TimeSpan(6, 45, 0))));
        Assert.That(saved.ArrivalTime, Is.EqualTo(saved.DepartureTime.AddMinutes(50)));
        Assert.That(saved.Location, Is.EqualTo("Wiley Elementary"));
        Assert.That(saved.Status, Is.EqualTo("Scheduled"));
        Assert.That(saved.Notes, Is.EqualTo("Daily schedule for AM-1"));
    }

    [Test]
    public async Task AddDailyFromPublishedRoute_CopiesSessionPmFleetNotAm()
    {
        var factory = CreateFactory();
        int routeId;
        int amBusId;
        int pmBusId;
        int amDriverId;
        int pmDriverId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var amBus = new Bus
            {
                BusNumber = "AM1", SeatingCapacity = 72, Status = "Active", Year = 2020,
                Make = "IC", Model = "CE", VINNumber = "VINAMFLEET1", LicenseNumber = "LAMF1",
                CreatedDate = DateTime.UtcNow
            };
            var pmBus = new Bus
            {
                BusNumber = "PM1", SeatingCapacity = 72, Status = "Active", Year = 2020,
                Make = "IC", Model = "CE", VINNumber = "VINPMFLEET1", LicenseNumber = "LPMF1",
                CreatedDate = DateTime.UtcNow
            };
            var amDriver = new Driver { DriverName = "AM Driver", DriversLicenceType = "CDL", Status = "Active" };
            var pmDriver = new Driver { DriverName = "PM Driver", DriversLicenceType = "CDL", Status = "Active" };
            var route = new Route
            {
                RouteName = "North-PM",
                Date = DateTime.Today,
                IsActive = true,
                Session = RouteSession.PM,
                PMBeginTime = new TimeSpan(15, 10, 0),
                EstimatedDuration = 40,
                School = "Wiley Elementary"
            };
            ctx.Buses.AddRange(amBus, pmBus);
            ctx.Drivers.AddRange(amDriver, pmDriver);
            ctx.Routes.Add(route);
            await ctx.SaveChangesAsync();
            route.AMVehicleId = amBus.BusId;
            route.AMDriverId = amDriver.DriverId;
            route.PMVehicleId = pmBus.BusId;
            route.PMDriverId = pmDriver.DriverId;
            ctx.Entry(route).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
            routeId = route.RouteId;
            amBusId = amBus.BusId;
            pmBusId = pmBus.BusId;
            amDriverId = amDriver.DriverId;
            pmDriverId = pmDriver.DriverId;
        }

        var sut = new ScheduleService(factory);
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        Assert.That(await sut.AddDailyFromPublishedRouteAsync(
            routeId,
            day), Is.True);

        var saved = (await sut.GetSchedulesAsync()).Single();
        Assert.That(saved.BusId, Is.EqualTo(pmBusId));
        Assert.That(saved.DriverId, Is.EqualTo(pmDriverId));
        Assert.That(saved.BusId, Is.Not.EqualTo(amBusId));
        Assert.That(saved.DriverId, Is.Not.EqualTo(amDriverId));
        Assert.That(saved.DepartureTime, Is.EqualTo(day.Add(new TimeSpan(15, 10, 0))));
        Assert.That(saved.ArrivalTime, Is.EqualTo(saved.DepartureTime.AddMinutes(40)));
    }

    [Test]
    public async Task AddDailyFromPublishedRoute_ReturnsFalseWhenSessionSlotEmpty()
    {
        var factory = CreateFactory();
        var ids = await SeedRouteBusDriverAsync(factory);
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var route = await ctx.Routes.FirstAsync(r => r.RouteId == ids.RouteId);
            route.PMVehicleId = ids.BusId;
            route.PMDriverId = ids.DriverId;
            ctx.Entry(route).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
        }

        var sut = new ScheduleService(factory);
        Assert.That(await sut.AddDailyFromPublishedRouteAsync(
            ids.RouteId,
            DateTime.UtcNow), Is.False);
        Assert.That(await sut.GetSchedulesAsync(), Is.Empty);
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
        Assert.That(all[0].DestinationTown, Is.Null);

        var byId = await sut.GetScheduleByIdAsync(all[0].ScheduleId);
        Assert.That(byId, Is.Not.Null);
        Assert.That(byId!.DriverId, Is.EqualTo(ids.DriverId));
    }
}
