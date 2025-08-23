using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Unit tests for FleetMonitoringService
    /// Tests fleet status monitoring, GPS tracking, and maintenance alerts
    /// </summary>
    [TestFixture]
    public class FleetMonitoringServiceTests : IDisposable
    {
        private readonly BusBuddyDbContext _context;
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IBusCachingService _cacheService;
        private readonly Mock<IGeoDataService> _mockGeoDataService;
        private readonly FleetMonitoringService _fleetService;

        public FleetMonitoringServiceTests()
        {
            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BusBuddyDbContext(options);
            _contextFactory = new TestDbContextFactory(_context);

            // Create real caching service with in-memory cache
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            _cacheService = new BusCachingService(memoryCache);

            // Mock geo data service
            _mockGeoDataService = new Mock<IGeoDataService>();

            _fleetService = new FleetMonitoringService(_contextFactory, _cacheService, _mockGeoDataService.Object);

            SeedTestData();
        }

        private void SeedTestData()
        {
            var buses = new List<Bus>
            {
                new Bus
                {
                    VehicleId = 1,
                    BusNumber = "001",
                    Status = "Active",
                    GPSTracking = true,
                    CurrentLatitude = 40.7128m,
                    CurrentLongitude = -74.0060m,
                    NextMaintenanceDue = DateTime.Today.AddDays(30),
                    DateLastInspection = DateTime.Today.AddDays(-180),
                    Year = 2020,
                    Make = "Blue Bird",
                    Model = "Vision"
                },
                new Bus
                {
                    VehicleId = 2,
                    BusNumber = "002",
                    Status = "Maintenance",
                    GPSTracking = false,
                    NextMaintenanceDue = DateTime.Today.AddDays(-5), // Overdue
                    DateLastInspection = DateTime.Today.AddDays(-400), // Overdue inspection
                    Year = 2019,
                    Make = "Thomas",
                    Model = "Saf-T-Liner"
                },
                new Bus
                {
                    VehicleId = 3,
                    BusNumber = "003",
                    Status = "Out of Service",
                    GPSTracking = true,
                    CurrentLatitude = null, // GPS offline
                    CurrentLongitude = null,
                    NextMaintenanceDue = DateTime.Today.AddDays(15),
                    DateLastInspection = DateTime.Today.AddDays(-90),
                    Year = 2021,
                    Make = "IC Bus",
                    Model = "CE"
                }
            };

            _context.Buses.AddRange(buses);
            _context.SaveChanges();
        }

    [Test]
    public async Task GetFleetStatusAsync_ShouldReturnCorrectStatistics()
        {
            // Act
            var fleetStatus = await _fleetService.GetFleetStatusAsync();

            // Assert
            Assert.That(fleetStatus, Is.Not.Null);
            Assert.That(fleetStatus.TotalBuses, Is.EqualTo(3));
            Assert.That(fleetStatus.ActiveBuses, Is.EqualTo(1));
            Assert.That(fleetStatus.BusesInMaintenance, Is.EqualTo(1));
            Assert.That(fleetStatus.OutOfServiceBuses, Is.EqualTo(1));
            Assert.That(fleetStatus.GpsEnabledBuses, Is.EqualTo(2));
            Assert.That(fleetStatus.OverdueMaintenanceBuses, Is.EqualTo(1)); // Bus 002 is overdue
            Assert.That(fleetStatus.CriticalAlerts, Is.Not.Empty);
        }

    [Test]
    public async Task MonitorBusLocationAsync_WithValidBus_ShouldReturnMonitoringData()
        {
            // Act
            var monitoringData = await _fleetService.MonitorBusLocationAsync(1);

            // Assert
            Assert.That(monitoringData, Is.Not.Null);
            Assert.That(monitoringData!.BusId, Is.EqualTo(1));
            Assert.That(monitoringData.BusNumber, Is.EqualTo("001"));
            Assert.That(monitoringData.Status, Is.EqualTo("Active"));
            Assert.That(monitoringData.CurrentLatitude, Is.EqualTo(40.7128m));
            Assert.That(monitoringData.CurrentLongitude, Is.EqualTo(-74.0060m));
            Assert.That(monitoringData.IsGpsActive, Is.True);
            Assert.That(monitoringData.HasMaintenanceAlerts, Is.False); // Bus 001 maintenance is not overdue
        }

    [Test]
    public async Task MonitorBusLocationAsync_WithInvalidBus_ShouldReturnNull()
        {
            // Act
            var monitoringData = await _fleetService.MonitorBusLocationAsync(999);

            // Assert
            Assert.That(monitoringData, Is.Null);
        }

    [Test]
    public async Task GetOverdueMaintenanceAlertsAsync_ShouldReturnOverdueBuses()
        {
            // Act
            var overdueBuses = await _fleetService.GetOverdueMaintenanceAlertsAsync();

            // Assert
            Assert.That(overdueBuses.Count, Is.EqualTo(1));
            Assert.That(overdueBuses[0].BusNumber, Is.EqualTo("002"));
            Assert.That(overdueBuses[0].NextMaintenanceDue < DateTime.Today, Is.True);
        }

    [Test]
    public async Task GetActiveGpsTrackedBusesAsync_ShouldReturnOnlyActiveGpsBuses()
        {
            // Act
            var gpsBuses = await _fleetService.GetActiveGpsTrackedBusesAsync();

            // Assert
            Assert.That(gpsBuses.Count, Is.EqualTo(1));
            Assert.That(gpsBuses[0].BusNumber, Is.EqualTo("001"));
            Assert.That(gpsBuses[0].Status, Is.EqualTo("Active"));
            Assert.That(gpsBuses[0].GPSTracking, Is.True);
        }

    [Test]
    public async Task UpdateBusLocationAsync_WithValidBus_ShouldUpdateLocation()
        {
            // Arrange
            var newLat = 41.8781m;
            var newLon = -87.6298m;

            // Act
            var result = await _fleetService.UpdateBusLocationAsync(1, newLat, newLon);

            // Assert
            Assert.That(result, Is.True);

            // Verify location was updated
            var updatedBus = await _context.Buses.FindAsync(1);
            Assert.That(updatedBus!.CurrentLatitude, Is.EqualTo(newLat));
            Assert.That(updatedBus.CurrentLongitude, Is.EqualTo(newLon));
        }

    [Test]
    public async Task UpdateBusLocationAsync_WithNonGpsBus_ShouldReturnFalse()
        {
            // Act
            var result = await _fleetService.UpdateBusLocationAsync(2, 40.0m, -74.0m); // Bus 002 has GPS disabled

            // Assert
            Assert.That(result, Is.False);
        }

    [Test]
    public async Task GetBusesByOperationalStatusAsync_ShouldFilterByStatus()
        {
            // Act
            var activeBuses = await _fleetService.GetBusesByOperationalStatusAsync("Active");

            // Assert
            Assert.That(activeBuses.Count, Is.EqualTo(1));
            Assert.That(activeBuses[0].BusNumber, Is.EqualTo("001"));
        }

    [Test]
    public async Task GetCriticalAlertsAsync_ShouldReturnMaintenanceAndGpsAlerts()
        {
            // Act
            var alerts = await _fleetService.GetCriticalAlertsAsync();

            // Assert
            Assert.That(alerts.Count >= 2, Is.True); // At least maintenance and GPS alerts

            var maintenanceAlert = alerts.FirstOrDefault(a => a.AlertType == "Maintenance");
            Assert.That(maintenanceAlert, Is.Not.Null);
            Assert.That(maintenanceAlert!.BusNumber, Is.EqualTo("002"));

            var gpsAlert = alerts.FirstOrDefault(a => a.AlertType == "GPS");
            Assert.That(gpsAlert, Is.Not.Null);
            Assert.That(gpsAlert!.BusNumber, Is.EqualTo("003")); // Bus 003 has GPS enabled but offline
        }

    [Test]
    public async Task CalculateFleetUtilizationAsync_ShouldReturnMetrics()
        {
            // Act
            var metrics = await _fleetService.CalculateFleetUtilizationAsync();

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics!.UtilizationPercentage, Is.EqualTo(33.33m)); // 1 active out of 3 total
            Assert.That(metrics.BusesInService, Is.EqualTo(1));
            Assert.That(metrics.BusesAvailable, Is.EqualTo(1)); // Only active buses are available
            Assert.That(metrics.CalculatedAt <= DateTime.Now, Is.True);
        }

        /// <summary>
        /// Test implementation of IBusBuddyDbContextFactory for unit tests
        /// </summary>
        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly BusBuddyDbContext _context;

            public TestDbContextFactory(BusBuddyDbContext context)
            {
                _context = context;
            }

            public BusBuddyDbContext CreateDbContext()
            {
                return _context;
            }
            public BusBuddyDbContext CreateWriteDbContext()
            {
                return _context;
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
