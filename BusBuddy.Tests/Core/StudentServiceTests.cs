using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class StudentServiceTests : DatabaseTestBase
    {
        private StudentService _studentService = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // Create the service using the inherited context factory
            _studentService = new StudentService(ContextFactory);
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
        public async Task AddStudentAsync_ValidStudent_PersistsAndSetsDefaults()
        {
            // Arrange
            var newStudent = new Student
            {
                StudentName = "David Wilson",
                Grade = "5",
                School = "Test School",
                ParentGuardian = "Sarah Wilson",
                EmergencyPhone = "555-019-9999",
                HomeAddress = "999 Test St",
                City = "Test City",
                State = "TX",
                Zip = "12399"
            };

            // Act
            var addedStudent = await _studentService.AddStudentAsync(newStudent);

            // Assert
            addedStudent.Should().NotBeNull();
            addedStudent.StudentId.Should().BeGreaterThan(0);
            addedStudent.EnrollmentDate.Should().NotBeNull();
            addedStudent.Active.Should().BeTrue(); // Default value

            // Verify in database using service method to avoid context issues
            var fromDb = await _studentService.GetStudentByIdAsync(addedStudent.StudentId);
            fromDb.Should().NotBeNull();
            fromDb!.StudentName.Should().Be("David Wilson");
            fromDb.Grade.Should().Be("5");
        }

        [Test]
        public async Task GetStudentsByRouteAsync_ReturnsStudentsOnRoute()
        {
            // Act
            var eastStudents = await _studentService.GetStudentsByRouteAsync("East Route");

            // Assert
            eastStudents.Should().NotBeNull();
            eastStudents.Should().HaveCount(2); // Alice and Charlie on East Route
            eastStudents.First().StudentName.Should().Be("Alice Johnson");
        }

        [Test]
        public async Task GetActiveStudentsAsync_ReturnsOnlyActiveStudents()
        {
            // Act
            var activeStudents = await _studentService.GetActiveStudentsAsync();

            // Assert
            activeStudents.Should().NotBeNull();
            activeStudents.Should().HaveCount(4); // Alice, Bob, Charlie, Diana
            activeStudents.All(s => s.Active).Should().BeTrue();
            activeStudents.Select(s => s.StudentName).Should().BeEquivalentTo(new[] { "Alice Johnson", "Bob Smith", "Charlie Brown", "Diana Prince" });
        }

        [Test]
        public async Task GetStudentsBySchoolAsync_ReturnsStudentsFromSchool()
        {
            // Act
            var schoolStudents = await _studentService.GetStudentsBySchoolAsync("Test School");

            // Assert
            schoolStudents.Should().NotBeNull();
            schoolStudents.Count.Should().Be(5); // All 5 students from TestDataSeeder
            schoolStudents.All(s => s.School == "Test School").Should().BeTrue();
        }

        [Test]
        public async Task AssignStudentToRouteAsync_UpdatesRoutesCorrectly()
        {
            // Arrange - Use student ID 2 (Bob Smith) who has PM route but no AM route
            const int studentId = 2;

            // Act
            var result = await _studentService.AssignStudentToRouteAsync(studentId, "West Route", "North Route");

            // Assert
            result.Should().BeTrue();

            // Verify in database using service method to avoid context issues
            var updatedStudent = await _studentService.GetStudentByIdAsync(studentId);
            updatedStudent.Should().NotBeNull();
            updatedStudent!.AMRoute.Should().Be("West Route");
            updatedStudent.PMRoute.Should().Be("North Route");
        }

        [Test]
        public async Task ValidateStudentAsync_InvalidPhone_ReturnsErrors()
        {
            // Arrange
            var invalidStudent = new Student
            {
                StudentName = "Test Student",
                Grade = "3",
                School = "Test School",
                ParentGuardian = "Test Parent",
                EmergencyPhone = "invalid-phone",
                HomeAddress = "123 Test St",
                City = "Test City",
                State = "TX",
                Zip = "12345"
            };

            // Act
            var errors = await _studentService.ValidateStudentAsync(invalidStudent);

            // Assert
            errors.Should().NotBeNull();
            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("phone", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public async Task ValidateStudentAsync_ValidStudent_ReturnsNoErrors()
        {
            // Arrange
            var validStudent = new Student
            {
                StudentName = "Valid Student",
                Grade = "3",
                School = "Test School",
                ParentGuardian = "Valid Parent",
                EmergencyPhone = "555-123-4567",
                HomeAddress = "123 Valid St",
                City = "Valid City",
                State = "TX",
                Zip = "12345"
            };

            // Act
            var errors = await _studentService.ValidateStudentAsync(validStudent);

            // Assert
            errors.Should().NotBeNull();
            errors.Should().BeEmpty();
        }

        [Test]
        public async Task ExportStudentsToCsvAsync_IncludesAllStudents()
        {
            // Act
            var csv = await _studentService.ExportStudentsToCsvAsync();

            // Assert
            csv.Should().NotBeNullOrEmpty();
            csv.Should().StartWith("Student ID,Student Number,Student Name");

            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Length.Should().BeGreaterThan(1); // Header + at least one data row
        }

        [Test]
        public async Task GetStudentStatisticsAsync_ReturnsCorrectCounts()
        {
            // Act
            var stats = await _studentService.GetStudentStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.Should().ContainKey("TotalStudents");
            stats.Should().ContainKey("ActiveStudents");
            stats.Should().ContainKey("StudentsWithRoutes");

            stats["TotalStudents"].Should().Be(5); // All 5 students from TestDataSeeder
            stats["ActiveStudents"].Should().Be(4); // 4 active students (Eve is inactive)
            stats["StudentsWithRoutes"].Should().Be(5); // All students have at least one route
        }

        [Test]
        public async Task SearchStudentsAsync_ByName_ReturnsMatchingStudents()
        {
            // Act
            var results = await _studentService.SearchStudentsAsync("Alice");

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(1);
            results.First().StudentName.Should().Be("Alice Johnson");
        }

        [Test]
        public async Task UpdateStudentAddressAsync_ValidAddress_UpdatesSuccessfully()
        {
            // Arrange
            const int studentId = 1;

            // Act
            var result = await _studentService.UpdateStudentAddressAsync(studentId, "456 Updated St", "Updated City", "CA", "98765");
            result.Should().BeTrue();

            // Assert - Verify using service method to avoid context issues
            var updatedStudent = await _studentService.GetStudentByIdAsync(studentId);
            updatedStudent.Should().NotBeNull();
            updatedStudent!.HomeAddress.Should().Be("456 Updated St");
            updatedStudent.City.Should().Be("Updated City");
            updatedStudent.State.Should().Be("CA");
            updatedStudent.Zip.Should().Be("98765");
        }

        [Test]
        public void UpdateStudentAddressAsync_InvalidState_ThrowsArgumentException()
        {
            // Arrange
            const int studentId = 1;

            // Act & Assert
            Func<Task> act = async () => await _studentService.UpdateStudentAddressAsync(studentId, "123 Test St", "Test City", "InvalidState", "12345");
            act.Should().ThrowAsync<ArgumentException>().WithMessage("*State must be a 2-letter abbreviation*");
        }
    }
}
