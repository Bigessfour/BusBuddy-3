using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Driver;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Drivers roster VM: selection-gated toolbar commands and home-route eligibility warning smoke.
/// Grandfather assign rules stay in Core; this only proves the grid warning surface.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DriversViewModelTests
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
    public async Task EditAndDeleteCommands_RequireSelection()
    {
        var options = NewDbOptions();
        var drivers = new List<Driver> { Qualified(1, "Alice", schoolBus: true) };
        var driverService = new Mock<IDriverService>();
        driverService.Setup(s => s.GetAllDriversAsync()).ReturnsAsync(drivers);

        var vm = new DriversViewModel(new TestDbContextFactory(options), driverService.Object);
        await vm.LoadDriversAsync();

        vm.EditDriverCommand.CanExecute(null).Should().BeFalse();
        vm.DeleteDriverCommand.CanExecute(null).Should().BeFalse();
        vm.ViewLicenseCommand.CanExecute(null).Should().BeFalse();
        vm.TrainingRecordsCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedDriver = vm.Drivers[0];

        vm.HasSelectedDriver.Should().BeTrue();
        vm.EditDriverCommand.CanExecute(null).Should().BeTrue();
        vm.DeleteDriverCommand.CanExecute(null).Should().BeTrue();
        vm.ViewLicenseCommand.CanExecute(null).Should().BeTrue();
        vm.TrainingRecordsCommand.CanExecute(null).Should().BeTrue();
        vm.RefreshCommand.CanExecute(null).Should().BeTrue();
        vm.AddDriverCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public async Task LoadDrivers_HomeRouteMissingEndorsement_SetsWarning_WithoutClearingRoute()
    {
        var options = NewDbOptions();
        await using (var db = new BusBuddyDbContext(options))
        {
            db.Drivers.AddRange(
                Qualified(1, "Home No S", schoolBus: false),
                Qualified(2, "Spare Ok", schoolBus: true));
            db.Routes.Add(new Route
            {
                RouteId = 10,
                RouteName = "AM Proof",
                Date = DateTime.UtcNow.Date,
                IsActive = true,
                School = "Wiley",
                AMDriverId = 1
            });
            await db.SaveChangesAsync();
        }

        var roster = new List<Driver>
        {
            Qualified(1, "Home No S", schoolBus: false),
            Qualified(2, "Spare Ok", schoolBus: true),
            Qualified(3, "Unassigned No S", schoolBus: false)
        };
        var driverService = new Mock<IDriverService>();
        driverService.Setup(s => s.GetAllDriversAsync()).ReturnsAsync(roster);

        var vm = new DriversViewModel(new TestDbContextFactory(options), driverService.Object);
        await vm.LoadDriversAsync();

        var home = vm.Drivers.Should().ContainSingle(d => d.DriverId == 1).Subject;
        home.HomeRouteEligibilityWarning.Should().Contain("school-bus endorsement");

        vm.Drivers.Should().ContainSingle(d => d.DriverId == 2)
            .Which.HomeRouteEligibilityWarning.Should().BeNull();
        vm.Drivers.Should().ContainSingle(d => d.DriverId == 3)
            .Which.HomeRouteEligibilityWarning.Should().BeNull(
                "unassigned drivers do not get a home-route warning");

        await using (var verify = new BusBuddyDbContext(options))
        {
            var route = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 10);
            route.AMDriverId.Should().Be(1, "warning must not clear grandfathered route FKs");
        }
    }

    [Test]
    public async Task LoadDrivers_PmHomeRouteExpiredLicense_SetsWarning_WithoutClearingRoute()
    {
        var options = NewDbOptions();
        await using (var db = new BusBuddyDbContext(options))
        {
            db.Drivers.Add(Qualified(5, "PM Expired", schoolBus: true, expiry: DateTime.Today.AddDays(-1)));
            db.Routes.Add(new Route
            {
                RouteId = 20,
                RouteName = "PM Proof",
                Date = DateTime.UtcNow.Date,
                IsActive = true,
                School = "Wiley",
                PMDriverId = 5
            });
            await db.SaveChangesAsync();
        }

        var roster = new List<Driver>
        {
            Qualified(5, "PM Expired", schoolBus: true, expiry: DateTime.Today.AddDays(-1))
        };
        var driverService = new Mock<IDriverService>();
        driverService.Setup(s => s.GetAllDriversAsync()).ReturnsAsync(roster);

        var vm = new DriversViewModel(new TestDbContextFactory(options), driverService.Object);
        await vm.LoadDriversAsync();

        var pm = vm.Drivers.Should().ContainSingle(d => d.DriverId == 5).Subject;
        pm.HomeRouteEligibilityWarning.Should().Contain("expired license");

        await using (var verify = new BusBuddyDbContext(options))
        {
            var route = await verify.Routes.AsNoTracking().SingleAsync(r => r.RouteId == 20);
            route.PMDriverId.Should().Be(5, "warning must not clear grandfathered PM route FKs");
        }
    }

    private static DbContextOptions<BusBuddyDbContext> NewDbOptions() =>
        new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"DriversVm_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static Driver Qualified(int id, string name, bool schoolBus, DateTime? expiry = null) =>
        new()
        {
            DriverId = id,
            DriverName = name,
            Status = "Active",
            TrainingComplete = true,
            LicenseClass = "Class B",
            SchoolBusEndorsement = schoolBus,
            LicenseExpiryDate = expiry ?? DateTime.Today.AddDays(90)
        };
}
