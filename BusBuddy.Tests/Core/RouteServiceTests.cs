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
        private readonly DbContextOptions<BusBuddyDbContext> _options;

        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options)
        {
            _options = options;
        }

        public BusBuddyDbContext CreateDbContext() => new BusBuddyDbContext(_options);

        public BusBuddyDbContext CreateWriteDbContext() => new BusBuddyDbContext(_options);
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

            var contextFactory = new TestDbContextFactory(_dbOptions);
            _routeService = new RouteService(contextFactory);

            // Ensure clean database and seed test data
            _dbContext.Database.EnsureDeleted();
            _dbContext.Database.EnsureCreated();
            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                try
                {
                    if (_dbContext != null)
                    {
                        _dbContext.Database.EnsureDeleted();
                    }
                }
                catch
                {
                    // ignore teardown exceptions
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
                new Route { RouteName = "Route A", Date = DateTime.Today, IsActive = true, Description = "Morning Route", School = "Test School" },
                new Route { RouteName = "Route B", Date = DateTime.Today, IsActive = true, Description = "Afternoon Route", School = "Test School" },
                new Route { RouteName = "Route C", Date = DateTime.Today, IsActive = false, Description = "Inactive Route", School = "Test School" }
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

        [Test]
        public async Task AssignStudentToRouteAsync_AM_SetsAMRouteOnly()
        {
            var student = new Student
            {
                StudentName = "Alice",
                Grade = "3",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0001",
                Active = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            var updated = await _dbContext.Students.FindAsync(student.StudentId);
            Assert.That(updated!.AMRoute, Is.EqualTo("Route A"));
            Assert.That(updated.PMRoute, Is.Null.Or.Empty);
        }

        [Test]
        public async Task AssignStudentToRouteAsync_PM_SetsPMRouteOnly()
        {
            var student = new Student
            {
                StudentName = "Bob",
                Grade = "4",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0002",
                Active = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route B");

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.PM);

            Assert.That(result.IsSuccess, Is.True);
            var updated = await _dbContext.Students.FindAsync(student.StudentId);
            Assert.That(updated!.PMRoute, Is.EqualTo("Route B"));
            Assert.That(updated.AMRoute, Is.Null.Or.Empty);
        }

        [Test]
        public async Task AssignStudentToRouteAsync_RejectsWhenSlotAlreadyAssigned()
        {
            var student = new Student
            {
                StudentName = "Carol",
                Grade = "5",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0003",
                Active = true,
                AMRoute = "Route B"
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public async Task RemoveStudentFromRouteAsync_AM_ClearsSlotOnly()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            var student = new Student
            {
                StudentName = "Dan",
                Grade = "2",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0004",
                Active = true,
                AMRoute = "Route A",
                PMRoute = "Route B"
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.RemoveStudentFromRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            var updated = await _dbContext.Students.FindAsync(student.StudentId);
            Assert.That(updated!.AMRoute, Is.Null.Or.Empty);
            Assert.That(updated.PMRoute, Is.EqualTo("Route B"));
        }

        [Test]
        public async Task GetUnassignedStudentsAsync_AM_IncludesStudentWithPMOnly()
        {
            _dbContext.Students.Add(new Student
            {
                StudentName = "Eve",
                Grade = "1",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0005",
                Active = true,
                PMRoute = "Route B"
            });
            _dbContext.Students.Add(new Student
            {
                StudentName = "Frank",
                Grade = "1",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0006",
                Active = true,
                AMRoute = "Route A"
            });
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.GetUnassignedStudentsAsync(RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Select(s => s.StudentName), Does.Contain("Eve"));
            Assert.That(result.Value.Select(s => s.StudentName), Does.Not.Contain("Frank"));
        }

        [Test]
        public async Task GetStudentsForRouteAsync_ReturnsSlotSubset()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.AddRange(
                new Student { StudentName = "G1", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-1", AMRoute = "Route A" },
                new Student { StudentName = "G2", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-2", PMRoute = "Route A" });
            await _dbContext.SaveChangesAsync();

            var amResult = await _routeService.GetStudentsForRouteAsync(route.RouteId, RouteTimeSlot.AM);
            var pmResult = await _routeService.GetStudentsForRouteAsync(route.RouteId, RouteTimeSlot.PM);

            Assert.That(amResult.Value!.Select(s => s.StudentName), Is.EquivalentTo(new[] { "G1" }));
            Assert.That(pmResult.Value!.Select(s => s.StudentName), Is.EquivalentTo(new[] { "G2" }));
        }

        [Test]
        public async Task AutoAssignStudentsAsync_StopsAtCapacity()
        {
            var bus = new Bus { BusNumber = "B1", Year = 2020, Make = "Test", Model = "M", SeatingCapacity = 2, VINNumber = "VIN1", LicenseNumber = "L1" };
            _dbContext.Buses.Add(bus);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            route.AMVehicleId = bus.BusId;
            await _dbContext.SaveChangesAsync();

            for (int i = 0; i < 5; i++)
            {
                _dbContext.Students.Add(new Student
                {
                    StudentName = $"Student{i}",
                    Grade = "1",
                    School = "T",
                    ParentGuardian = "P",
                    EmergencyPhone = $"555-{i}",
                    Active = true
                });
            }
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.AutoAssignStudentsAsync(route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count, Is.EqualTo(2));
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

        #region Additional Scenarios

        [Test]
        public async Task CreateNewRouteAsync_Valid_CreatesInactiveRoute()
        {
            var result = await _routeService.CreateNewRouteAsync("Route Z", DateTime.Today.AddDays(1), "desc");
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.IsActive, Is.False);
            Assert.That(result.Value.School, Is.Not.Null);
        }

        [Test]
        public async Task CreateNewRouteAsync_DuplicateNameSameDate_Fails()
        {
            // Seed an existing route for tomorrow with same name
            _dbContext.Routes.Add(new Route { RouteName = "DupRoute", Date = DateTime.Today.AddDays(1), IsActive = true, School = "T" });
            _dbContext.SaveChanges();

            var dup = await _routeService.CreateNewRouteAsync("DupRoute", DateTime.Today.AddDays(1));
            Assert.That(dup.IsSuccess, Is.False);
        }

        [Test]
        public async Task ActivateAndDeactivateRoute_TogglesFlags()
        {
            var r = new Route { RouteName = "Toggle", Date = DateTime.Today.AddDays(1), IsActive = false, School = "T" };
            _dbContext.Routes.Add(r);
            await _dbContext.SaveChangesAsync();

            var valid = await _routeService.ValidateRouteForActivationAsync(r.RouteId);
            Assert.That(valid.IsSuccess, Is.True);

            var activated = await _routeService.ActivateRouteAsync(r.RouteId);
            Assert.That(activated.IsSuccess, Is.True);
            _dbContext.ChangeTracker.Clear();
            Assert.That((await _dbContext.Routes.FindAsync(r.RouteId))!.IsActive, Is.True);

            var deactivated = await _routeService.DeactivateRouteAsync(r.RouteId);
            Assert.That(deactivated.IsSuccess, Is.True);
            _dbContext.ChangeTracker.Clear();
            Assert.That((await _dbContext.Routes.FindAsync(r.RouteId))!.IsActive, Is.False);
        }

        [Test]
        public async Task IsRouteNumberUniqueAsync_RespectsExcludeId()
        {
            var r = new Route { RouteName = "UniqueX", Date = DateTime.Today, IsActive = true, School = "T" };
            _dbContext.Routes.Add(r);
            await _dbContext.SaveChangesAsync();

            var notUnique = await _routeService.IsRouteNumberUniqueAsync("UniqueX");
            Assert.That(notUnique.IsSuccess, Is.True);
            Assert.That(notUnique.Value, Is.False);

            var uniqueWhenExcluding = await _routeService.IsRouteNumberUniqueAsync("UniqueX", r.RouteId);
            Assert.That(uniqueWhenExcluding.IsSuccess, Is.True);
            Assert.That(uniqueWhenExcluding.Value, Is.True);
        }

        #endregion
    }
}
