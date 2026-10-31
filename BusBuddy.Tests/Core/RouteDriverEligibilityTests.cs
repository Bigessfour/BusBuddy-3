using System;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class RouteDriverEligibilityTests
{
    private DbContextOptions<BusBuddyDbContext> _options = null!;
    private RouteService _routeService = null!;

    private sealed class Factory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public Factory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext()
        {
            var ctx = new BusBuddyDbContext(_options);
            ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            return ctx;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"RouteDriverElig_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var db = new BusBuddyDbContext(_options);
        db.Routes.Add(new Route
        {
            RouteId = 1,
            RouteName = "AM Proof",
            Date = DateTime.UtcNow.Date,
            IsActive = true,
            School = "T",
            AMDriverId = 1
        });
        db.Drivers.AddRange(
            Qualified(1, "Home Driver"),
            Qualified(2, "Qualified Spare"),
            new Driver
            {
                DriverId = 3,
                DriverName = "No S",
                Status = "Active",
                TrainingComplete = true,
                LicenseClass = "Class B",
                SchoolBusEndorsement = false,
                LicenseExpiryDate = DateTime.Today.AddDays(90)
            });
        db.SaveChanges();

        _routeService = new RouteService(new Factory(_options));
    }

    [Test]
    public async Task AssignDriverToRouteAsync_MissingSchoolBusEndorsement_ReturnsFailure()
    {
        var result = await _routeService.AssignDriverToRouteAsync(1, 3, RouteTimeSlot.PM);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("missing school-bus endorsement");
    }

    [Test]
    public async Task AssignDriverToRouteAsync_ValidEligibility_Succeeds()
    {
        var result = await _routeService.AssignDriverToRouteAsync(1, 2, RouteTimeSlot.PM);
        result.IsSuccess.Should().BeTrue();

        await using var db = new BusBuddyDbContext(_options);
        var route = await db.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 1);
        route.PMDriverId.Should().Be(2);
        route.AMDriverId.Should().Be(1);
    }

    [Test]
    public async Task UpdateRouteAsync_ChangingAmDriverToUnqualified_ReturnsFailure_AndPreservesPreviousDriver()
    {
        await using var db = new BusBuddyDbContext(_options);
        var route = await db.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 1);
        route.AMDriverId = 3;

        var result = await _routeService.UpdateRouteAsync(route);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("missing school-bus endorsement");

        await using var verify = new BusBuddyDbContext(_options);
        var stored = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 1);
        stored.AMDriverId.Should().Be(1);
    }

    [Test]
    public async Task UpdateRouteAsync_UnchangedExpiredDriverOnRoute_Succeeds()
    {
        await using (var db = new BusBuddyDbContext(_options))
        {
            var home = await db.Drivers.SingleAsync(d => d.DriverId == 1);
            home.LicenseExpiryDate = DateTime.Today.AddDays(-2);
            await db.SaveChangesAsync();
        }

        await using var read = new BusBuddyDbContext(_options);
        var route = await read.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 1);
        route.RouteName = "AM Proof Renamed";

        var result = await _routeService.UpdateRouteAsync(route);
        result.IsSuccess.Should().BeTrue();

        await using var verify = new BusBuddyDbContext(_options);
        var stored = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 1);
        stored.AMDriverId.Should().Be(1);
        stored.RouteName.Should().Be("AM Proof Renamed");
    }

    [Test]
    public async Task CreateRouteAsync_UnqualifiedDriver_ReturnsFailure()
    {
        var route = new Route
        {
            RouteName = "New With Bad Driver",
            Date = DateTime.UtcNow.Date.AddDays(1),
            IsActive = true,
            School = "T",
            AMDriverId = 3
        };

        var result = await _routeService.CreateRouteAsync(route);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("missing school-bus endorsement");
    }

    [Test]
    public async Task CloneRouteAsync_DropsUnqualifiedDriver_KeepsEligible()
    {
        await using (var db = new BusBuddyDbContext(_options))
        {
            var source = await db.Routes.SingleAsync(r => r.RouteId == 1);
            source.AMDriverId = 1;
            source.PMDriverId = 3;
            await db.SaveChangesAsync();
        }

        var result = await _routeService.CloneRouteAsync(1, DateTime.UtcNow.Date.AddDays(2), "Clone Proof");
        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.AMDriverId.Should().Be(1);
        result.Value.PMDriverId.Should().BeNull();
    }

    [Test]
    public async Task AssignDriverToRouteAsync_ReapplyIncumbent_AllowsGrandfather()
    {
        await using (var db = new BusBuddyDbContext(_options))
        {
            var home = await db.Drivers.SingleAsync(d => d.DriverId == 1);
            home.SchoolBusEndorsement = false;
            await db.SaveChangesAsync();
        }

        var result = await _routeService.AssignDriverToRouteAsync(1, 1, RouteTimeSlot.AM);
        result.IsSuccess.Should().BeTrue();
    }

    private static Driver Qualified(int id, string name) => new()
    {
        DriverId = id,
        DriverName = name,
        Status = "Active",
        TrainingComplete = true,
        LicenseClass = "Class B",
        SchoolBusEndorsement = true,
        LicenseExpiryDate = DateTime.Today.AddDays(90)
    };
}
