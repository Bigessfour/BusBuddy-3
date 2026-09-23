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
public class GeoDataServiceTests
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
            .UseInMemoryDatabase($"GeoData_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    [Test]
    public async Task GetRoutesWithGeoData_PersistsStopDerivedWaypointsWhenJsonEmpty()
    {
        var factory = CreateFactory();
        int routeId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM-1",
                Date = DateTime.Today,
                IsActive = true
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            seed.RouteStops.AddRange(
                Stop(routeId, 1, 38.15m, -102.72m),
                Stop(routeId, 2, 38.16m, -102.71m));
            await seed.SaveChangesAsync();
        }

        var loaded = await new GeoDataService(factory).GetRoutesWithGeoDataAsync();
        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].WaypointsJson, Is.Not.Null.And.Not.Empty);

        await using var verify = factory.CreateDbContext();
        var persisted = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId);
        Assert.That(persisted.WaypointsJson, Is.EqualTo(loaded[0].WaypointsJson));
        var points = RouteWaypointSerializer.Parse(persisted.WaypointsJson);
        Assert.That(points, Has.Count.EqualTo(2));
        Assert.That(points[0].Latitude, Is.EqualTo(38.15).Within(0.0001));
    }

    [Test]
    public async Task GetRouteGeoData_PersistsStopDerivedWaypointsWhenJsonEmpty()
    {
        var factory = CreateFactory();
        int routeId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "PM-1",
                Date = DateTime.Today,
                IsActive = true
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            seed.RouteStops.AddRange(
                Stop(routeId, 1, 38.20m, -102.80m),
                Stop(routeId, 2, 38.21m, -102.79m));
            await seed.SaveChangesAsync();
        }

        var loaded = await new GeoDataService(factory).GetRouteGeoDataAsync(routeId);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.WaypointsJson, Is.Not.Null.And.Not.Empty);

        await using var verify = factory.CreateDbContext();
        var persisted = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId);
        Assert.That(persisted.WaypointsJson, Is.EqualTo(loaded.WaypointsJson));
        Assert.That(RouteWaypointSerializer.Parse(persisted.WaypointsJson), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetRoutesWithGeoData_DoesNotOverwriteStoredWaypoints()
    {
        var factory = CreateFactory();
        var stored = RouteWaypointSerializer.FromPairs([(38.0, -102.0), (38.1, -102.1)]);
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM-Keep",
                Date = DateTime.Today,
                IsActive = true,
                WaypointsJson = stored
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            seed.RouteStops.Add(Stop(route.RouteId, 1, 39.0m, -103.0m));
            await seed.SaveChangesAsync();
        }

        var loaded = await new GeoDataService(factory).GetRoutesWithGeoDataAsync();
        Assert.That(loaded[0].WaypointsJson, Is.EqualTo(stored));

        await using var verify = factory.CreateDbContext();
        Assert.That((await verify.Routes.AsNoTracking().SingleAsync()).WaypointsJson, Is.EqualTo(stored));
    }

    [Test]
    public async Task GetDistrictMap_IncludesValidatedSchoolsAndStops_OmitsZeroAndListsNeedsValidation()
    {
        var factory = CreateFactory();
        await using (var seed = factory.CreateWriteDbContext())
        {
            seed.Destinations.AddRange(
                School("Wiley School", 38.1535m, -102.7195m),
                School("Unvalidated School", null, null),
                School("Null Island", 0m, 0m),
                new Destination
                {
                    Name = "Museum",
                    DestinationType = DestinationTypes.FieldTrip,
                    Address = "1 Main",
                    City = "Lamar",
                    State = "CO",
                    ZipCode = "81052",
                    IsActive = true,
                    Latitude = 38.2m,
                    Longitude = -102.6m
                },
                School("Closed School", 38.11m, -102.61m, active: false));
            seed.PickupStops.AddRange(
                new PickupStop { Name = "Oak & 4th", Latitude = 38.16m, Longitude = -102.71m, Active = true, StopType = PickupStopTypes.Corner },
                new PickupStop { Name = "No GPS Stop", Latitude = 0m, Longitude = 0m, Active = true, StopType = PickupStopTypes.Corner });
            await seed.SaveChangesAsync();
        }

        var snapshot = await new GeoDataService(factory).GetDistrictMapAsync(null);

        Assert.That(snapshot.SelectedRoute, Is.Null);
        Assert.That(snapshot.Schools.Select(s => s.Name), Is.EqualTo(new[] { "Wiley School" }));
        Assert.That(snapshot.CatalogStops.Select(s => s.Name), Is.EqualTo(new[] { "Oak & 4th" }));
        Assert.That(snapshot.NeedsValidation, Does.Contain("School: Unvalidated School"));
        Assert.That(snapshot.NeedsValidation, Does.Contain("School: Null Island"));
        Assert.That(snapshot.NeedsValidation, Does.Contain("Pickup: No GPS Stop"));
        Assert.That(snapshot.NeedsValidation.Any(n => n.Contains("Wiley School", StringComparison.Ordinal)), Is.False);
        Assert.That(snapshot.NeedsValidation.Any(n => n.Contains("Closed School", StringComparison.Ordinal)), Is.False);
        Assert.That(snapshot.NeedsValidation.Any(n => n.Contains("Museum", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public async Task GetDistrictMap_HomesOnlyForRequestedRoute_AndDoesNotOverwriteStoredPath()
    {
        var factory = CreateFactory();
        var stored = RouteWaypointSerializer.FromEncodedPolyline(
            "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
            [(38.15, -102.72), (38.16, -102.71)]);
        int routeId;
        int otherId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM-5",
                Date = DateTime.Today,
                IsActive = true,
                Session = RouteSession.AM,
                WaypointsJson = stored,
                Distance = 12.4m,
                EstimatedDuration = 36
            };
            var other = new Route
            {
                RouteName = "AM-9",
                Date = DateTime.Today,
                IsActive = true,
                Session = RouteSession.AM
            };
            seed.Routes.AddRange(route, other);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            otherId = other.RouteId;
            seed.RouteStops.Add(Stop(routeId, 1, 39.0m, -103.0m));
            seed.Students.AddRange(
                new Student
                {
                    StudentName = "Ada",
                    Latitude = 38.14m,
                    Longitude = -102.73m,
                    AmRouteId = routeId,
                    Active = true
                },
                new Student
                {
                    StudentName = "Bea",
                    Latitude = 38.15m,
                    Longitude = -102.72m,
                    AmRouteId = otherId,
                    Active = true
                });
            await seed.SaveChangesAsync();
        }

        var service = new GeoDataService(factory);
        var selected = await service.GetDistrictMapAsync(routeId);
        var none = await service.GetDistrictMapAsync(null);

        Assert.That(none.SelectedRoute, Is.Null);
        Assert.That(selected.SelectedRoute, Is.Not.Null);
        Assert.That(selected.SelectedRoute!.WaypointsJson, Is.EqualTo(stored));
        Assert.That(selected.SelectedRoute.DistanceMiles, Is.EqualTo(12.4m));
        Assert.That(selected.SelectedRoute.DurationMinutes, Is.EqualTo(36));
        Assert.That(selected.SelectedRoute.Homes.Select(h => h.StudentName), Is.EqualTo(new[] { "Ada" }));
        Assert.That(selected.SelectedRoute.Homes[0].Pins, Has.Count.EqualTo(1));
        Assert.That(selected.SelectedRoute.Homes[0].Pins[0].AtPickup, Is.False);

        await using var verify = factory.CreateDbContext();
        Assert.That((await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId)).WaypointsJson, Is.EqualTo(stored));
    }

    [Test]
    public async Task GetDistrictMap_DerivedPathOmitsUnassignedStudentHome()
    {
        var factory = CreateFactory();
        int routeId;
        await using (var seed = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM-Home",
                Date = DateTime.Today,
                IsActive = true,
                Session = RouteSession.AM
            };
            seed.Routes.Add(route);
            await seed.SaveChangesAsync();
            routeId = route.RouteId;
            var rider = new Student
            {
                StudentName = "Ada",
                Latitude = 38.14m,
                Longitude = -102.73m,
                AmRouteId = routeId,
                Active = true
            };
            seed.Students.Add(rider);
            await seed.SaveChangesAsync();
            seed.RouteStops.AddRange(
                Stop(routeId, 1, 38.15m, -102.72m),
                new RouteStop
                {
                    RouteId = routeId,
                    StopName = "Bea Home",
                    StopOrder = 2,
                    Latitude = 38.90m,
                    Longitude = -102.10m,
                    Notes = "StudentId=999999",
                    ScheduledArrival = TimeSpan.FromHours(7),
                    ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(1)),
                    CreatedDate = DateTime.UtcNow
                });
            await seed.SaveChangesAsync();
        }

        var snapshot = await new GeoDataService(factory).GetDistrictMapAsync(routeId);
        var points = RouteWaypointSerializer.Parse(snapshot.SelectedRoute!.WaypointsJson);
        Assert.That(points, Has.Count.EqualTo(1));
        Assert.That(points[0].Latitude, Is.EqualTo(38.15).Within(0.0001));
        Assert.That(snapshot.SelectedRoute.PublishedStops.Select(s => s.Name), Is.EqualTo(new[] { "Stop 1" }));

        await using var verify = factory.CreateDbContext();
        var persisted = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == routeId);
        Assert.That(RouteWaypointSerializer.Parse(persisted.WaypointsJson), Has.Count.EqualTo(1));
    }

    private static Destination School(string name, decimal? lat, decimal? lon, bool active = true) => new()
    {
        Name = name,
        DestinationType = DestinationTypes.School,
        Address = "1 School St",
        City = "Lamar",
        State = "CO",
        ZipCode = "81052",
        IsActive = active,
        Latitude = lat,
        Longitude = lon
    };

    private static RouteStop Stop(int routeId, int order, decimal lat, decimal lon) => new()
    {
        RouteId = routeId,
        StopName = $"Stop {order}",
        StopOrder = order,
        Latitude = lat,
        Longitude = lon,
        ScheduledArrival = TimeSpan.FromHours(7),
        ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(1)),
        CreatedDate = DateTime.UtcNow
    };
}
