using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class RouteServiceThinApiTests
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

    private sealed class RecordingRoutingService : IRoutingService
    {
        public int DrivePathCalls { get; private set; }

        public Task<DrivePathResult> ComputeDrivePathAsync(
            (double Latitude, double Longitude) origin,
            (double Latitude, double Longitude) destination,
            IReadOnlyList<(double Latitude, double Longitude)> waypoints,
            CancellationToken cancellationToken = default)
        {
            DrivePathCalls++;
            return Task.FromResult(new DrivePathResult
            {
                EncodedPolyline = "thinApiPolyline",
                Points = new[] { origin, destination },
                DistanceMeters = 1200,
                Duration = "90s"
            });
        }

        public Task<IReadOnlyList<RouteMatrixElement>> ComputeRouteMatrixAsync(
            (double Latitude, double Longitude) origin,
            IReadOnlyList<(double Latitude, double Longitude)> destinations,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RouteMatrixElement>>(Array.Empty<RouteMatrixElement>());
    }

    private static TestDbContextFactory CreateFactory()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        return new TestDbContextFactory(
            new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"ThinApi_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
    }

    [Test]
    public async Task GetRoutesByBusId_ReturnsAmAndPmPairings()
    {
        var factory = CreateFactory();
        int amBusId;
        int pmBusId;
        int amRouteId;
        int pmRouteId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var amBus = new Bus
            {
                BusNumber = "AM1", SeatingCapacity = 72, Status = "Active", Year = 2020,
                Make = "IC", Model = "CE", VINNumber = "VINAM1", LicenseNumber = "LAM1",
                CreatedDate = DateTime.UtcNow
            };
            var pmBus = new Bus
            {
                BusNumber = "PM1", SeatingCapacity = 72, Status = "Active", Year = 2020,
                Make = "IC", Model = "CE", VINNumber = "VINPM1", LicenseNumber = "LPM1",
                CreatedDate = DateTime.UtcNow
            };
            ctx.Buses.AddRange(amBus, pmBus);
            await ctx.SaveChangesAsync();
            amBusId = amBus.BusId;
            pmBusId = pmBus.BusId;

            var am = new Route
            {
                RouteName = "North AM",
                Date = DateTime.UtcNow.Date,
                IsActive = true,
                AMVehicleId = amBusId
            };
            var pm = new Route
            {
                RouteName = "North-PM",
                Date = DateTime.UtcNow.Date,
                IsActive = true,
                Session = RouteSession.PM,
                PMVehicleId = pmBusId
            };
            ctx.Routes.AddRange(am, pm);
            await ctx.SaveChangesAsync();
            amRouteId = am.RouteId;
            pmRouteId = pm.RouteId;
        }

        var sut = new RouteService(factory);
        var amResult = await sut.GetRoutesByBusIdAsync(amBusId);
        var pmResult = await sut.GetRoutesByBusIdAsync(pmBusId);
        var noneResult = await sut.GetRoutesByBusIdAsync(amBusId + pmBusId + 99);
        Assert.That(amResult.IsSuccess, Is.True, amResult.Error);
        Assert.That(pmResult.IsSuccess, Is.True, pmResult.Error);
        Assert.That(noneResult.IsSuccess, Is.True, noneResult.Error);

        Assert.That(amResult.Value.Select(r => r.RouteId), Is.EquivalentTo(new[] { amRouteId }));
        Assert.That(pmResult.Value.Select(r => r.RouteId), Is.EquivalentTo(new[] { pmRouteId }));
        Assert.That(noneResult.Value, Is.Empty);
        Assert.That((await sut.GetRoutesByBusIdAsync(0)).IsSuccess, Is.False);
    }

    [Test]
    public async Task AddSecondStop_RefreshesDrivePathThroughInjectedRoutingService()
    {
        var factory = CreateFactory();
        var routing = new RecordingRoutingService();
        var sut = new RouteService(factory, waypointRebuild: null, fitnessEvaluator: null, routing);

        int routeId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var route = new Route { RouteName = "AM-1", Date = DateTime.UtcNow.Date, IsActive = true };
            ctx.Routes.Add(route);
            await ctx.SaveChangesAsync();
            routeId = route.RouteId;
        }

        var first = await sut.AddStopToRouteAsync(routeId, new RouteStop
        {
            StopName = "Home",
            Latitude = 38.10m,
            Longitude = -102.70m,
            StopOrder = 1,
            ScheduledArrival = TimeSpan.FromHours(7),
            ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(1)),
            CreatedDate = DateTime.UtcNow
        });
        Assert.That(first.IsSuccess, Is.True, first.Error);
        Assert.That(routing.DrivePathCalls, Is.EqualTo(0));

        var second = await sut.AddStopToRouteAsync(routeId, new RouteStop
        {
            StopName = "School",
            Latitude = 38.08m,
            Longitude = -102.62m,
            StopOrder = 2,
            ScheduledArrival = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(20)),
            ScheduledDeparture = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(21)),
            CreatedDate = DateTime.UtcNow
        });
        Assert.That(second.IsSuccess, Is.True, second.Error);
        Assert.That(routing.DrivePathCalls, Is.EqualTo(1));

        await using var verify = factory.CreateDbContext();
        var persisted = await verify.Routes.AsNoTracking().FirstAsync(r => r.RouteId == routeId);
        Assert.That(persisted.WaypointsJson, Does.Contain("thinApiPolyline"));
    }

    [Test]
    public void ProductionDi_RegistersRouteServiceWithIRoutingService()
    {
        var greedy = typeof(RouteService).GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        Assert.That(
            greedy.GetParameters().Select(p => p.ParameterType),
            Does.Contain(typeof(IRoutingService)));
    }
}
