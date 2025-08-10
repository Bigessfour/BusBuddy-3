using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class DriverServiceTests : IDisposable
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private DriverService _driverService = null!;
        private Mock<IEnhancedCachingService> _cacheMock = null!;

        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly BusBuddyDbContext _ctx;
            public TestDbContextFactory(BusBuddyDbContext ctx) => _ctx = ctx;
            public BusBuddyDbContext CreateDbContext() => _ctx;
            public BusBuddyDbContext CreateWriteDbContext() => _ctx;
            public void Dispose() { _ctx.Dispose(); }
        }

        [SetUp]
        public void SetUp()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"DriversDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);

            _cacheMock = new Mock<IEnhancedCachingService>();
            _cacheMock.Setup(m => m.GetAllDriversAsync(It.IsAny<Func<Task<IEnumerable<Driver>>>>()))
                .Returns<Func<Task<IEnumerable<Driver>>>>(async fetch => (await fetch()).ToList());

            SeedData();

            _driverService = new DriverService(new TestDbContextFactory(_dbContext), _cacheMock.Object);
        }

        private void SeedData()
        {
            _dbContext.Routes.AddRange(new[]
            {
                new Route { RouteId = 1, RouteName = "Route A", Date = DateTime.Today, IsActive = true, School = "T" },
                new Route { RouteId = 2, RouteName = "Route B", Date = DateTime.Today, IsActive = true, School = "T" }
            });

            _dbContext.Drivers.AddRange(new[]
            {
                new Driver { DriverId = 1, DriverName = "Alice Driver", DriversLicenceType = "Standard", Status = "Active", TrainingComplete = true, LicenseExpiryDate = DateTime.Today.AddDays(90) },
                new Driver { DriverId = 2, DriverName = "Bob Driver", DriversLicenceType = "Standard", Status = "Active", TrainingComplete = true, LicenseExpiryDate = DateTime.Today.AddDays(90) },
                new Driver { DriverId = 3, DriverName = "Inactive", DriversLicenceType = "Standard", Status = "Inactive", TrainingComplete = true, LicenseExpiryDate = DateTime.Today.AddDays(90) },
                new Driver { DriverId = 4, DriverName = "Expired License", DriversLicenceType = "Standard", Status = "Active", TrainingComplete = true, LicenseExpiryDate = DateTime.Today.AddDays(-1) }
            });

            _dbContext.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // Guard against ObjectDisposedException during teardown in case a test already disposed the context
                try
                {
                    if (_dbContext != null)
                    {
                        _dbContext.Database.EnsureDeleted();
                    }
                }
                catch
                {
                    // Ignore teardown exceptions — tests already completed
                }
            }
            finally
            {
                try { _dbContext?.Dispose(); } catch { /* ignore */ }
            }
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            GC.SuppressFinalize(this);
        }

        [Test]
        public async Task GetActiveDriversAsync_FiltersByStatus()
        {
            var active = await _driverService.GetActiveDriversAsync();
            active.Should().OnlyContain(d => d.Status == "Active");
        }

        [Test]
        public async Task SearchDriversAsync_ByNameAndEmail_ReturnsMatches()
        {
            // Ensure some emails/phones for search
            var d = await _dbContext.Drivers.FindAsync(1);
            d!.DriverEmail = "alice@example.com";
            d.DriverPhone = "555-555-5555";
            await _dbContext.SaveChangesAsync();

            var byName = await _driverService.SearchDriversAsync("alice");
            byName.Should().NotBeEmpty();

            var byEmail = await _driverService.SearchDriversAsync("example.com");
            byEmail.Should().NotBeEmpty();
        }

        [Test]
        public async Task AssignDriverToRouteAsync_QualifiedAndAvailable_Assigns()
        {
            // No existing assignments
            var ok = await _driverService.AssignDriverToRouteAsync(1, 1, isAMRoute: true);
            ok.Should().BeTrue();

            var route = await _dbContext.Routes.FindAsync(1);
            route!.AMDriverId.Should().Be(1);
        }

        [Test]
        public void AssignDriverToRouteAsync_UnqualifiedDriver_Throws()
        {
            // Driver 4 has expired license
            Func<Task> act = async () => await _driverService.AssignDriverToRouteAsync(4, 1, true);
            _ = act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not qualified*");
        }

        [Test]
        public async Task IsDriverAvailableForRouteAsync_ReturnsFalseWhenAlreadyAssigned()
        {
            // Assign driver 2 to AM Route 1
            await _driverService.AssignDriverToRouteAsync(2, 1, true);

            var available = await _driverService.IsDriverAvailableForRouteAsync(2, DateTime.Today, true);
            available.Should().BeFalse();
        }

        [Test]
        public async Task UpdateDriverLicenseInfoAsync_ValidatesAndUpdates()
        {
            var ok = await _driverService.UpdateDriverLicenseInfoAsync(1, "LIC123", "B", DateTime.Today.AddYears(1));
            ok.Should().BeTrue();

            var d = await _dbContext.Drivers.FindAsync(1);
            d!.LicenseNumber.Should().Be("LIC123");
            d.LicenseClass.Should().Be("B");
        }

        [Test]
        public async Task UpdateDriverStatusAsync_WithActiveAssignments_Throws()
        {
            // Assign driver 1 to AM Route 1
            await _driverService.AssignDriverToRouteAsync(1, 1, true);

            Func<Task> act = async () => await _driverService.UpdateDriverStatusAsync(1, "Inactive");
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Test]
        public async Task ExportDriversToCsvAsync_IncludesHeader()
        {
            var csv = await _driverService.ExportDriversToCsvAsync();
            csv.Should().StartWith("Driver ID,Driver Name,Phone,Email");
        }
    }
}
