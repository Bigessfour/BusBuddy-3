using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class PickupStopServiceTests
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
            .UseInMemoryDatabase($"PickupStops_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    [Test]
    public async Task AddStop_PersistsCatalogRowWithGps()
    {
        var sut = new PickupStopService(CreateFactory());
        var stop = await sut.AddStopAsync("Oak & 4th", "NE corner", 38.0872m, -102.6208m, PickupStopTypes.Corner);
        Assert.That(stop.PickupStopId, Is.GreaterThan(0));
        Assert.That(stop.Name, Is.EqualTo("Oak & 4th"));
        Assert.That(stop.Latitude, Is.EqualTo(38.0872m));
    }

    [Test]
    public async Task FindNearest_ReturnsStopWithinRadius()
    {
        var factory = CreateFactory();
        var sut = new PickupStopService(factory);
        await sut.AddStopAsync("Oak & 4th", null, 38.0872m, -102.6208m);

        var near = await sut.FindNearestAsync(38.08725, -102.62085, maxMeters: 50);
        Assert.That(near, Is.Not.Null);
        Assert.That(near!.Name, Is.EqualTo("Oak & 4th"));

        var far = await sut.FindNearestAsync(39.0, -103.0, maxMeters: 400);
        Assert.That(far, Is.Null);
    }

    [Test]
    public async Task UpdateStop_PersistsNameAndPin()
    {
        var sut = new PickupStopService(CreateFactory());
        var created = await sut.AddStopAsync("Oak & 4th", "NE corner", 38.0872m, -102.6208m);

        var updated = await sut.UpdateStopAsync(
            created.PickupStopId,
            "Oak & 5th",
            "NW corner",
            38.09m,
            -102.63m,
            PickupStopTypes.Intersection,
            "moved a block");

        Assert.That(updated.Name, Is.EqualTo("Oak & 5th"));
        Assert.That(updated.StopType, Is.EqualTo(PickupStopTypes.Intersection));
        var loaded = await sut.GetByIdAsync(created.PickupStopId);
        Assert.That(loaded!.Latitude, Is.EqualTo(38.09m));
        Assert.That(loaded.Notes, Is.EqualTo("moved a block"));
    }

    [Test]
    public async Task RetireStop_DeletesWhenUnused()
    {
        var sut = new PickupStopService(CreateFactory());
        var created = await sut.AddStopAsync("Oak & 4th", null, 38.0872m, -102.6208m);

        var result = await sut.RetireStopAsync(created.PickupStopId);

        Assert.That(result, Is.EqualTo(CatalogDeleteResult.Deleted));
        Assert.That(await sut.GetByIdAsync(created.PickupStopId), Is.Null);
        Assert.That(await sut.GetActiveStopsAsync(), Is.Empty);
    }

    [Test]
    public async Task RetireStop_RetiresWhenStudentsStillAssigned()
    {
        var factory = CreateFactory();
        var sut = new PickupStopService(factory);
        var created = await sut.AddStopAsync("Oak & 4th", null, 38.0872m, -102.6208m);

        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Students.Add(new Student
            {
                StudentName = "TEST_STUDENT_01",
                StudentNumber = "TEST-0001",
                PickupStopId = created.PickupStopId,
                SchoolYear = "2026-2027",
            });
            await ctx.SaveChangesAsync();
        }

        var result = await sut.RetireStopAsync(created.PickupStopId);

        Assert.That(result, Is.EqualTo(CatalogDeleteResult.Retired));
        var loaded = await sut.GetByIdAsync(created.PickupStopId);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Active, Is.False);
        Assert.That(await sut.GetActiveStopsAsync(), Is.Empty);
    }

    [Test]
    public async Task RetireStop_RetiresWhenPublishedRouteStopStillNamesIt()
    {
        var factory = CreateFactory();
        var sut = new PickupStopService(factory);
        var created = await sut.AddStopAsync("Oak & 4th", null, 38.0872m, -102.6208m);

        await using (var ctx = factory.CreateWriteDbContext())
        {
            var route = new Route { RouteName = "AM-1", Date = DateTime.UtcNow.Date, IsActive = true };
            ctx.Routes.Add(route);
            await ctx.SaveChangesAsync();
            ctx.RouteStops.Add(new RouteStop
            {
                RouteId = route.RouteId,
                StopName = created.Name,
                StopOrder = 1,
                ScheduledArrival = TimeSpan.FromHours(7),
                ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(1)),
                CreatedDate = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var result = await sut.RetireStopAsync(created.PickupStopId);

        Assert.That(result, Is.EqualTo(CatalogDeleteResult.Retired));
        Assert.That((await sut.GetByIdAsync(created.PickupStopId))!.Active, Is.False);
    }

    [Test]
    public void UpdateStop_RejectsFallbackCoordinates()
    {
        var sut = new PickupStopService(CreateFactory());
        var created = sut.AddStopAsync("Oak & 4th", null, 38.0872m, -102.6208m).GetAwaiter().GetResult();
        Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(() =>
            sut.UpdateStopAsync(created.PickupStopId, "Oak", null, 0m, 0m)));
    }

    [Test]
    public async Task GetActiveStops_ExcludesInactive()
    {
        var factory = CreateFactory();
        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.PickupStops.Add(new PickupStop
            {
                Name = "Inactive",
                Latitude = 1,
                Longitude = 1,
                StopType = PickupStopTypes.Corner,
                Active = false
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new PickupStopService(factory);
        await sut.AddStopAsync("Active Corner", null, 38.1m, -102.6m);
        var list = await sut.GetActiveStopsAsync();
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].Name, Is.EqualTo("Active Corner"));
    }
}
