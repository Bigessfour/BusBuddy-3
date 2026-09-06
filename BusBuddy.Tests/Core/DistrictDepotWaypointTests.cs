using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DistrictDepotWaypointTests
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
            .UseInMemoryDatabase($"DepotWp_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    [Test]
    public async Task Rebuild_AmRoute_BookendsDepotAndSchool()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        await using (var seed = factory.CreateDbContext())
        {
            seed.Destinations.Add(new Destination
            {
                DestinationId = 1,
                Name = "Lamar High School",
                DestinationType = DestinationTypes.School,
                Latitude = 38.07m,
                Longitude = -102.63m,
                IsActive = true
            });
            seed.Students.Add(new Student
            {
                StudentName = "Rider",
                Active = true,
                Latitude = 38.08m,
                Longitude = -102.62m,
                AMRoute = "Route AM",
                DestinationId = 1,
                CreatedDate = DateTime.UtcNow
            });
            seed.Routes.Add(new Route
            {
                RouteName = "Route AM",
                Date = DateTime.Today,
                IsActive = true,
                School = "Lamar High School"
            });
            await seed.SaveChangesAsync();
        }

        var depotSettings = Options.Create(new RoutingDistrictSettings
        {
            DepotLatitude = 38.0866,
            DepotLongitude = -102.6201
        });
        var rebuild = new RouteWaypointRebuildService(factory, depotSettings);
        int routeId;
        await using (var ctx = factory.CreateDbContext())
        {
            routeId = ctx.Routes.Single().RouteId;
        }

        var json = await rebuild.RebuildAndPersistAsync(routeId);
        var points = RouteWaypointSerializer.Parse(json!);
        Assert.That(points.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(points[0].Latitude, Is.EqualTo(38.0866).Within(0.001), "AM route starts at depot");
        Assert.That(points[^1].Latitude, Is.EqualTo(38.07).Within(0.001), "AM route ends at school");
    }
}
