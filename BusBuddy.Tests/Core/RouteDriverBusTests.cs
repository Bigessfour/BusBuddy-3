using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class RouteDriverBusTests
    {
        private class TestFactory : IBusBuddyDbContextFactory
        {
            private readonly DbContextOptions<BusBuddyDbContext> _options;
            public TestFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
            public BusBuddyDbContext CreateDbContext()
            {
                var ctx = new BusBuddyDbContext(_options);
                ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return ctx;
            }
            public BusBuddyDbContext CreateWriteDbContext()
            {
                var ctx = new BusBuddyDbContext(_options);
                ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
                return ctx;
            }
        }

        private static DbContextOptions<BusBuddyDbContext> NewOptions()
            => new DbContextOptionsBuilder<BusBuddyDbContext>().UseInMemoryDatabase($"bb_routes_{Guid.NewGuid():N}").Options;

        [Test]
        public async Task Drivers_BasicCrud_Works()
        {
            var options = NewOptions();
            var factory = new TestFactory(options);
            using (var ctx = new BusBuddyDbContext(options))
            {
                await ctx.Database.EnsureCreatedAsync();
                ctx.Drivers.Add(new Driver { DriverName = "Jane Doe", DriversLicenceType = "CDL" });
                await ctx.SaveChangesAsync();
            }

            var driverService = new DriverService(factory, new NoOpCache());
            var all = await driverService.GetAllDriversAsync();
            all.Should().NotBeEmpty();
            all.Any(d => d.DriverName == "Jane Doe").Should().BeTrue();
        }

        [Test]
        public async Task Buses_BasicCrud_Works()
        {
            var options = NewOptions();
            using var ctx = new BusBuddyDbContext(options);
            await ctx.Database.EnsureCreatedAsync();

            var bus = new Bus
            {
                BusNumber = "BUS-101",
                Year = 2020,
                Make = "BlueBird",
                Model = "Vision",
                SeatingCapacity = 60,
                VINNumber = "1HGBH41JXMN109186",
                LicenseNumber = "ABC123"
            };
            ctx.Buses.Add(bus);
            await ctx.SaveChangesAsync();

            var exists = await ctx.Buses.AnyAsync(b => b.BusNumber == "BUS-101");
            exists.Should().BeTrue();
        }

        [Test]
        public async Task Routes_Create_And_Assign_AM_PM_Driver_And_Bus()
        {
            var options = NewOptions();
            var factory = new TestFactory(options);
            using (var seed = new BusBuddyDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                var amDriver = new Driver { DriverName = "AM Driver", DriversLicenceType = "CDL" };
                var pmDriver = new Driver { DriverName = "PM Driver", DriversLicenceType = "CDL" };
                var amBus = new Bus { BusNumber = "AM-1", Year = 2021, Make = "IC", Model = "CE", SeatingCapacity = 50, VINNumber = "1HGBH41JXMN109187", LicenseNumber = "AM1" };
                var pmBus = new Bus { BusNumber = "PM-1", Year = 2022, Make = "Thomas", Model = "C2", SeatingCapacity = 52, VINNumber = "1HGBH41JXMN109188", LicenseNumber = "PM1" };
                seed.AddRange(amDriver, pmDriver, amBus, pmBus);
                await seed.SaveChangesAsync();

                var route = new Route { RouteName = "East", Date = DateTime.Today, IsActive = true };
                seed.Routes.Add(route);
                await seed.SaveChangesAsync();

                route.AMDriverId = amDriver.DriverId;
                route.AMVehicleId = amBus.VehicleId;
                route.PMDriverId = pmDriver.DriverId;
                route.PMVehicleId = pmBus.VehicleId;
                await seed.SaveChangesAsync();
            }

            // Verify via service
            var routeService = new RouteService(factory);
            var all = await routeService.GetAllRoutesAsync();
            all.IsSuccess.Should().BeTrue();
            var routes = all.Value!.ToList();
            routes.Should().HaveCount(1);
            var r = routes[0];
            r.AMDriverId.Should().NotBeNull();
            r.AMVehicleId.Should().NotBeNull();
            r.PMDriverId.Should().NotBeNull();
            r.PMVehicleId.Should().NotBeNull();
        }

        private sealed class NoOpCache : IEnhancedCachingService
        {
            public Task<IReadOnlyList<Bus>> GetAllBusesAsync(Func<Task<IEnumerable<Bus>>> fetchFunc)
                => Wrap(fetchFunc);

            public Task<IReadOnlyList<Driver>> GetAllDriversAsync(Func<Task<IEnumerable<Driver>>> fetchFunc)
                => Wrap(fetchFunc);

            public Task<IReadOnlyList<Route>> GetAllRoutesAsync(Func<Task<IEnumerable<Route>>> fetchFunc)
                => Wrap(fetchFunc);

            public Task<IReadOnlyList<Student>> GetAllStudentsAsync(Func<Task<IEnumerable<Student>>> fetchFunc)
                => Wrap(fetchFunc);

            public Task<IReadOnlyDictionary<string, int>> GetDashboardMetricsAsync(Func<Task<Dictionary<string, int>>> fetchFunc)
                => Task.FromResult((IReadOnlyDictionary<string, int>)new Dictionary<string, int>());

            public Task<Dictionary<string, int>> GetCachedDashboardMetricsAsync()
                => Task.FromResult(new Dictionary<string, int>());

            public void SetDashboardMetricsDirectly(Dictionary<string, int> metrics) { }

            public void InvalidateCache(string key) { }

            public void InvalidateAllCaches() { }

            private static async Task<IReadOnlyList<T>> Wrap<T>(Func<Task<IEnumerable<T>>> fetch)
                => (await fetch()).ToList();
        }
    }
}
