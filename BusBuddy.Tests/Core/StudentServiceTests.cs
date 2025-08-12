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
    public class StudentServiceTests : IDisposable
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private StudentService _studentService = null!;

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
                .UseInMemoryDatabase($"StudentsDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);

            // Seed minimal data
            _dbContext.Routes.AddRange(new[]
            {
                new Route { RouteId = 1, RouteName = "East Route", Date = DateTime.Today, IsActive = true, School = "Test" },
                new Route { RouteId = 2, RouteName = "West Route", Date = DateTime.Today, IsActive = true, School = "Test" }
            });
            _dbContext.SaveChanges();

            _studentService = new StudentService(new TestDbContextFactory(_dbContext));
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

            var added = await _studentService.AddStudentAsync(s);

            added.StudentId.Should().BeGreaterThan(0);
            added.EnrollmentDate.Should().NotBeNull();

            var fromDb = await _dbContext.Students.FindAsync(added.StudentId);
            fromDb.Should().NotBeNull();
            fromDb!.StudentName.Should().Be("Alice Test");
        }

        [Test]
        public async Task ValidateStudentAsync_InvalidPhoneAndZip_ReturnsErrors()
        {
            // Force strict validation mode for this test so phone/ZIPS are enforced even if environment defaults changed.
            Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", "strict");
            var s = new Student
            {
                StudentName = "Bob",
                Grade = "3",
                School = "Test",
                ParentGuardian = "P",
                EmergencyPhone = "bad",
                HomePhone = "also-bad",
                HomeAddress = "1 A St",
                City = "City",
                State = "CO",
                Zip = "9999"
            };

            var errors = await _studentService.ValidateStudentAsync(s);
            errors.Should().Contain(e => e.Contains("phone", StringComparison.OrdinalIgnoreCase));
            errors.Should().Contain(e => e.Contains("ZIP", StringComparison.OrdinalIgnoreCase));
            Environment.SetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE", null);
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
            east.Select(s => s.StudentName).Should().BeEquivalentTo(new []{"S1","S2"});
        }

        [Test]
        public async Task AssignStudentToRouteAsync_UpdatesAMandPM()
        {
            var s = new Student
            {
                StudentName = "Carol",
                Grade = "2",
                School = "T",
                ParentGuardian = "P",
                EmergencyPhone = "555-555-5555"
            };
            _dbContext.Students.Add(s);
            await _dbContext.SaveChangesAsync();

            var ok = await _studentService.AssignStudentToRouteAsync(s.StudentId, "East Route", "West Route");
            ok.Should().BeTrue();

            var updated = await _dbContext.Students.FindAsync(s.StudentId);
            updated!.AMRoute.Should().Be("East Route");
            updated.PMRoute.Should().Be("West Route");
        }

        [Test]
        public void UpdateStudentAddressAsync_InvalidState_Throws()
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

            Func<Task> act = async () => await _studentService.UpdateStudentAddressAsync(s.StudentId, "123", "City", "Colorado", "12345");
            act.Should().ThrowAsync<ArgumentException>().WithMessage("*State must be a 2-letter abbreviation*");
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
    }
}
