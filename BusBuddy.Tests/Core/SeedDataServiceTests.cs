using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class SeedDataServiceTests
    {
        [Test]
        public async Task SeedStudentsFromCsvAsync_AddsAllStudents_NoDuplicates()
        {
            var mockFactory = new Mock<IBusBuddyDbContextFactory>();
            var students = new List<Student>();
            var families = new List<Family>();
            var mockContext = new Mock<BusBuddyDbContext>();
            mockContext.Setup(c => c.Students).ReturnsDbSet(students);
            mockContext.Setup(c => c.Families).ReturnsDbSet(families);
            mockFactory.Setup(f => f.CreateDbContext()).Returns(mockContext.Object);

            var service = new SeedDataService(mockFactory.Object);
            await service.SeedStudentsFromCsvAsync();

            Assert.That(students.Count, Is.EqualTo(4)); // Adjust to CSV row count
            Assert.That(students.Select(s => s.StudentNumber).Distinct().Count(), Is.EqualTo(students.Count));
        }

        [Test]
        public async Task SeedStudentsFromCsvAsync_GeneratesStudentNumber_WhenMissing()
        {
            // Setup: Use a CSV row with blank Student # (modify SeedDataService for testability if needed)
            // ...mock setup as above...
            // After seeding:
            // Assert.That(students.Any(s => s.StudentNumber.StartsWith("STU")), Is.True);
        }

        [Test]
        public async Task SeedStudentsFromCsvAsync_SkipsInvalidRows_AndLogs()
        {
            // Setup: Add a row with all fields blank or missing required fields
            // ...mock setup as above...
            // After seeding:
            // Assert that no student was added for that row
            // Optionally, verify logger was called with error (using Serilog test sink)
        }

        [Test]
        public async Task SeedStudentsFromCsvAsync_GroupsSiblings_SameFamily()
        {
            // Setup: Two rows, same parent, second row blanks parent fields
            // ...mock setup as above...
            // After seeding:
            // var familyIds = students.Select(s => s.FamilyId).Distinct().ToList();
            // Assert.That(familyIds.Count, Is.EqualTo(1));
        }
    }
}
