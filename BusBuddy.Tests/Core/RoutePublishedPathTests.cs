using System.IO;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Tests.WPF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class RoutePublishedPathTests
{
    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext() => new(_options);
    }

    private static DbContextOptions<BusBuddyDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"RoutePath_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    [Test]
    public void RouteSession_InfersTwoRowKeyingWithoutASecondStructure()
    {
        Assert.That(RouteSession.Infer("Draft-Wiley-cell-1", false, "008 HomeToSchool AM"), Is.EqualTo(RouteSession.AM));
        Assert.That(RouteSession.Infer("Draft-Wiley-cell-1-PM", false, "008 HomeToSchool PM"), Is.EqualTo(RouteSession.PM));
        Assert.That(RouteSession.Infer("Draft-Wiley-cell-1-PM", false, "008 Transfer PM"), Is.EqualTo(RouteSession.Transfer));
        Assert.That(RouteSession.Infer("Special Needs Route", true, null), Is.EqualTo(RouteSession.SpecialNeeds));
    }

    [Test]
    public void RouteModel_DoesNotMergeTripIntoRoute()
    {
        var routeSource = CoreSourceFile.Read("Models/Route.cs");
        Assert.That(routeSource, Does.Not.Contain("public bool IsTrip"));
        Assert.That(routeSource, Does.Contain("never add IsTrip"));
    }

    [Test]
    public async Task CreateRoute_InfersPmSessionFromTwoRowName()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        var service = new RouteService(factory);
        var result = await service.CreateRouteAsync(new Route
        {
            RouteName = "Draft-School-cell-1-PM",
            Date = DateTime.Today,
            IsActive = true,
            School = "Wiley"
        });

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(result.Value!.Session, Is.EqualTo(RouteSession.PM));
    }

    [Test]
    public async Task AddStop_RejectsUnvalidatedCoordinates()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        var service = new RouteService(factory);
        var route = (await service.CreateRouteAsync(new Route
        {
            RouteName = "AM-5",
            Date = DateTime.Today,
            IsActive = true,
            School = "Wiley"
        })).Value!;

        var missing = await service.AddStopToRouteAsync(route.RouteId, new RouteStop
        {
            StopName = "Guessed",
            StopOrder = 1,
            CreatedDate = DateTime.UtcNow
        });
        Assert.That(missing.IsSuccess, Is.False);

        var origin = await service.AddStopToRouteAsync(route.RouteId, new RouteStop
        {
            StopName = "Null Island",
            Latitude = 0m,
            Longitude = 0m,
            StopOrder = 1,
            CreatedDate = DateTime.UtcNow
        });
        Assert.That(origin.IsSuccess, Is.False);
    }

    [Test]
    public async Task ReorderStops_RefreshesWaypointsFromValidatedStopOrder()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        var service = new RouteService(factory);
        var route = (await service.CreateRouteAsync(new Route
        {
            RouteName = "AM-5",
            Date = DateTime.Today,
            IsActive = true,
            School = "Wiley"
        })).Value!;

        var first = (await service.AddStopToRouteAsync(route.RouteId, Stop("Home", 38.10m, -102.70m, 1))).Value!;
        var second = (await service.AddStopToRouteAsync(route.RouteId, Stop("School", 38.08m, -102.62m, 2))).Value!;

        var reorder = await service.ReorderRouteStopsAsync(route.RouteId, new List<int> { second.RouteStopId, first.RouteStopId });
        Assert.That(reorder.IsSuccess, Is.True, reorder.Error);

        await using var verify = factory.CreateDbContext();
        var persisted = await verify.Routes.AsNoTracking().FirstAsync(r => r.RouteId == route.RouteId);
        var stops = RouteWaypointSerializer.ParseStops(persisted.WaypointsJson);
        Assert.That(stops, Has.Count.EqualTo(2));
        Assert.That(stops[0].Latitude, Is.EqualTo(38.08).Within(0.0001));
        Assert.That(stops[1].Latitude, Is.EqualTo(38.10).Within(0.0001));
    }

    [Test]
    public async Task RecordRiderException_DoesNotMutateStopsOrYearAssignment()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        var service = new RouteService(factory);

        int routeId;
        int studentId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM-5",
                Date = DateTime.Today,
                IsActive = true,
                School = "Wiley",
                Session = RouteSession.AM
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;

            seed.RouteStops.Add(Stop("Home", 38.10m, -102.70m, 1, routeId));
            seed.RouteStops.Add(Stop("School", 38.08m, -102.62m, 2, routeId));
            var rider = new Student
            {
                StudentName = "Rider",
                Grade = "3",
                School = "Wiley",
                ParentGuardian = "P",
                EmergencyPhone = "555",
                Active = true,
                AMRoute = "AM-5",
                CreatedDate = DateTime.UtcNow
            };
            seed.Students.Add(rider);
            await seed.SaveChangesAsync();
            studentId = rider.StudentId;
        }

        var recorded = await service.RecordRiderExceptionAsync(routeId, studentId, DateTime.Today, "Absent");
        Assert.That(recorded.IsSuccess, Is.True, recorded.Error);

        await using var verify = factory.CreateDbContext();
        var student = await verify.Students.AsNoTracking().FirstAsync(s => s.StudentId == studentId);
        Assert.That(student.AMRoute, Is.EqualTo("AM-5"));
        Assert.That(await verify.RouteStops.CountAsync(s => s.RouteId == routeId), Is.EqualTo(2));
        Assert.That(await verify.Students.CountAsync(), Is.EqualTo(1));
        Assert.That(await verify.RouteRiderExceptions.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task WaypointRebuild_PrefersPublishedStopsOverStudentHomes()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        int routeId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM-5",
                Date = DateTime.Today,
                IsActive = true,
                School = "Wiley"
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            seed.RouteStops.Add(Stop("Catalog", 38.11m, -102.71m, 1, routeId));
            seed.RouteStops.Add(Stop("School", 38.08m, -102.62m, 2, routeId));
            seed.Students.Add(new Student
            {
                StudentName = "HomePin",
                Active = true,
                AMRoute = "AM-5",
                Latitude = 38.99m,
                Longitude = -102.99m,
                CreatedDate = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var json = await new RouteWaypointRebuildService(factory).RebuildAndPersistAsync(routeId);
        var stops = RouteWaypointSerializer.ParseStops(json);
        Assert.That(stops, Has.Count.EqualTo(2));
        Assert.That(stops[0].Latitude, Is.EqualTo(38.11).Within(0.0001));
        Assert.That(stops.Select(s => s.Latitude), Does.Not.Contain(38.99));
    }

    private static RouteStop Stop(string name, decimal lat, decimal lon, int order, int routeId = 0) =>
        new()
        {
            RouteId = routeId,
            StopName = name,
            Latitude = lat,
            Longitude = lon,
            StopOrder = order,
            ScheduledArrival = TimeSpan.FromHours(7),
            ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(1)),
            CreatedDate = DateTime.UtcNow
        };
}
