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
        Assert.That(RouteSession.ToAssignmentSlot(new Route { RouteName = "Draft-Wiley-cell-1", Session = RouteSession.AM }), Is.EqualTo(RouteTimeSlot.AM));
        Assert.That(RouteSession.ToAssignmentSlot(new Route { RouteName = "Draft-Wiley-cell-1-PM", Session = RouteSession.PM }), Is.EqualTo(RouteTimeSlot.PM));
        Assert.That(
            RouteSession.ToAssignmentSlot(new Route { RouteName = "SN-PM", Session = RouteSession.SpecialNeeds, IsSpecialNeedsRoute = true }),
            Is.EqualTo(RouteTimeSlot.PM));
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
        int routeId;
        int firstId;
        int secondId;
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
            var first = Stop("Home", 38.10m, -102.70m, 1, routeId);
            var second = Stop("School", 38.08m, -102.62m, 2, routeId);
            seed.RouteStops.AddRange(first, second);
            await seed.SaveChangesAsync();
            firstId = first.RouteStopId;
            secondId = second.RouteStopId;
        }

        var service = new RouteService(factory);
        var reorder = await service.ReorderRouteStopsAsync(routeId, new List<int> { secondId, firstId });
        Assert.That(reorder.IsSuccess, Is.True, reorder.Error);

        await using var verify = factory.CreateDbContext();
        var persisted = await verify.Routes.AsNoTracking().FirstAsync(r => r.RouteId == routeId);
        var stops = RouteWaypointSerializer.ParseStops(persisted.WaypointsJson);
        Assert.That(stops, Has.Count.EqualTo(2), persisted.WaypointsJson ?? "<null>");
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

        var ids = await service.GetRiderExceptionStudentIdsAsync(routeId, DateTime.Today);
        Assert.That(ids.IsSuccess, Is.True, ids.Error);
        Assert.That(ids.Value, Does.Contain(studentId));
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

    [Test]
    public async Task WaypointRebuild_SkipsStudentStopWithNoAssignedRider()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        int routeId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "Special Needs Route",
                Date = DateTime.Today,
                IsActive = true,
                School = "Wiley K-12 School"
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            seed.RouteStops.Add(Stop("District Bus Barn", 38.0872m, -102.6208m, 1, routeId));
            seed.RouteStops.Add(Stop("TEST_STUDENT_SN_01", 38.1512m, -102.7210m, 2, routeId));
            seed.RouteStops.Add(Stop("Wiley K-12 School", 38.1535m, -102.7195m, 3, routeId));
            seed.Students.Add(new Student
            {
                StudentName = "Assigned Rider",
                Active = true,
                AmRouteId = routeId,
                HomeAddress = "710 S 4th Street",
                Latitude = 38.0822m,
                Longitude = -102.6178m,
                CreatedDate = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var json = await new RouteWaypointRebuildService(factory).RebuildAndPersistAsync(routeId);
        var stops = RouteWaypointSerializer.ParseStops(json);
        Assert.That(stops.Select(s => s.Latitude), Does.Not.Contain(38.1512));
        Assert.That(stops, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task OmitUnlistedSchool_DropsInactiveWileyAndClearsTheDrivePath()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        int routeId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            seed.Destinations.Add(new Destination
            {
                Name = "Wiley K-12 School",
                DestinationType = DestinationTypes.School,
                City = "Wiley",
                IsActive = false,
                Latitude = 38.1535m,
                Longitude = -102.7195m,
                CreatedDate = DateTime.UtcNow
            });
            seed.Destinations.Add(new Destination
            {
                Name = "Lamar High School",
                DestinationType = DestinationTypes.School,
                City = "Lamar",
                IsActive = true,
                Latitude = 38.0872m,
                Longitude = -102.6207m,
                CreatedDate = DateTime.UtcNow
            });
            var route = new Route
            {
                RouteName = "Special Needs Route",
                Date = DateTime.Today,
                IsActive = true,
                IsSpecialNeedsRoute = true,
                School = "Wiley K-12 School",
                Session = RouteSession.SpecialNeeds,
                WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
                {
                    (38.0872, -102.6208),
                    (38.1535, -102.7195)
                })
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            var barn = Stop("District Bus Barn", 38.0872m, -102.6208m, 1, routeId);
            barn.StopAddress = "Bus barn";
            seed.RouteStops.Add(barn);
            seed.RouteStops.Add(Stop("Wiley K-12 School", 38.1535m, -102.7195m, 2, routeId));
            await seed.SaveChangesAsync();
        }

        var cleanup = await new RouteWaypointRebuildService(factory).OmitUnlistedSchoolsAsync(routeId);
        Assert.That(cleanup.Changed, Is.True);
        Assert.That(cleanup.School, Is.Null);
        Assert.That(cleanup.WaypointsJson, Is.Null.Or.Empty);

        await using var verify = factory.CreateDbContext();
        var names = await verify.RouteStops.Select(s => s.StopName).ToListAsync();
        Assert.That(names, Is.Empty);
        var routeRow = await verify.Routes.SingleAsync(r => r.RouteId == routeId);
        Assert.That(routeRow.School, Is.Null);
        Assert.That(routeRow.WaypointsJson, Is.Null.Or.Empty);
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
