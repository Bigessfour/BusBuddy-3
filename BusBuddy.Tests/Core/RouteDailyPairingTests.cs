using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public sealed class RouteDailyPairingTests
{
    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext()
        {
            var context = new BusBuddyDbContext(_options);
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            return context;
        }
    }

    private static DbContextOptions<BusBuddyDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase("bb-113-sub-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    [Test]
    public void Resolve_FallsBackToRouteAmPairWhenNoSchedule()
    {
        var route = new Route
        {
            RouteId = 4,
            Date = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            AMVehicleId = 2,
            AMDriverId = 3,
            AMBeginTime = TimeSpan.FromHours(7)
        };

        var pairing = RouteDailyPairing.Resolve(route, RouteTimeSlot.AM, schedulesForRouteDate: null);

        Assert.That(pairing.FromSchedule, Is.False);
        Assert.That(pairing.BusId, Is.EqualTo(2));
        Assert.That(pairing.DriverId, Is.EqualTo(3));
    }

    [Test]
    public void Resolve_UsesSchedulePairWhenPresentForRouteDate()
    {
        var day = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var route = new Route
        {
            RouteId = 4,
            Date = day,
            AMVehicleId = 2,
            AMDriverId = 3,
            AMBeginTime = TimeSpan.FromHours(7),
            EstimatedDuration = 40
        };
        var schedules = new[]
        {
            new Schedule
            {
                RouteId = 4,
                BusId = 12,
                DriverId = 8,
                ScheduleDate = day,
                DepartureTime = day.AddHours(7),
                ArrivalTime = day.AddHours(8),
                Status = "Scheduled"
            }
        };

        var pairing = RouteDailyPairing.Resolve(route, RouteTimeSlot.AM, schedules);

        Assert.That(pairing.FromSchedule, Is.True);
        Assert.That(pairing.BusId, Is.EqualTo(12));
        Assert.That(pairing.DriverId, Is.EqualTo(8));
        Assert.That(route.AMVehicleId, Is.EqualTo(2), "year-default bus must stay put");
        Assert.That(route.AMDriverId, Is.EqualTo(3), "year-default driver must stay put");
    }

    [Test]
    public void Resolve_IgnoresCancelledScheduleRows()
    {
        var day = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var route = new Route
        {
            RouteId = 4,
            Date = day,
            AMVehicleId = 2,
            AMDriverId = 3,
            AMBeginTime = TimeSpan.FromHours(7)
        };
        var schedules = new[]
        {
            new Schedule
            {
                RouteId = 4,
                BusId = 12,
                DriverId = 8,
                ScheduleDate = day,
                DepartureTime = day.AddHours(7),
                ArrivalTime = day.AddHours(8),
                Status = "Cancelled"
            }
        };

        var pairing = RouteDailyPairing.Resolve(route, RouteTimeSlot.AM, schedules);

        Assert.That(pairing.FromSchedule, Is.False);
        Assert.That(pairing.BusId, Is.EqualTo(2));
        Assert.That(pairing.DriverId, Is.EqualTo(3));
    }

    [Test]
    public void BuildSessionRow_UsesRouteDateNotUtcNow()
    {
        var day = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        var route = new Route
        {
            RouteId = 4,
            RouteName = "AM Special Needs Bus 5",
            School = "North",
            Date = day,
            AMBeginTime = new TimeSpan(6, 45, 0),
            EstimatedDuration = 50
        };

        var row = RouteDailyPairing.BuildSessionRow(route, RouteTimeSlot.AM, busId: 12, driverId: 8);

        Assert.That(row.ScheduleDate, Is.EqualTo(day));
        Assert.That(row.BusId, Is.EqualTo(12));
        Assert.That(row.DriverId, Is.EqualTo(8));
        Assert.That(row.DepartureTime, Is.EqualTo(day.Add(new TimeSpan(6, 45, 0))));
        Assert.That(row.ArrivalTime, Is.EqualTo(row.DepartureTime.AddMinutes(50)));
        Assert.That(row.Notes, Does.Contain("Substitute"));
        Assert.That(row.Notes, Does.Contain("2026-11-06"));
    }

    [Test]
    public async Task SaveDatedSubstitute_DoesNotRewriteRouteYearDefaults()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        int homeBusId;
        int spareBusId;
        int homeDriverId;
        int poolDriverId;
        int routeId;

        using (var db = factory.CreateWriteDbContext())
        {
            var homeBus = new Bus
            {
                BusNumber = "5",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                SeatingCapacity = 70,
                VINNumber = "VIN5",
                LicenseNumber = "L5",
                Status = "Active"
            };
            var spareBus = new Bus
            {
                BusNumber = "12",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                SeatingCapacity = 70,
                VINNumber = "VIN12",
                LicenseNumber = "L12",
                Status = "Active"
            };
            db.Buses.AddRange(homeBus, spareBus);
            var homeDriver = new Driver
            {
                DriverName = "Home",
                Status = "Active",
                LicenseClass = "B",
                SchoolBusEndorsement = true,
                TrainingComplete = true
            };
            var poolDriver = new Driver
            {
                DriverName = "Pool",
                Status = "Active",
                LicenseClass = "B",
                SchoolBusEndorsement = true,
                TrainingComplete = true
            };
            db.Drivers.AddRange(homeDriver, poolDriver);
            await db.SaveChangesAsync();
            homeBusId = homeBus.BusId;
            spareBusId = spareBus.BusId;
            homeDriverId = homeDriver.DriverId;
            poolDriverId = poolDriver.DriverId;

            var route = new Route
            {
                RouteName = "AM Special Needs Bus 5",
                Date = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                AMVehicleId = homeBusId,
                AMDriverId = homeDriverId,
                AMBeginTime = TimeSpan.FromHours(7),
                EstimatedDuration = 45
            };
            db.Routes.Add(route);
            await db.SaveChangesAsync();
            routeId = route.RouteId;
        }

        var schedules = new ScheduleService(factory);
        Route routeRow;
        using (var db = factory.CreateDbContext())
        {
            routeRow = await db.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId);
        }

        var row = RouteDailyPairing.BuildSessionRow(routeRow, RouteTimeSlot.AM, spareBusId, poolDriverId);
        await schedules.SaveRouteSessionScheduleAsync(row);

        using (var verify = factory.CreateDbContext())
        {
            var storedRoute = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId);
            Assert.That(storedRoute.AMVehicleId, Is.EqualTo(homeBusId));
            Assert.That(storedRoute.AMDriverId, Is.EqualTo(homeDriverId));

            var stored = await schedules.GetSchedulesForRouteOnDateAsync(routeId, routeRow.Date);
            Assert.That(stored.Count(s => s.Status != "Cancelled"), Is.EqualTo(1));
            Assert.That(stored.Single(s => s.Status != "Cancelled").BusId, Is.EqualTo(spareBusId));
            Assert.That(stored.Single(s => s.Status != "Cancelled").DriverId, Is.EqualTo(poolDriverId));

            var pairing = RouteDailyPairing.Resolve(storedRoute, RouteTimeSlot.AM, stored);
            Assert.That(pairing.FromSchedule, Is.True);
            Assert.That(pairing.BusId, Is.EqualTo(spareBusId));
            Assert.That(pairing.DriverId, Is.EqualTo(poolDriverId));
        }
    }

    [Test]
    public void PreferActiveNavMatch_SkipsCancelledTwin()
    {
        var day = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var cancelled = new Schedule
        {
            ScheduleId = 1,
            BusId = 12,
            DriverId = 8,
            ScheduleDate = day,
            DepartureTime = day.AddHours(7),
            Status = "Cancelled",
            Bus = new Bus { BusId = 12, BusNumber = "OLD" },
            Driver = new Driver { DriverId = 8, DriverName = "Old" },
            UpdatedDate = day.AddHours(-1)
        };
        var active = new Schedule
        {
            ScheduleId = 2,
            BusId = 12,
            DriverId = 8,
            ScheduleDate = day,
            DepartureTime = day.AddHours(7),
            Status = "Scheduled",
            Bus = new Bus { BusId = 12, BusNumber = "12-SPARE" },
            Driver = new Driver { DriverId = 8, DriverName = "Pool Driver" },
            UpdatedDate = day.AddMinutes(-5)
        };

        var match = RouteDailyPairing.PreferActiveNavMatch(
            new[] { cancelled, active },
            busId: 12,
            driverId: 8,
            fromSchedule: true);

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Bus!.BusNumber, Is.EqualTo("12-SPARE"));
        Assert.That(match.Driver!.DriverName, Is.EqualTo("Pool Driver"));
    }

    [Test]
    public void MatchesSessionWindow_FollowsPreferredBeginSideOfNoon()
    {
        Assert.That(
            RouteDailyPairing.MatchesSessionWindow(
                TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(30)),
                RouteTimeSlot.AM,
                preferredBegin: TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(30))),
            Is.True,
            "late AM begin keeps afternoon clocks in-window");
        Assert.That(
            RouteDailyPairing.MatchesSessionWindow(
                TimeSpan.FromHours(7),
                RouteTimeSlot.AM,
                preferredBegin: TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(30))),
            Is.False);
        Assert.That(
            RouteDailyPairing.MatchesSessionWindow(
                TimeSpan.FromHours(11),
                RouteTimeSlot.PM,
                preferredBegin: TimeSpan.FromHours(11).Add(TimeSpan.FromMinutes(30))),
            Is.True,
            "early PM begin keeps late-morning clocks in-window");
    }

    [Test]
    public void Resolve_DoesNotPickNewerPmRowWhenResolvingAmAfterClockDrift()
    {
        var day = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var route = new Route
        {
            RouteId = 4,
            Date = day,
            AMVehicleId = 2,
            AMDriverId = 3,
            AMBeginTime = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(15)), // re-timed off stored 07:00
            PMVehicleId = 20,
            PMDriverId = 21,
            PMBeginTime = TimeSpan.FromHours(14)
        };
        var amSpare = new Schedule
        {
            ScheduleId = 1,
            RouteId = 4,
            BusId = 12,
            DriverId = 8,
            ScheduleDate = day,
            DepartureTime = day.AddHours(7),
            ArrivalTime = day.AddHours(8),
            Status = "Scheduled",
            UpdatedDate = day.AddHours(-2)
        };
        var newerPm = new Schedule
        {
            ScheduleId = 2,
            RouteId = 4,
            BusId = 99,
            DriverId = 98,
            ScheduleDate = day,
            DepartureTime = day.AddHours(14),
            ArrivalTime = day.AddHours(15),
            Status = "Scheduled",
            UpdatedDate = day.AddMinutes(-1)
        };

        var pairing = RouteDailyPairing.Resolve(route, RouteTimeSlot.AM, new[] { amSpare, newerPm });

        Assert.That(pairing.FromSchedule, Is.True);
        Assert.That(pairing.BusId, Is.EqualTo(12), "AM resolve must stay in morning window");
        Assert.That(pairing.DriverId, Is.EqualTo(8));
    }

    [Test]
    public void Resolve_PrefersNewestSameSlotRowOverOlderHomeBusRow()
    {
        var day = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var route = new Route
        {
            RouteId = 4,
            Date = day,
            AMVehicleId = 2,
            AMDriverId = 3,
            AMBeginTime = TimeSpan.FromHours(7)
        };
        var olderHome = new Schedule
        {
            ScheduleId = 1,
            RouteId = 4,
            BusId = 2,
            DriverId = 3,
            ScheduleDate = day,
            DepartureTime = day.AddHours(7),
            ArrivalTime = day.AddHours(8),
            Status = "Scheduled",
            CreatedDate = day.AddHours(-2),
            UpdatedDate = day.AddHours(-2)
        };
        var newerSpare = new Schedule
        {
            ScheduleId = 2,
            RouteId = 4,
            BusId = 12,
            DriverId = 8,
            ScheduleDate = day,
            DepartureTime = day.AddHours(7),
            ArrivalTime = day.AddHours(8),
            Status = "Scheduled",
            CreatedDate = day.AddHours(-1),
            UpdatedDate = day.AddMinutes(-5)
        };

        var pairing = RouteDailyPairing.Resolve(route, RouteTimeSlot.AM, new[] { olderHome, newerSpare });

        Assert.That(pairing.FromSchedule, Is.True);
        Assert.That(pairing.BusId, Is.EqualTo(12), "spare must beat older Generate home-bus row");
        Assert.That(pairing.DriverId, Is.EqualTo(8));
        Assert.That(route.AMVehicleId, Is.EqualTo(2));
    }

    [Test]
    public async Task SaveRouteSessionSchedule_CancelsOlderSameSlotSiblingWhenSpareBusSaved()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        int homeBusId;
        int spareBusId;
        int homeDriverId;
        int poolDriverId;
        int routeId;
        var day = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

        using (var db = factory.CreateWriteDbContext())
        {
            var homeBus = new Bus
            {
                BusNumber = "5",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                SeatingCapacity = 70,
                VINNumber = "VIN5b",
                LicenseNumber = "L5b",
                Status = "Active"
            };
            var spareBus = new Bus
            {
                BusNumber = "12",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                SeatingCapacity = 70,
                VINNumber = "VIN12b",
                LicenseNumber = "L12b",
                Status = "Active"
            };
            db.Buses.AddRange(homeBus, spareBus);
            var homeDriver = new Driver
            {
                DriverName = "Home",
                Status = "Active",
                LicenseClass = "B",
                SchoolBusEndorsement = true,
                TrainingComplete = true
            };
            var poolDriver = new Driver
            {
                DriverName = "Pool",
                Status = "Active",
                LicenseClass = "B",
                SchoolBusEndorsement = true,
                TrainingComplete = true
            };
            db.Drivers.AddRange(homeDriver, poolDriver);
            await db.SaveChangesAsync();
            homeBusId = homeBus.BusId;
            spareBusId = spareBus.BusId;
            homeDriverId = homeDriver.DriverId;
            poolDriverId = poolDriver.DriverId;

            var route = new Route
            {
                RouteName = "AM Special Needs Bus 5",
                Date = day,
                IsActive = true,
                AMVehicleId = homeBusId,
                AMDriverId = homeDriverId,
                AMBeginTime = TimeSpan.FromHours(7),
                EstimatedDuration = 45
            };
            db.Routes.Add(route);
            await db.SaveChangesAsync();
            routeId = route.RouteId;

            db.Schedules.Add(new Schedule
            {
                RouteId = routeId,
                BusId = homeBusId,
                DriverId = homeDriverId,
                ScheduleDate = day,
                DepartureTime = day.AddHours(7),
                ArrivalTime = day.AddHours(7).AddMinutes(45),
                Status = "Scheduled",
                Notes = "Generated from Route Management",
                CreatedDate = day.AddHours(-3)
            });
            await db.SaveChangesAsync();
        }

        var schedules = new ScheduleService(factory);
        Route routeRow;
        using (var db = factory.CreateDbContext())
        {
            routeRow = await db.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId);
        }

        var spare = RouteDailyPairing.BuildSessionRow(routeRow, RouteTimeSlot.AM, spareBusId, poolDriverId);
        await schedules.SaveRouteSessionScheduleAsync(spare);

        var stored = await schedules.GetSchedulesForRouteOnDateAsync(routeId, day);
        Assert.That(stored, Has.Count.EqualTo(2));
        Assert.That(stored.Count(s => s.Status == "Cancelled"), Is.EqualTo(1));
        Assert.That(stored.Single(s => s.Status == "Cancelled").BusId, Is.EqualTo(homeBusId));
        Assert.That(stored.Single(s => s.Status != "Cancelled").BusId, Is.EqualTo(spareBusId));

        var pairing = RouteDailyPairing.Resolve(routeRow, RouteTimeSlot.AM, stored);
        Assert.That(pairing.BusId, Is.EqualTo(spareBusId));
        Assert.That(routeRow.AMVehicleId, Is.EqualTo(homeBusId), "year-default FK must stay");
    }
}
