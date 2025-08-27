using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class DriverServiceTests : DatabaseTestBase
    {
        private DriverService _driverService = null!;
        private Mock<IEnhancedCachingService> _mockCachingService = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // Set up mock caching service
            _mockCachingService = new Mock<IEnhancedCachingService>();
            _mockCachingService
                .Setup(x => x.GetAllDriversAsync(It.IsAny<Func<Task<IEnumerable<Driver>>>>()))
                .Returns((Func<Task<IEnumerable<Driver>>> fetchFunc) =>
                {
                    // For testing, return the seeded data directly
                    var testDrivers = new[]
                    {
                        new Driver
                        {
                            DriverId = 1,
                            DriverName = "John Smith",
                            LicenseNumber = "DL123456",
                            DriverPhone = "555-010-1234",
                            Status = "Active",
                            DriversLicenceType = "Class B",
                            TrainingComplete = true
                        },
                        new Driver
                        {
                            DriverId = 2,
                            DriverName = "Jane Doe",
                            LicenseNumber = "DL789012",
                            DriverPhone = "555-010-5678",
                            Status = "Active",
                            DriversLicenceType = "Class B",
                            TrainingComplete = true
                        },
                        new Driver
                        {
                            DriverId = 3,
                            DriverName = "Bob Johnson",
                            LicenseNumber = "DL345678",
                            DriverPhone = "555-010-9012",
                            Status = "Inactive",
                            DriversLicenceType = "Class B",
                            TrainingComplete = false
                        }
                    };
                    return Task.FromResult((IReadOnlyList<Driver>)testDrivers.ToList());
                });

            // Create the service using the inherited context factory
            _driverService = new DriverService(ContextFactory, _mockCachingService.Object);
        }



        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        private bool _disposed;

        public override void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                base.TearDown();
                base.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        [Test]
        public async Task GetAllDriversAsync_ReturnsAllDrivers()
        {
            // Act
            var drivers = await _driverService.GetAllDriversAsync();

            // Assert
            drivers.Should().NotBeNull();
            drivers.Should().HaveCount(3);
        }

        [Test]
        public async Task GetDriverByIdAsync_ExistingId_ReturnsDriver()
        {
            // Act
            var driver = await _driverService.GetDriverByIdAsync(1);

            // Assert
            driver.Should().NotBeNull();
            driver!.DriverName.Should().Be("John Driver");
            driver.LicenseNumber.Should().Be("DL123456");
        }

        [Test]
        public async Task GetDriverByIdAsync_NonExistingId_ReturnsNull()
        {
            // Act
            var driver = await _driverService.GetDriverByIdAsync(999);

            // Assert
            driver.Should().BeNull();
        }

        [Test]
        public async Task GetActiveDriversAsync_ReturnsOnlyActiveDrivers()
        {
            // Act
            var activeDrivers = await _driverService.GetActiveDriversAsync();

            // Assert
            activeDrivers.Should().NotBeNull();
            activeDrivers.Count.Should().Be(2);
            activeDrivers.All(d => d.Status == "Active").Should().BeTrue();
            activeDrivers.Select(d => d.DriverName).Should().BeEquivalentTo(new[] { "John Driver", "Jane Driver" });
        }

        [Test]
        public async Task AddDriverAsync_ValidDriver_PersistsAndSetsDefaults()
        {
            // Arrange
            var newDriver = new Driver
            {
                DriverName = "Alice Wilson",
                LicenseNumber = "DL999999",
                DriverPhone = "555-019-9999",
                DriversLicenceType = "Class B",
                TrainingComplete = true
            };

            // Act
            var addedDriver = await _driverService.AddDriverAsync(newDriver);

            // Assert
            addedDriver.Should().NotBeNull();
            addedDriver.DriverId.Should().BeGreaterThan(0);
            addedDriver.Status.Should().Be("Active"); // Default value
            addedDriver.DriverName.Should().Be("Alice Wilson");

            // Verify in database
            using var context = ContextFactory.CreateDbContext();
            var fromDb = await context.Drivers.FindAsync(addedDriver.DriverId);
            fromDb.Should().NotBeNull();
            fromDb!.DriverName.Should().Be("Alice Wilson");
        }

        [Test]
        public async Task UpdateDriverAsync_ValidDriver_UpdatesSuccessfully()
        {
            // Arrange
            const int driverId = 1;
            var driverToUpdate = new Driver
            {
                DriverId = driverId,
                DriverName = "John Driver Jr.",
                LicenseNumber = "DL123456",
                DriverPhone = "555-010-1234",
                Status = "Active",
                DriversLicenceType = "Class B",
                TrainingComplete = true
            };

            // Act
            var result = await _driverService.UpdateDriverAsync(driverToUpdate);

            // Assert
            result.Should().BeTrue();

            // Verify in database
            using var context = ContextFactory.CreateDbContext();
            var updatedDriver = await context.Drivers.FindAsync(driverId);
            updatedDriver.Should().NotBeNull();
            updatedDriver!.DriverName.Should().Be("John Driver Jr.");
        }

        [Test]
        public async Task DeleteDriverAsync_ValidId_DeletesSuccessfully()
        {
            // Arrange
            const int driverId = 3;

            // Act
            var result = await _driverService.DeleteDriverAsync(driverId);

            // Assert
            result.Should().BeTrue();

            // Verify in database
            using var context = ContextFactory.CreateDbContext();
            var deletedDriver = await context.Drivers.FindAsync(driverId);
            deletedDriver.Should().BeNull();
        }

        [Test]
        public async Task SearchDriversAsync_ByName_ReturnsMatchingDrivers()
        {
            // Act
            var results = await _driverService.SearchDriversAsync("John");

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(1);
            results.First().DriverName.Should().Be("John Driver");
        }

        [Test]
        public async Task SearchDriversAsync_EmptySearchTerm_ReturnsAllDrivers()
        {
            // Act
            var results = await _driverService.SearchDriversAsync("");

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(3);
        }

        [Test]
        public async Task UpdateDriverStatusAsync_ValidStatus_UpdatesSuccessfully()
        {
            // Arrange
            const int driverId = 1;
            const string newStatus = "On Leave";

            // Act
            var result = await _driverService.UpdateDriverStatusAsync(driverId, newStatus);

            // Assert
            result.Should().BeTrue();

            // Verify in database
            using var context = ContextFactory.CreateDbContext();
            var updatedDriver = await context.Drivers.FindAsync(driverId);
            updatedDriver.Should().NotBeNull();
            updatedDriver!.Status.Should().Be(newStatus);
        }

        [Test]
        public async Task UpdateDriverLicenseInfoAsync_ValidInfo_UpdatesSuccessfully()
        {
            // Arrange
            const int driverId = 1;
            const string newLicenseNumber = "DL111111";
            const string newLicenseClass = "Class A";
            var newExpirationDate = DateTime.Today.AddYears(2);

            // Act
            var result = await _driverService.UpdateDriverLicenseInfoAsync(driverId, newLicenseNumber, newLicenseClass, newExpirationDate);

            // Assert
            result.Should().BeTrue();

            // Verify in database
            using var context = ContextFactory.CreateDbContext();
            var updatedDriver = await context.Drivers.FindAsync(driverId);
            updatedDriver.Should().NotBeNull();
            updatedDriver!.LicenseNumber.Should().Be(newLicenseNumber);
            updatedDriver.DriversLicenceType.Should().Be(newLicenseClass);
        }

        [Test]
        public async Task ValidateDriverAsync_InvalidDriver_ReturnsErrors()
        {
            // Arrange
            var invalidDriver = new Driver
            {
                DriverName = "",
                LicenseNumber = "",
                DriverPhone = "invalid-phone",
                DriversLicenceType = ""
            };

            // Act
            var errors = await _driverService.ValidateDriverAsync(invalidDriver);

            // Assert
            errors.Should().NotBeNull();
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("name", StringComparison.OrdinalIgnoreCase));
            errors.Should().Contain(e => e.Contains("license", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public async Task ValidateDriverAsync_ValidDriver_ReturnsNoErrors()
        {
            // Arrange
            var validDriver = new Driver
            {
                DriverName = "Valid Driver",
                LicenseNumber = "DL123456",
                DriverPhone = "555-123-4567",
                DriversLicenceType = "Class B",
                TrainingComplete = true
            };

            // Act
            var errors = await _driverService.ValidateDriverAsync(validDriver);

            // Assert
            errors.Should().NotBeNull();
            errors.Should().BeEmpty();
        }

        [Test]
        public async Task GetDriverStatisticsAsync_ReturnsCorrectCounts()
        {
            // Act
            var stats = await _driverService.GetDriverStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.Should().ContainKey("TotalDrivers");
            stats.Should().ContainKey("ActiveDrivers");
            stats.Should().ContainKey("QualifiedDrivers");

            stats["TotalDrivers"].Should().Be(3);
            stats["ActiveDrivers"].Should().Be(2);
            stats["QualifiedDrivers"].Should().Be(2); // Drivers with TrainingComplete = true
        }

        [Test]
        public async Task AssignDriverToRouteAsync_ValidIds_AssignsSuccessfully()
        {
            // Arrange
            const int driverId = 1;
            const int routeId = 1;
            const bool isAMRoute = true;

            // Act
            var result = await _driverService.AssignDriverToRouteAsync(driverId, routeId, isAMRoute);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task IsDriverAvailableForRouteAsync_AvailableDriver_ReturnsTrue()
        {
            // Arrange
            const int driverId = 1;
            var routeDate = DateTime.Today;
            const bool isAMRoute = true;

            // Act
            var result = await _driverService.IsDriverAvailableForRouteAsync(driverId, routeDate, isAMRoute);

            // Assert
            result.Should().BeTrue();
        }

        [Test]
        public async Task GetDriversByQualificationStatusAsync_ValidStatus_ReturnsMatchingDrivers()
        {
            // Act
            var qualifiedDrivers = await _driverService.GetDriversByQualificationStatusAsync("Qualified");

            // Assert
            qualifiedDrivers.Should().NotBeNull();
            qualifiedDrivers.Should().HaveCountGreaterThan(0);
            qualifiedDrivers.All(d => d.QualificationStatus == "Qualified").Should().BeTrue();
        }

        [Test]
        public async Task GetDriversNeedingRenewalAsync_ReturnsDriversNeedingRenewal()
        {
            // Act
            var driversNeedingRenewal = await _driverService.GetDriversNeedingRenewalAsync();

            // Assert
            driversNeedingRenewal.Should().NotBeNull();
            // This would depend on the license expiration dates in the test data
        }
    }
}
