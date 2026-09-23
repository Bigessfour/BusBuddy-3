using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class AssignedHomeStopSyncTests
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

    [Test]
    public void UseCatalogPickup_SpecialNeedsUsesHomeEvenWhenCatalogIdIsSet()
    {
        var student = new Student
        {
            RequiresSpecialNeedsBus = true,
            PickupStopId = 4,
            Latitude = 38.08m,
            Longitude = -102.62m
        };

        Assert.That(AssignedHomeStopSync.UseCatalogPickup(student), Is.False);
        Assert.That(AssignedHomeStopSync.FollowsValidatedHome(student), Is.True);
    }

    [Test]
    public async Task Apply_MovesExclusiveStopToValidatedHome_AndDropsStaleDrivePath()
    {
        var factory = CreateFactory();
        int routeId;
        int studentId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = await AddRouteAsync(seed, "AM-Special");
            routeId = route.RouteId;
            route.WaypointsJson = RouteWaypointSerializer.FromPairs([(38.50, -102.90), (38.20, -102.70)]);
            var student = HomeStudent(routeId, 38.08m, -102.62m, specialNeeds: true, pickupStopId: 9);
            seed.Students.Add(student);
            await seed.SaveChangesAsync();
            studentId = student.StudentId;
            seed.RouteStops.AddRange(
                Stop(routeId, 1, "School", 38.20m, -102.70m, notes: null),
                Stop(routeId, 2, "Corner", 38.50m, -102.90m, notes: $"StudentId={studentId}"));
            await seed.SaveChangesAsync();
        }

        var snapshot = await new GeoDataService(factory).GetDistrictMapAsync(routeId);
        var published = snapshot.SelectedRoute!.PublishedStops.Select(s => s.Latitude).ToList();

        Assert.That(published.Any(lat => Math.Abs(lat - 38.08) < 0.0001), Is.True);
        Assert.That(published.Any(lat => Math.Abs(lat - 38.50) < 0.0001), Is.False);
        var points = RouteWaypointSerializer.Parse(snapshot.SelectedRoute.WaypointsJson);
        Assert.That(points.Any(p => Math.Abs(p.Latitude - 38.08) < 0.0001), Is.True);
        Assert.That(points.Any(p => Math.Abs(p.Latitude - 38.50) < 0.0001), Is.False);

        await using var verify = factory.CreateDbContext();
        var stop = await verify.RouteStops.SingleAsync(s => s.Notes == $"StudentId={studentId}");
        Assert.That(stop.Latitude, Is.EqualTo(38.08m));
        Assert.That(stop.Longitude, Is.EqualTo(-102.62m));
    }

    [Test]
    public async Task Apply_AddressMatchedStopFollowsHome_WhenNotesAreEmpty()
    {
        var factory = CreateFactory();
        await using var db = factory.CreateWriteDbContext();
        var route = await AddRouteAsync(db, "AM-Address");
        route.WaypointsJson = RouteWaypointSerializer.FromPairs([(38.50, -102.90), (38.20, -102.70)]);
        var student = HomeStudent(route.RouteId, 38.08m, -102.62m, specialNeeds: true, pickupStopId: null);
        db.Students.Add(student);
        await db.SaveChangesAsync();
        var homeStop = Stop(route.RouteId, 2, "Driveway", 38.50m, -102.90m, notes: null);
        homeStop.StopAddress = student.HomeAddress!;
        db.RouteStops.AddRange(
            Stop(route.RouteId, 1, "School", 38.20m, -102.70m, notes: null),
            homeStop);
        await db.SaveChangesAsync();

        var changed = await AssignedHomeStopSync.ApplyAsync(db, student);
        await db.SaveChangesAsync();

        Assert.That(changed, Is.True);
        var moved = await db.RouteStops.SingleAsync(s => s.StopOrder == 2);
        Assert.That(moved.Latitude, Is.EqualTo(38.08m));
        Assert.That(moved.Longitude, Is.EqualTo(-102.62m));
        var school = await db.RouteStops.SingleAsync(s => s.StopOrder == 1);
        Assert.That(school.Latitude, Is.EqualTo(38.20m));
        Assert.That(route.WaypointsJson, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Apply_SplitsSpecialNeedsRiderOffSharedCatalogStop()
    {
        var factory = CreateFactory();
        await using var db = factory.CreateWriteDbContext();
        var route = await AddRouteAsync(db, "AM-Shared");
        var special = HomeStudent(route.RouteId, 38.08m, -102.62m, specialNeeds: true, pickupStopId: 9);
        var neighbor = new Student
        {
            StudentName = "TEST_NEIGHBOR",
            Latitude = 38.16m,
            Longitude = -102.71m,
            PickupStopId = 9,
            AmRouteId = route.RouteId,
            Active = true
        };
        db.Students.AddRange(special, neighbor);
        await db.SaveChangesAsync();
        db.RouteStops.Add(Stop(
            route.RouteId,
            1,
            "Oak",
            38.16m,
            -102.71m,
            notes: $"StudentIds={special.StudentId},{neighbor.StudentId}"));
        await db.SaveChangesAsync();

        var changed = await AssignedHomeStopSync.ApplyAsync(db, special);
        await db.SaveChangesAsync();

        Assert.That(changed, Is.True);
        var stops = await db.RouteStops.AsNoTracking().Where(s => s.RouteId == route.RouteId).ToListAsync();
        var shared = stops.Single(s => s.Notes == $"StudentId={neighbor.StudentId}");
        var home = stops.Single(s => s.Notes == $"StudentId={special.StudentId}");
        Assert.That(shared.Latitude, Is.EqualTo(38.16m));
        Assert.That(home.Latitude, Is.EqualTo(38.08m));
        Assert.That(home.Longitude, Is.EqualTo(-102.62m));
    }

    [Test]
    public async Task Apply_LeavesCatalogRiderOnTheSharedStop()
    {
        var factory = CreateFactory();
        await using var db = factory.CreateWriteDbContext();
        var route = await AddRouteAsync(db, "AM-Catalog");
        var student = new Student
        {
            StudentName = "TEST_CATALOG",
            Latitude = 38.08m,
            Longitude = -102.62m,
            PickupStopId = 9,
            AmRouteId = route.RouteId,
            Active = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        db.RouteStops.Add(Stop(route.RouteId, 1, "Oak", 38.16m, -102.71m, notes: $"StudentId={student.StudentId}"));
        route.WaypointsJson = "[[38.16,-102.71]]";
        await db.SaveChangesAsync();

        var changed = await AssignedHomeStopSync.ApplyAsync(db, student);

        Assert.That(changed, Is.False);
        var stop = await db.RouteStops.SingleAsync();
        Assert.That(stop.Latitude, Is.EqualTo(38.16m));
        Assert.That(route.WaypointsJson, Is.EqualTo("[[38.16,-102.71]]"));
    }

    private static TestDbContextFactory CreateFactory()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"HomeStopSync_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    private static async Task<Route> AddRouteAsync(BusBuddyDbContext db, string name)
    {
        var route = new Route
        {
            RouteName = name,
            Date = DateTime.Today,
            IsActive = true,
            Session = RouteSession.AM
        };
        db.Routes.Add(route);
        await db.SaveChangesAsync();
        return route;
    }

    private static Student HomeStudent(
        int routeId,
        decimal lat,
        decimal lon,
        bool specialNeeds,
        int? pickupStopId) =>
        new()
        {
            StudentName = "TEST_HOME_RIDER",
            HomeAddress = "100 Test St",
            Latitude = lat,
            Longitude = lon,
            RequiresSpecialNeedsBus = specialNeeds,
            PickupStopId = pickupStopId,
            AmRouteId = routeId,
            Active = true
        };

    private static RouteStop Stop(
        int routeId,
        int order,
        string name,
        decimal lat,
        decimal lon,
        string? notes) =>
        new()
        {
            RouteId = routeId,
            StopName = name,
            StopOrder = order,
            Latitude = lat,
            Longitude = lon,
            Notes = notes,
            StopAddress = "1 Test St",
            ScheduledArrival = TimeSpan.FromHours(7),
            ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(1)),
            CreatedDate = DateTime.UtcNow
        };
}
