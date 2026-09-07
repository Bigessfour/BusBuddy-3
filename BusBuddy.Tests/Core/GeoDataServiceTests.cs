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
