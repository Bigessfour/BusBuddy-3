using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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

        [Test]
        public async Task GetAllRoutesAsync_PopulatesStudentAndStopCounts()
        {
            var routeA = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Keyed Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-77",
                AMRoute = "Route A",
                AmRouteId = routeA.RouteId
            });
            _dbContext.RouteStops.Add(new RouteStop
            {
                RouteId = routeA.RouteId,
                StopName = "Home",
                StopAddress = "1 Main",
                Latitude = 38.10m,
                Longitude = -102.70m,
                StopOrder = 1,
                ScheduledArrival = new TimeSpan(7, 20, 0),
                ScheduledDeparture = new TimeSpan(7, 22, 0),
                CreatedDate = DateTime.UtcNow,
                EstimatedArrivalTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(7), DateTimeKind.Utc),
                EstimatedDepartureTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(7).AddMinutes(2), DateTimeKind.Utc)
            });
            _dbContext.RouteStops.Add(new RouteStop
            {
                RouteId = routeA.RouteId,
                StopName = "School",
                StopAddress = "Wiley School",
                Latitude = 38.08m,
                Longitude = -102.62m,
                StopOrder = 2,
                ScheduledArrival = new TimeSpan(8, 0, 0),
                ScheduledDeparture = new TimeSpan(8, 5, 0),
                CreatedDate = DateTime.UtcNow,
                EstimatedArrivalTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(8), DateTimeKind.Utc),
                EstimatedDepartureTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(8).AddMinutes(5), DateTimeKind.Utc)
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await _routeService.GetAllRoutesAsync();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            var loadedA = result.Value!.Single(r => r.RouteName == "Route A");
            var loadedB = result.Value!.Single(r => r.RouteName == "Route B");
            Assert.That(loadedA.StudentCount, Is.EqualTo(1));
            Assert.That(loadedA.StopCount, Is.EqualTo(2));
            Assert.That(loadedB.StudentCount, Is.EqualTo(0));
            Assert.That(loadedB.StopCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetAllRoutesAsync_SharedNameWithoutKey_DoesNotInflateBothRows()
        {
            var first = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Routes.Add(new Route
            {
                RouteName = "Route A",
                Date = DateTime.Today.AddDays(1),
                IsActive = true,
                School = "Test School"
            });
            _dbContext.Students.Add(new Student
            {
                StudentName = "Orphan Name Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-77",
                AMRoute = "Route A"
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await _routeService.GetAllRoutesAsync();
            Assert.That(result.IsSuccess, Is.True, result.Error);
            var named = result.Value!.Where(r => r.RouteName == "Route A").ToList();
            Assert.That(named, Has.Count.EqualTo(2));
            Assert.That(named.Sum(r => r.StudentCount ?? 0), Is.EqualTo(0),
                "name-only riders cannot be attributed when the name is shared across dates");
            Assert.That(named.Any(r => r.RouteId == first.RouteId), Is.True);
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

        private async Task<Student> ReloadStudentAsync(int studentId)
        {
            _dbContext.ChangeTracker.Clear();
            return await _dbContext.Students.AsNoTracking().FirstAsync(s => s.StudentId == studentId);
        }

        #endregion

        #region Route Assignment Tests (High-Value Scenarios)

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
                Active = true,
                RidesAm = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            var updated = await ReloadStudentAsync(student.StudentId);
            Assert.That(updated.AMRoute, Is.EqualTo("Route A"));
            Assert.That(updated.AmRouteId, Is.EqualTo(route.RouteId));
            Assert.That(updated.PMRoute, Is.Null.Or.Empty);
        }

        [Test]
        public async Task AssignStudentToRouteAsync_RefusesWhenAmEligibilityIsOff()
        {
            var student = new Student
            {
                StudentName = "No AM",
                Grade = "3",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0009",
                Active = true,
                RidesAm = false
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("not eligible"));
            var updated = await ReloadStudentAsync(student.StudentId);
            Assert.That(updated.AmRouteId, Is.Null);
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
                Active = true,
                RidesPm = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.AsTracking().FirstAsync(r => r.RouteName == "Route B");
            route.Session = RouteSession.PM;
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.PM);

            Assert.That(result.IsSuccess, Is.True);
            var updated = await ReloadStudentAsync(student.StudentId);
            Assert.That(updated.PMRoute, Is.EqualTo("Route B"));
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
            var updated = await ReloadStudentAsync(student.StudentId);
            Assert.That(updated.AMRoute, Is.Null.Or.Empty);
            Assert.That(updated.AmRouteId, Is.Null);
            Assert.That(updated.PMRoute, Is.EqualTo("Route B"));
        }

        [Test]
        public async Task AssignAndRemove_PersistOnStudentRow_AcrossNewDbContext()
        {
            var student = new Student
            {
                StudentName = "Session Persist",
                Grade = "2",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0010",
                Active = true,
                RidesAm = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");

            var assigned = await _routeService.AssignStudentToRouteAsync(
                student.StudentId, route.RouteId, RouteTimeSlot.AM);
            Assert.That(assigned.IsSuccess, Is.True);

            await using (var nextSession = new BusBuddyDbContext(_dbOptions))
            {
                var loaded = await nextSession.Students.AsNoTracking()
                    .FirstAsync(s => s.StudentId == student.StudentId);
                Assert.That(StudentRouteAssignment.IsAssignedAny(loaded), Is.True);
                Assert.That(loaded.AmRouteId, Is.EqualTo(route.RouteId));
                Assert.That(loaded.AMRoute, Is.EqualTo("Route A"));
            }

            var removed = await _routeService.RemoveStudentFromRouteAsync(
                student.StudentId, route.RouteId, RouteTimeSlot.AM);
            Assert.That(removed.IsSuccess, Is.True);

            await using (var nextSession = new BusBuddyDbContext(_dbOptions))
            {
                var loaded = await nextSession.Students.AsNoTracking()
                    .FirstAsync(s => s.StudentId == student.StudentId);
                Assert.That(StudentRouteAssignment.IsAssignedAny(loaded), Is.False);
                Assert.That(StudentRouteAssignment.IsUnassignedAm(loaded), Is.True);
                Assert.That(loaded.AmRouteId, Is.Null);
                Assert.That(loaded.AMRoute, Is.Null.Or.Empty);
            }
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
            var bus = new Bus { BusNumber = "B1", Year = 2020, Make = "Test", Model = "M", SeatingCapacity = 2, VINNumber = "VIN1", LicenseNumber = "L1", Status = "Active" };
            _dbContext.Buses.Add(bus);
            await _dbContext.SaveChangesAsync();

            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            route.AMVehicleId = bus.BusId;
            var updateRoute = await _routeService.UpdateRouteAsync(route);
            Assert.That(updateRoute.IsSuccess, Is.True, updateRoute.Error);

            var routeCheck = await _routeService.GetRouteByIdAsync(route.RouteId);
            Assert.That(routeCheck.IsSuccess, Is.True);
            Assert.That(routeCheck.Value!.AMVehicleId, Is.EqualTo(bus.BusId));

            for (int i = 0; i < 5; i++)
            {
                _dbContext.Students.Add(new Student
                {
                    StudentName = $"Student{i}",
                    Grade = "1",
                    School = "Test School",
                    ParentGuardian = "P",
                    EmergencyPhone = $"555-{i}",
                    Active = true,
                    RidesAm = true
                });
            }
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await _routeService.AutoAssignStudentsAsync(route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count, Is.EqualTo(2));

            _dbContext.ChangeTracker.Clear();
            var assignedCount = await _dbContext.Students.CountAsync(s => s.AMRoute == "Route A");
            Assert.That(assignedCount, Is.EqualTo(2));
        }

        [Test]
        public async Task AutoAssignStudentsAsync_SkipsIneligibleAndContinues()
        {
            // Ordered by name, so a special-needs child first used to abort the whole pass.
            var evaluator = new BusBuddy.Core.Services.RouteDetermination.AssignFitnessEvaluator(
                new TestDbContextFactory(_dbOptions));
            var service = new RouteService(new TestDbContextFactory(_dbOptions), waypointRebuild: null, evaluator);

            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Aaa Special",
                Grade = "1",
                School = "Test School",
                ParentGuardian = "P",
                EmergencyPhone = "555-1",
                Active = true,
                RequiresSpecialNeedsBus = true
            });
            _dbContext.Students.Add(new Student
            {
                StudentName = "Zed Regular",
                Grade = "1",
                School = "Test School",
                ParentGuardian = "P",
                EmergencyPhone = "555-2",
                Active = true,
                RidesAm = true
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await service.AutoAssignStudentsAsync(route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value!.Select(s => s.StudentName), Is.EquivalentTo(new[] { "Zed Regular" }));
            _dbContext.ChangeTracker.Clear();
            var special = await _dbContext.Students.FirstAsync(s => s.StudentName == "Aaa Special");
            var regular = await _dbContext.Students.FirstAsync(s => s.StudentName == "Zed Regular");
            Assert.That(special.AMRoute, Is.Null);
            Assert.That(regular.AMRoute, Is.EqualTo("Route A"));
            Assert.That(regular.AmRouteId, Is.EqualTo(route.RouteId));
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>((Action)(() => new RouteService(null!)));
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
            Assert.That(result.Value.School, Is.Null);
            Assert.That(result.Value.Session, Is.EqualTo(RouteSession.AM));
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
            var r = new Route { RouteName = "Toggle", Date = DateTime.Today.AddDays(1), IsActive = false, School = "T", Session = RouteSession.AM };
            _dbContext.Routes.Add(r);
            await _dbContext.SaveChangesAsync();
            await SeedRunnableRouteAsync(r);

            var valid = await _routeService.ValidateRouteForActivationAsync(r.RouteId);
            Assert.That(valid.IsSuccess, Is.True);
            Assert.That(valid.Value!.IsValid, Is.True, string.Join("; ", valid.Value.Issues));

            var activated = await _routeService.ActivateRouteAsync(r.RouteId);
            Assert.That(activated.IsSuccess, Is.True, activated.Error);
            _dbContext.ChangeTracker.Clear();
            Assert.That((await _dbContext.Routes.FindAsync(r.RouteId))!.IsActive, Is.True);

            var deactivated = await _routeService.DeactivateRouteAsync(r.RouteId);
            Assert.That(deactivated.IsSuccess, Is.True);
            _dbContext.ChangeTracker.Clear();
            Assert.That((await _dbContext.Routes.FindAsync(r.RouteId))!.IsActive, Is.False);
        }

        [Test]
        public async Task UpdateRouteAsync_Rename_FollowsRiderAssignments()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Rename Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-77",
                // Name casing deliberately differs: the cascade follows the key, not the string.
                AMRoute = "route a",
                PMRoute = "Route A",
                AmRouteId = route.RouteId,
                PmRouteId = route.RouteId
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            route.RouteName = "Route A Renamed";
            var result = await _routeService.UpdateRouteAsync(route);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            _dbContext.ChangeTracker.Clear();
            var rider = await _dbContext.Students.FirstAsync(s => s.StudentName == "Rename Rider");
            Assert.That(rider.AMRoute, Is.EqualTo("Route A Renamed"));
            Assert.That(rider.PMRoute, Is.EqualTo("Route A Renamed"));
        }

        [Test]
        public async Task UpdateRouteAsync_Rename_FollowsOnlyItsOwnRidersWhenNameIsShared()
        {
            // Two routes share a name on different dates. Before Student.AmRouteId existed the cascade
            // could not tell them apart and had to skip every rider; keyed assignments must now follow
            // the renamed route only and leave the other route's riders alone.
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route B");
            var sameNameOtherDate = new Route
            {
                RouteName = "Route B",
                Date = DateTime.Today.AddDays(1),
                IsActive = true,
                School = "Test School"
            };
            _dbContext.Routes.Add(sameNameOtherDate);
            await _dbContext.SaveChangesAsync();

            _dbContext.Students.Add(new Student
            {
                StudentName = "Renamed Route Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-78",
                AMRoute = "Route B",
                AmRouteId = route.RouteId
            });
            _dbContext.Students.Add(new Student
            {
                StudentName = "Other Route Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-79",
                AMRoute = "Route B",
                AmRouteId = sameNameOtherDate.RouteId
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            route.RouteName = "Route B Renamed";
            var result = await _routeService.UpdateRouteAsync(route);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            _dbContext.ChangeTracker.Clear();

            var followed = await _dbContext.Students.FirstAsync(s => s.StudentName == "Renamed Route Rider");
            var untouched = await _dbContext.Students.FirstAsync(s => s.StudentName == "Other Route Rider");
            Assert.That(followed.AMRoute, Is.EqualTo("Route B Renamed"));
            Assert.That(untouched.AMRoute, Is.EqualTo("Route B"));
        }

        [Test]
        public async Task AssignStudentToRouteAsync_SetsIdentityKeyAlongsideName()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            var student = new Student
            {
                StudentName = "Key Mirror Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-80",
                RidesAm = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var assigned = await _routeService.AssignStudentToRouteAsync(
                student.StudentId, route.RouteId, RouteTimeSlot.AM);

            Assert.That(assigned.IsSuccess, Is.True, assigned.Error);
            _dbContext.ChangeTracker.Clear();
            var saved = await _dbContext.Students.FirstAsync(s => s.StudentId == student.StudentId);
            Assert.That(saved.AmRouteId, Is.EqualTo(route.RouteId));
            Assert.That(saved.AMRoute, Is.EqualTo(route.RouteName));
        }

        [Test]
        public async Task UpdateRouteAsync_ReInfersSessionAfterRename()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route C");

            route.RouteName = "Route C-PM";
            var result = await _routeService.UpdateRouteAsync(route);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value!.Session, Is.EqualTo(RouteSession.PM));
        }

        [Test]
        public async Task AddStopToRouteAsync_WithoutEstimates_PersistsWallClockTimes()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");

            var result = await _routeService.AddStopToRouteAsync(route.RouteId, new RouteStop
            {
                StopName = "Elm & 2nd",
                StopAddress = "200 Elm St",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
                ScheduledArrival = new TimeSpan(7, 30, 0),
                ScheduledDeparture = new TimeSpan(7, 32, 0)
            });

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value!.EstimatedArrivalTime.TimeOfDay, Is.EqualTo(new TimeSpan(7, 30, 0)));
            Assert.That(result.Value.EstimatedDepartureTime.TimeOfDay, Is.EqualTo(new TimeSpan(7, 32, 0)));
        }

        [Test]
        public async Task UpdateRouteStopAsync_UpdatesNameAddressAndCoordinates()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            var add = await _routeService.AddStopToRouteAsync(route.RouteId, new RouteStop
            {
                StopName = "Old Name",
                StopAddress = "100 Old St",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
                ScheduledArrival = new TimeSpan(7, 10, 0),
                ScheduledDeparture = new TimeSpan(7, 12, 0)
            });
            Assert.That(add.IsSuccess, Is.True, add.Error);
            var stopId = add.Value!.RouteStopId;

            var update = await _routeService.UpdateRouteStopAsync(route.RouteId, new RouteStop
            {
                RouteStopId = stopId,
                StopName = "New Name",
                StopAddress = "200 New St",
                Latitude = 38.09m,
                Longitude = -102.63m
            });

            Assert.That(update.IsSuccess, Is.True, update.Error);
            Assert.That(update.Value!.StopName, Is.EqualTo("New Name"));
            Assert.That(update.Value.StopAddress, Is.EqualTo("200 New St"));
            Assert.That(update.Value.Latitude, Is.EqualTo(38.09m));
            Assert.That(update.Value.ScheduledArrival, Is.EqualTo(new TimeSpan(7, 10, 0)));
        }

        [Test]
        public async Task CloneRouteAsync_CopiesStopsAndDepartureEstimate()
        {
            var source = await _dbContext.Routes.AsTracking().FirstAsync(r => r.RouteName == "Route A");
            source.AMVehicleId = 11;
            source.AMDriverId = 12;
            source.PMVehicleId = 13;
            source.PMDriverId = 14;
            await _dbContext.SaveChangesAsync();
            var arrival = new DateTime(2026, 8, 17, 7, 15, 0);
            var departure = new DateTime(2026, 8, 17, 7, 18, 0);
            _dbContext.RouteStops.Add(new RouteStop
            {
                RouteId = source.RouteId,
                StopName = "Main & 3rd",
                StopAddress = "100 Main St",
                StopOrder = 1,
                ScheduledArrival = new TimeSpan(7, 15, 0),
                ScheduledDeparture = new TimeSpan(7, 18, 0),
                CreatedDate = DateTime.UtcNow,
                EstimatedArrivalTime = arrival,
                EstimatedDepartureTime = departure
            });
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.CloneRouteAsync(source.RouteId, DateTime.Today.AddDays(1), "Copy of Route A");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.RouteName, Is.EqualTo("Copy of Route A"));
            Assert.That(result.Value.IsActive, Is.False);
            Assert.That(result.Value.AMVehicleId, Is.EqualTo(11));
            Assert.That(result.Value.AMDriverId, Is.EqualTo(12));
            Assert.That(result.Value.PMVehicleId, Is.EqualTo(13));
            Assert.That(result.Value.PMDriverId, Is.EqualTo(14));

            var clonedStops = await _dbContext.RouteStops
                .Where(s => s.RouteId == result.Value.RouteId)
                .ToListAsync();
            Assert.That(clonedStops, Has.Count.EqualTo(1));
            Assert.That(clonedStops[0].StopName, Is.EqualTo("Main & 3rd"));
            Assert.That(clonedStops[0].EstimatedDepartureTime, Is.EqualTo(departure));
        }

        [Test]
        public async Task DeleteRouteAsync_RemovesEmptyRoute()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route C");

            var result = await _routeService.DeleteRouteAsync(route.RouteId);

            Assert.That(result.IsSuccess, Is.True);
            _dbContext.ChangeTracker.Clear();
            Assert.That(await _dbContext.Routes.AnyAsync(r => r.RouteId == route.RouteId), Is.False);
        }

        [Test]
        public async Task DeleteRouteAsync_WithSchedules_SoftRetiresAndKeepsHistory()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Delete Rider",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-9",
                AMRoute = "Route A",
                PMRoute = "Route A",
                AmRouteId = route.RouteId,
                PmRouteId = route.RouteId
            });
            var bus = new Bus
            {
                BusNumber = "DEL-1",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                SeatingCapacity = 40,
                VINNumber = "VINDEL1",
                LicenseNumber = "LDEL1",
                Status = "Active"
            };
            var driver = new Driver { DriverName = "Delete Driver", DriversLicenceType = "CDL", Status = "Active" };
            _dbContext.Buses.Add(bus);
            _dbContext.Drivers.Add(driver);
            await _dbContext.SaveChangesAsync();

            _dbContext.Schedules.Add(new Schedule
            {
                RouteId = route.RouteId,
                BusId = bus.BusId,
                DriverId = driver.DriverId,
                ScheduleDate = DateTime.Today,
                DepartureTime = DateTime.Today.AddHours(7),
                ArrivalTime = DateTime.Today.AddHours(8)
            });
            _dbContext.RouteStops.Add(new RouteStop
            {
                RouteId = route.RouteId,
                StopName = "Barn",
                StopOrder = 1
            });
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.DeleteRouteAsync(route.RouteId);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Error, Does.Contain("retired").IgnoreCase);
            Assert.That(result.Error, Does.Contain("student"));
            Assert.That(result.Error, Does.Contain("schedule"));
            Assert.That(result.Error, Does.Contain("bus and driver").IgnoreCase);
            _dbContext.ChangeTracker.Clear();
            var kept = await _dbContext.Routes.FirstAsync(r => r.RouteId == route.RouteId);
            Assert.That(kept.IsActive, Is.False);
            Assert.That(await _dbContext.Schedules.AnyAsync(s => s.RouteId == route.RouteId), Is.True);
            Assert.That(await _dbContext.RouteStops.AnyAsync(s => s.RouteId == route.RouteId), Is.True);
            var rider = await _dbContext.Students.FirstAsync(s => s.StudentName == "Delete Rider");
            Assert.That(rider.AmRouteId, Is.EqualTo(route.RouteId));
            Assert.That(rider.PmRouteId, Is.EqualTo(route.RouteId));
        }

        [Test]
        public async Task DeleteRouteAsync_UnassignsRidersStoredWithDifferentCasing()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Casing Rider",
                Grade = "4",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-8",
                AMRoute = "route a",
                PMRoute = "ROUTE A"
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await _routeService.DeleteRouteAsync(route.RouteId);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            _dbContext.ChangeTracker.Clear();
            var rider = await _dbContext.Students.FirstAsync(s => s.StudentName == "Casing Rider");
            Assert.That(rider.AMRoute, Is.Null);
            Assert.That(rider.PMRoute, Is.Null);
        }

        [Test]
        public async Task AssignStudentToRouteAsync_SharedName_DoesNotTreatOtherDateAsSameRoute()
        {
            var first = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            var otherDate = new Route
            {
                RouteName = "Route A",
                Date = DateTime.Today.AddDays(1),
                IsActive = true,
                School = "Test School"
            };
            _dbContext.Routes.Add(otherDate);
            var student = new Student
            {
                StudentName = "Keyed Rider",
                Grade = "3",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0010",
                Active = true,
                AMRoute = first.RouteName,
                AmRouteId = first.RouteId
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, otherDate.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.False);
            var updated = await ReloadStudentAsync(student.StudentId);
            Assert.That(updated.AmRouteId, Is.EqualTo(first.RouteId));
        }

        [Test]
        public async Task GetUnassignedStudentsAsync_AM_TreatsKeyedEmptyNameAsAssigned()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Keyed Empty Name",
                Grade = "1",
                School = "Test",
                ParentGuardian = "Parent",
                EmergencyPhone = "555-0011",
                Active = true,
                AMRoute = null,
                AmRouteId = route.RouteId
            });
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.GetUnassignedStudentsAsync(RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Select(s => s.StudentName), Does.Not.Contain("Keyed Empty Name"));
        }

        [Test]
        public async Task AssignStudentToRouteAsync_RejectsSlotThatDoesNotMatchSession()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            var student = new Student
            {
                StudentName = "Wrong Slot",
                Grade = "3",
                School = "Test School",
                ParentGuardian = "P",
                EmergencyPhone = "555-81",
                Active = true
            };
            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();

            var result = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId, RouteTimeSlot.PM);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("AM"));
            var saved = await ReloadStudentAsync(student.StudentId);
            Assert.That(saved.PMRoute, Is.Null.Or.Empty);
            Assert.That(saved.PmRouteId, Is.Null);
        }

        [Test]
        public async Task AutoAssignStudentsAsync_SkipsStudentsFromAnotherSchool()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            _dbContext.Students.Add(new Student
            {
                StudentName = "Other School",
                Grade = "1",
                School = "Elsewhere",
                ParentGuardian = "P",
                EmergencyPhone = "555-90",
                Active = true
            });
            _dbContext.Students.Add(new Student
            {
                StudentName = "Same School",
                Grade = "1",
                School = "Test School",
                ParentGuardian = "P",
                EmergencyPhone = "555-91",
                Active = true,
                RidesAm = true
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var result = await _routeService.AutoAssignStudentsAsync(route.RouteId, RouteTimeSlot.AM);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value!.Select(s => s.StudentName), Is.EquivalentTo(new[] { "Same School" }));
        }

        [Test]
        public async Task CreateNewRouteAsync_PersistsRequestedSessionAndSchool()
        {
            var result = await _routeService.CreateNewRouteAsync(
                "Route PM",
                DateTime.Today.AddDays(2),
                "afternoon",
                RouteSession.PM,
                "Wiley School");

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value!.Session, Is.EqualTo(RouteSession.PM));
            Assert.That(result.Value.School, Is.EqualTo("Wiley School"));
        }

        [Test]
        public async Task CreateRouteAsync_DuplicateNameSameDate_Fails()
        {
            var dup = new Route
            {
                RouteName = "Route A",
                Date = DateTime.Today,
                School = "Test School"
            };

            var result = await _routeService.CreateRouteAsync(dup);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("already exists"));
        }

        [Test]
        public async Task CloneRouteAsync_DuplicateNameSameDate_Fails()
        {
            var source = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");

            var result = await _routeService.CloneRouteAsync(source.RouteId, DateTime.Today, "Route B");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("already exists"));
        }

        [Test]
        public async Task ActivateRouteAsync_BareRoute_Fails()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route C");

            var valid = await _routeService.ValidateRouteForActivationAsync(route.RouteId);
            Assert.That(valid.IsSuccess, Is.True);
            Assert.That(valid.Value!.IsValid, Is.False);

            var activated = await _routeService.ActivateRouteAsync(route.RouteId);
            Assert.That(activated.IsSuccess, Is.False);
            _dbContext.ChangeTracker.Clear();
            Assert.That((await _dbContext.Routes.FindAsync(route.RouteId))!.IsActive, Is.False);
        }

        [Test]
        public async Task ActivateRouteAsync_PastDateWithStopsAndBus_Succeeds()
        {
            var route = new Route
            {
                RouteName = "Year Run",
                Date = DateTime.Today.AddDays(-30),
                IsActive = false,
                School = "Test School",
                Session = RouteSession.AM
            };
            _dbContext.Routes.Add(route);
            await _dbContext.SaveChangesAsync();
            await SeedRunnableRouteAsync(route);

            var activated = await _routeService.ActivateRouteAsync(route.RouteId);

            Assert.That(activated.IsSuccess, Is.True, activated.Error);
            _dbContext.ChangeTracker.Clear();
            Assert.That((await _dbContext.Routes.FindAsync(route.RouteId))!.IsActive, Is.True);
        }

        [Test]
        public async Task RemoveStopFromRouteAsync_CompactsStopOrder()
        {
            var route = await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteName == "Route A");
            var ids = new List<int>();
            for (var i = 0; i < 3; i++)
            {
                var add = await _routeService.AddStopToRouteAsync(route.RouteId, ValidStop($"Stop {i + 1}"));
                Assert.That(add.IsSuccess, Is.True, add.Error);
                ids.Add(add.Value!.RouteStopId);
            }

            var removed = await _routeService.RemoveStopFromRouteAsync(route.RouteId, ids[1]);
            Assert.That(removed.IsSuccess, Is.True, removed.Error);

            _dbContext.ChangeTracker.Clear();
            var orders = await _dbContext.RouteStops
                .Where(s => s.RouteId == route.RouteId)
                .OrderBy(s => s.StopOrder)
                .Select(s => s.StopOrder)
                .ToListAsync();
            Assert.That(orders, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public async Task GetSessionLoadAsync_SubtractsNotRidingAndWarnsWhenBusMissing()
        {
            var route = await _dbContext.Routes.FirstAsync(r => r.RouteName == "Route A");
            var riding = new Student
            {
                StudentName = "Riding",
                Grade = "1",
                School = "Test School",
                ParentGuardian = "P",
                EmergencyPhone = "555-1",
                Active = true,
                RidesAm = true
            };
            var absent = new Student
            {
                StudentName = "Absent",
                Grade = "1",
                School = "Test School",
                ParentGuardian = "P",
                EmergencyPhone = "555-2",
                Active = true,
                RidesAm = true
            };
            _dbContext.Students.AddRange(riding, absent);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            Assert.That((await _routeService.AssignStudentToRouteAsync(riding.StudentId, route.RouteId, RouteTimeSlot.AM)).IsSuccess, Is.True);
            Assert.That((await _routeService.AssignStudentToRouteAsync(absent.StudentId, route.RouteId, RouteTimeSlot.AM)).IsSuccess, Is.True);
            var recorded = await _routeService.RecordRiderExceptionAsync(route.RouteId, absent.StudentId, DateTime.Today, "Absent");
            Assert.That(recorded.IsSuccess, Is.True, recorded.Error);

            var load = await _routeService.GetSessionLoadAsync(route.RouteId, DateTime.Today);
            Assert.That(load.IsSuccess, Is.True, load.Error);
            Assert.That(load.Value!.AssignedCount, Is.EqualTo(2));
            Assert.That(load.Value.NotRidingCount, Is.EqualTo(1));
            Assert.That(load.Value.LoadCount, Is.EqualTo(1));
            Assert.That(load.Value.Capacity, Is.EqualTo(0));
            Assert.That(load.Value.Warning, Does.Contain("capacity is unknown"));

            var cleared = await _routeService.ClearRiderExceptionAsync(route.RouteId, absent.StudentId, DateTime.Today);
            Assert.That(cleared.IsSuccess, Is.True, cleared.Error);
            var after = await _routeService.GetSessionLoadAsync(route.RouteId, DateTime.Today);
            Assert.That(after.Value!.NotRidingCount, Is.EqualTo(0));
            Assert.That(after.Value.LoadCount, Is.EqualTo(2));
            _dbContext.ChangeTracker.Clear();
            var stillAssigned = await _dbContext.Students.FirstAsync(s => s.StudentId == absent.StudentId);
            Assert.That(stillAssigned.AmRouteId, Is.EqualTo(route.RouteId));
        }

        private async Task SeedRunnableRouteAsync(Route route)
        {
            var bus = new Bus
            {
                BusNumber = "RUN-" + route.RouteId,
                Year = 2020,
                Make = "Test",
                Model = "M",
                SeatingCapacity = 24,
                VINNumber = "VIN-RUN-" + route.RouteId,
                LicenseNumber = "LIC-" + route.RouteId,
                Status = "Active"
            };
            _dbContext.Buses.Add(bus);
            await _dbContext.SaveChangesAsync();

            _dbContext.ChangeTracker.Clear();
            var linked = await _routeService.AssignVehicleToRouteAsync(route.RouteId, bus.BusId, RouteTimeSlot.AM);
            Assert.That(linked.IsSuccess, Is.True, linked.Error);

            var first = await _routeService.AddStopToRouteAsync(route.RouteId, ValidStop("Depot"));
            var second = await _routeService.AddStopToRouteAsync(route.RouteId, ValidStop("School"));
            Assert.That(first.IsSuccess, Is.True, first.Error);
            Assert.That(second.IsSuccess, Is.True, second.Error);
        }

        private static RouteStop ValidStop(string name) => new()
        {
            StopName = name,
            StopAddress = name + " St",
            Latitude = 38.0872m,
            Longitude = -102.6208m,
            ScheduledArrival = new TimeSpan(7, 15, 0),
            ScheduledDeparture = new TimeSpan(7, 18, 0)
        };

        #endregion
    }
}
