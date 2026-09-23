using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class StudentServiceTests : IDisposable
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private StudentService _studentService = null!;

        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly DbContextOptions<BusBuddyDbContext> _options;
            public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
            public BusBuddyDbContext CreateDbContext() => new BusBuddyDbContext(_options);
            public BusBuddyDbContext CreateWriteDbContext() => new BusBuddyDbContext(_options);
        }

        [SetUp]
        public void SetUp()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"StudentsDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);

            // Seed minimal data
            _dbContext.Routes.AddRange(new[]
            {
                new Route { RouteId = 1, RouteName = "East Route", Date = DateTime.Today, IsActive = true, School = "Test", Session = RouteSession.AM },
                new Route { RouteId = 2, RouteName = "West Route", Date = DateTime.Today, IsActive = true, School = "Test", Session = RouteSession.PM }
            });
            _dbContext.SaveChanges();

            _studentService = new StudentService(new TestDbContextFactory(_dbOptions));
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
            GC.SuppressFinalize(this);
        }

        [Test]
        public async Task AddStudentAsync_ValidStudent_PersistsAndSetsDefaults()
        {
            var s = new Student
            {
                StudentName = "Alice Test",
                Grade = "3",
                School = "Test School",
                ParentGuardian = "Parent A",
                EmergencyPhone = "555-555-5555",
                HomeAddress = "123 East St",
                City = "Town",
                State = "CO",
                Zip = "12345"
            };

            var added = await AddRequiredAsync(s);

            added.StudentId.Should().BeGreaterThan(0);
            added.EnrollmentDate.Should().NotBeNull();

            var fromDb = await _dbContext.Students.FindAsync(added.StudentId);
            fromDb.Should().NotBeNull();
            fromDb!.StudentName.Should().Be("Alice Test");
        }

        [Test]
        public async Task ValidateStudentAsync_InvalidPhoneAndZip_ReturnsErrors()
        {
            var s = new Student
            {
                StudentName = "Bob",
                Grade = "3",
                School = "Test",
                ParentGuardian = "P",
                EmergencyPhone = "bad",
                HomePhone = "also-bad",
                CellPhone = "nope",
                HomeAddress = "1 A St",
                City = "City",
                State = "CO",
                Zip = "9999"
            };

            var errors = await _studentService.ValidateStudentAsync(s);
            errors.Should().Contain(e => e.Contains("home phone", StringComparison.OrdinalIgnoreCase));
            errors.Should().Contain(e => e.Contains("cell phone", StringComparison.OrdinalIgnoreCase));
            errors.Should().Contain(e => e.Contains("emergency phone", StringComparison.OrdinalIgnoreCase));
            errors.Should().Contain(e => e.Contains("ZIP", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public async Task ValidateStudentAsync_CommonPhoneFormats_ShouldPass()
        {
            var samples = new[]
            {
                "5555555555",
                "555-555-5555",
                "(555) 555-5555",
                "+1 (555) 555-5555",
                "555.555.5555",
            };

            foreach (var phone in samples)
            {
                var s = new Student
                {
                    StudentName = "Valid Phones",
                    Grade = "3",
                    School = "Test",
                    ParentGuardian = "P",
                    EmergencyPhone = phone,
                    HomePhone = phone,
                    HomeAddress = "1 A St",
                    City = "City",
                    State = "CO",
                    Zip = "12345"
                };

                var errors = await _studentService.ValidateStudentAsync(s);
                errors.Should().NotContain(e => e.Contains("phone", StringComparison.OrdinalIgnoreCase), $"'{phone}' should be accepted");
            }
        }

        [Test]
        public async Task GetStudentsByRouteAsync_ReturnsStudentsOnAMorPM()
        {
            _dbContext.Students.AddRange(new[]
            {
                new Student { StudentName = "S1", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-555-5555", AMRoute = "East Route" },
                new Student { StudentName = "S2", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-555-5555", PMRoute = "East Route" },
                new Student { StudentName = "S3", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-555-5555", AMRoute = "West Route" }
            });
            await _dbContext.SaveChangesAsync();

            var east = await _studentService.GetStudentsByRouteAsync("East Route");
            east.Should().HaveCount(2);
            east.Select(s => s.StudentName).Should().BeEquivalentTo(new[] { "S1", "S2" });
        }

        [Test]
        public async Task GetStudentsByRouteAsync_KeyedStudentWithStaleName_StaysOnKeyedRoute()
        {
            var west = await _dbContext.Routes.FirstAsync(r => r.RouteName == "West Route");
            _dbContext.Students.Add(new Student
            {
                StudentName = "S4",
                Grade = "1",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                AMRoute = "East Route",
                AmRouteId = west.RouteId
            });
            await _dbContext.SaveChangesAsync();

            var east = await _studentService.GetStudentsByRouteAsync("East Route");
            east.Select(s => s.StudentName).Should().NotContain("S4");
            var westRiders = await _studentService.GetStudentsByRouteAsync("West Route");
            westRiders.Select(s => s.StudentName).Should().Contain("S4");
        }

        [Test]
        public async Task GetStudentsByRouteAsync_SharedName_DoesNotMergeDatedRuns()
        {
            _dbContext.Routes.AddRange(
                new Route
                {
                    RouteId = 10,
                    RouteName = "North Elementary",
                    Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                    IsActive = true,
                    School = "T"
                },
                new Route
                {
                    RouteId = 11,
                    RouteName = "North Elementary",
                    Date = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc),
                    IsActive = true,
                    School = "T"
                });
            _dbContext.Students.Add(new Student
            {
                StudentName = "Keyed North",
                Grade = "1",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                AMRoute = "North Elementary",
                AmRouteId = 10
            });
            await _dbContext.SaveChangesAsync();

            var byName = await _studentService.GetStudentsByRouteAsync("North Elementary");
            byName.Should().BeEmpty();

            var byKey = await _studentService.GetStudentsByRouteAsync(10);
            byKey.Select(s => s.StudentName).Should().Contain("Keyed North");
        }

        [Test]
        public async Task AddStudentAsync_NormalizesPhoneAndResolvesUniqueRouteName()
        {
            var s = new Student
            {
                StudentName = "Carol",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "+1 (555) 555-5555",
                HomePhone = "5555555555",
                AMRoute = "East Route",
                PMRoute = "West Route",
                RidesAm = true,
                RidesPm = true
            };

            var added = await AddRequiredAsync(s);
            added.HomePhone.Should().Be("(555) 555-5555");
            added.EmergencyPhone.Should().Be("(555) 555-5555");
            added.AmRouteId.Should().Be(1);
            added.PmRouteId.Should().Be(2);
            added.AMRoute.Should().Be("East Route");
            added.PMRoute.Should().Be("West Route");
        }

        [Test]
        public async Task UpdateStudentAsync_KeyedSlotRewritesStaleName_ClearingKeyUnassigns()
        {
            var s = new Student
            {
                StudentName = "Dana",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                AMRoute = "East Route",
                PMRoute = "West Route"
            };
            var added = await AddRequiredAsync(s);
            added.AmRouteId.Should().Be(1);
            added.PmRouteId.Should().Be(2);

            added.AMRoute = "not the real name";
            added.PmRouteId = null;
            added.PMRoute = null;
            var rewritten = await _studentService.UpdateStudentAsync(added);
            rewritten.IsSuccess.Should().BeTrue(rewritten.Error);
            rewritten.Value.Should().BeTrue();

            await using var mirrored = new BusBuddyDbContext(_dbOptions);
            var stillKeyed = await mirrored.Students.AsNoTracking()
                .FirstAsync(row => row.StudentId == added.StudentId);
            stillKeyed.AmRouteId.Should().Be(1);
            stillKeyed.AMRoute.Should().Be("East Route");
            stillKeyed.PmRouteId.Should().BeNull();
            stillKeyed.PMRoute.Should().BeNull();

            stillKeyed.AmRouteId = null;
            stillKeyed.AMRoute = null;
            var cleared = await _studentService.UpdateStudentAsync(stillKeyed);
            cleared.IsSuccess.Should().BeTrue(cleared.Error);

            await using var nextSession = new BusBuddyDbContext(_dbOptions);
            var reloaded = await nextSession.Students.AsNoTracking()
                .FirstAsync(row => row.StudentId == added.StudentId);
            StudentRouteAssignment.IsAssignedAny(reloaded).Should().BeFalse();
            reloaded.AmRouteId.Should().BeNull();
            reloaded.AMRoute.Should().BeNull();
        }

        [Test]
        public async Task UpdateStudentAsync_SharedNameDoesNotReplaceExistingKey()
        {
            _dbContext.Routes.AddRange(
                new Route
                {
                    RouteId = 10,
                    RouteName = "North Elementary",
                    Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                    IsActive = true,
                    School = "T"
                },
                new Route
                {
                    RouteId = 11,
                    RouteName = "North Elementary",
                    Date = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc),
                    IsActive = true,
                    School = "T"
                });
            await _dbContext.SaveChangesAsync();

            var added = await AddRequiredAsync(new Student
            {
                StudentName = "Keyed",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                RidesAm = true,
                AMRoute = "East Route"
            });
            added.AMRoute = "North Elementary";

            var updated = await _studentService.UpdateStudentAsync(added);
            updated.IsSuccess.Should().BeTrue(updated.Error);

            await using var next = new BusBuddyDbContext(_dbOptions);
            var saved = await next.Students.AsNoTracking().FirstAsync(x => x.StudentId == added.StudentId);
            saved.AmRouteId.Should().Be(1);
            saved.AMRoute.Should().Be("East Route");
        }

        [Test]
        public async Task UpdateStudentAddressAsync_InvalidState_ReturnsFailure()
        {
            var s = new Student
            {
                StudentName = "D",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555"
            };
            _dbContext.Students.Add(s);
            _dbContext.SaveChanges();

            var result = await _studentService.UpdateStudentAddressAsync(s.StudentId, "123", "City", "Colorado", "12345");
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("State must be a 2-letter abbreviation");
        }

        [Test]
        public async Task ExportStudentsToCsvAsync_IncludesHeaderAndRows()
        {
            _dbContext.Students.Add(new Student
            {
                StudentName = "X",
                Grade = "1",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555"
            });
            await _dbContext.SaveChangesAsync();

            var csv = await _studentService.ExportStudentsToCsvAsync();
            csv.Should().StartWith("Student ID,Student Number,Student Name");
            csv.Split('\n').Length.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task GetStudentStatisticsAsync_ReturnsExpectedCounts()
        {
            _dbContext.Students.AddRange(new[]
            {
                new Student { StudentName = "A", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-555-5555", Active = true, AMRoute = "East Route" },
                new Student { StudentName = "B", Grade = "1", School = "T", ParentGuardian = "P", EmergencyPhone = "555-555-5555", Active = false },
            });
            await _dbContext.SaveChangesAsync();

            var stats = await _studentService.GetStudentStatisticsAsync();
            stats["TotalStudents"].Should().Be(2);
            stats["ActiveStudents"].Should().Be(1);
            stats["StudentsWithRoutes"].Should().Be(1);
        }

        [Test]
        public async Task GetIntakeWarnings_DoesNotBlockValidate()
        {
            var student = new Student { StudentName = "Incomplete", Grade = "1" };

            var warnings = _studentService.GetIntakeWarnings(student);
            var errors = await _studentService.ValidateStudentAsync(student);

            warnings.Should().Contain(w => w.Contains("School", StringComparison.Ordinal));
            warnings.Should().Contain(w => w.Contains("eligibility", StringComparison.OrdinalIgnoreCase));
            warnings.Should().Contain(w => w.Contains("validated coordinates", StringComparison.Ordinal));
            errors.Should().NotContain(w => w.Contains("School is not assigned", StringComparison.Ordinal));
        }

        [Test]
        public async Task UpdateStudentAddressAsync_ClearsPreviousPin()
        {
            var s = new Student
            {
                StudentName = "Moved",
                Grade = "3",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                HomeAddress = "1 Old St",
                City = "Wiley",
                State = "CO",
                Zip = "81090",
                Latitude = 38.08m,
                Longitude = -102.62m,
                PlaceId = "old-place"
            };
            _dbContext.Students.Add(s);
            await _dbContext.SaveChangesAsync();
            _dbContext.RouteStops.Add(new RouteStop
            {
                RouteId = 1,
                StopName = "Home",
                StopAddress = "1 Old St",
                Latitude = 38.08m,
                Longitude = -102.62m,
                Notes = $"StudentId={s.StudentId}"
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var ok = await _studentService.UpdateStudentAddressAsync(s.StudentId, "2 New St", "Lamar", "CO", "81052");

            ok.IsSuccess.Should().BeTrue(ok.Error);
            ok.Value.Should().BeTrue();
            _dbContext.ChangeTracker.Clear();
            var saved = await _dbContext.Students.AsNoTracking().FirstAsync(x => x.StudentId == s.StudentId);
            saved.HomeAddress.Should().Be("2 New St");
            saved.Latitude.Should().BeNull();
            saved.Longitude.Should().BeNull();
            saved.PlaceId.Should().BeNull();
            var stop = await _dbContext.RouteStops.AsNoTracking().FirstAsync(x => x.Notes == $"StudentId={s.StudentId}");
            stop.Latitude.Should().BeNull();
            stop.Longitude.Should().BeNull();
        }

        [Test]
        public async Task UpdateStudentAsync_TurningOffAmEligibility_ClearsAmRoute()
        {
            var s = new Student
            {
                StudentName = "Was AM",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                RidesAm = true,
                RidesPm = true,
                AMRoute = "East Route",
                PMRoute = "West Route"
            };
            var added = await AddRequiredAsync(s);
            added.RidesAm = false;
            var ok = await _studentService.UpdateStudentAsync(added);

            ok.IsSuccess.Should().BeTrue(ok.Error);
            ok.Value.Should().BeTrue();
            await using var next = new BusBuddyDbContext(_dbOptions);
            var saved = await next.Students.AsNoTracking().FirstAsync(x => x.StudentId == added.StudentId);
            saved.AmRouteId.Should().BeNull();
            saved.AMRoute.Should().BeNull();
            saved.PmRouteId.Should().Be(2);
        }

        [Test]
        public async Task UpdateStudentAsync_ValidationFailure_LeavesRouteAssignmentOnTheCaller()
        {
            var s = new Student
            {
                StudentName = "Still Assigned",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                RidesAm = true,
                AMRoute = "East Route"
            };
            var added = await AddRequiredAsync(s);
            added.RidesAm = false;
            added.StudentName = " ";

            var failed = await _studentService.UpdateStudentAsync(added);

            failed.IsSuccess.Should().BeFalse();
            added.AmRouteId.Should().Be(1);
            added.AMRoute.Should().Be("East Route");
            await using var next = new BusBuddyDbContext(_dbOptions);
            var saved = await next.Students.AsNoTracking().FirstAsync(x => x.StudentId == added.StudentId);
            saved.RidesAm.Should().BeTrue();
            saved.AmRouteId.Should().Be(1);
        }

        [Test]
        public async Task AddStudentAsync_SpecialNeeds_DefaultsAideAndClearsCatalogStop()
        {
            var s = new Student
            {
                StudentName = "Aide",
                Grade = "4",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555",
                RequiresSpecialNeedsBus = true,
                RequiresAide = false,
                PickupStopId = 9
            };

            var added = await AddRequiredAsync(s);

            added.RequiresAide.Should().BeTrue();
            added.PickupStopId.Should().BeNull();
        }

        private async Task<Student> AddRequiredAsync(Student student)
        {
            var result = await _studentService.AddStudentAsync(student);
            result.IsSuccess.Should().BeTrue(result.Error);
            return result.Value;
        }
    }
}
