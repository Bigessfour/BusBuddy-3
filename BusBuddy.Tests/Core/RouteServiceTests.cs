using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Moq;
using BusBuddy.Core.Services;
using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Simple test implementation of IBusBuddyDbContextFactory for unit tests
    /// </summary>
    public class TestDbContextFactory : IBusBuddyDbContextFactory
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

        public void Dispose()
        {
            _context?.Dispose();
        }
    }

    /// <summary>
    /// Optimized NUnit tests for RouteService with fast execution and minimal setup
    /// Uses AutoFixture patterns and Theory/TestCase for data-driven testing
    /// </summary>
    [TestFixture]
    public class RouteServiceTests : IDisposable // CA1001: Implements IDisposable for _dbContext
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private RouteService _routeService = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Setup in-memory database for fast tests
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;
        }

        [SetUp]
        public void SetUp()
        {
            // Fast setup with minimal mocking
            _dbContext = new BusBuddyDbContext(_dbOptions);

            // Create a simple context factory for testing
            var contextFactory = new TestDbContextFactory(_dbContext);
            _routeService = new RouteService(contextFactory);

            // Seed test data quickly
            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            GC.SuppressFinalize(this); // Fix CA1816: Prevent finalizer calls
        }

        #region Test Data Factory (AutoFixture-like pattern)

        private void SeedTestData()
        {
            var routes = CreateTestRoutes();
            _dbContext.Routes.AddRange(routes);
            _dbContext.SaveChanges();
        }

        private List<Route> CreateTestRoutes()
        {
            return new List<Route>
            {
                new Route { RouteId = 1, RouteName = "Route A", Date = DateTime.Today, IsActive = true, Description = "Morning Route", School = "Test School" },
                new Route { RouteId = 2, RouteName = "Route B", Date = DateTime.Today, IsActive = true, Description = "Afternoon Route", School = "Test School" },
                new Route { RouteId = 3, RouteName = "Route C", Date = DateTime.Today, IsActive = false, Description = "Inactive Route", School = "Test School" }
            };
        }

        #endregion

        #region Basic CRUD Tests (Data-Driven with TestCase)

        [Test]
        public async Task GetAllActiveRoutesAsync_ReturnsOnlyActiveRoutes()
        {
            // Act
            var result = await _routeService.GetAllActiveRoutesAsync();

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Only active routes
            Assert.That(result.Value.All(r => r.IsActive), Is.True);
        }

        [TestCase(1, "Route A")]
        [TestCase(2, "Route B")]
        public async Task GetRouteByIdAsync_WithValidId_ReturnsCorrectRoute(int routeId, string expectedName)
        {
            // Act
            var result = await _routeService.GetRouteByIdAsync(routeId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value.RouteName, Is.EqualTo(expectedName));
        }

        [Test]
        public async Task GetRouteByIdAsync_WithInvalidId_ReturnsFailureResult()
        {
            // Act
            var result = await _routeService.GetRouteByIdAsync(999);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
        }

        [Test]
        public async Task CreateRouteAsync_WithValidRoute_CreatesSuccessfully()
        {
            // Arrange
            var newRoute = new Route
            {
                RouteName = "New Route",
                Date = DateTime.Today.AddDays(1),
                Description = "Test Route",
                School = "Test School"
            };

            // Act
            var result = await _routeService.CreateRouteAsync(newRoute);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value.RouteId, Is.GreaterThan(0));

            // Verify in database
            var dbRoute = await _dbContext.Routes.FindAsync(result.Value.RouteId);
            Assert.That(dbRoute, Is.Not.Null);
            Assert.That(dbRoute.RouteName, Is.EqualTo("New Route"));
        }

        #endregion

        #region Search and Filtering Tests

        [TestCase("Route A", 1)]
        [TestCase("Route", 3)]
        [TestCase("Morning", 1)]
        [TestCase("NonExistent", 0)]
        public async Task SearchRoutesAsync_WithSearchTerm_ReturnsMatchingRoutes(string searchTerm, int expectedCount)
        {
            // Act
            var result = await _routeService.SearchRoutesAsync(searchTerm);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count(), Is.EqualTo(expectedCount));
        }

        #endregion

        #region Route Assignment Tests (High-Value Scenarios)

        [Test]
        public async Task ValidateRouteCapacityAsync_WithValidRoute_ReturnsSuccess()
        {
            // Act
            var result = await _routeService.ValidateRouteCapacityAsync(1);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RouteService(null!));
        }

        #endregion

        #region Performance Tests (Quick Validation)

        [Test]
        [CancelAfter(1000)] // Test must complete within 1 second
        public async Task GetAllRoutesAsync_PerformanceTest_CompletesQuickly()
        {
            // Act
            var result = await _routeService.GetAllActiveRoutesAsync();

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            // Test passes if it completes within timeout
        }

        #endregion
    }
}
