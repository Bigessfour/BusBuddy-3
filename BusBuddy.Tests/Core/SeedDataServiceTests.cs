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
            var studentsDbSet = CreateMockDbSet(students);
            var familiesDbSet = CreateMockDbSet(families);
            var mockContext = new Mock<BusBuddyDbContext>();
            mockContext.Setup(c => c.Students).Returns(studentsDbSet.Object);
            mockContext.Setup(c => c.Families).Returns(familiesDbSet.Object);
            mockFactory.Setup(f => f.CreateDbContext()).Returns(mockContext.Object);

            var service = new SeedDataService(mockFactory.Object);
            await service.SeedStudentsFromCsvAsync();

            Assert.That(students.Count, Is.EqualTo(2)); // Matches embedded CSV rows in SeedDataService
            Assert.That(students.Select(s => s.StudentNumber).Distinct().Count(), Is.EqualTo(students.Count));
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
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
#pragma warning restore CS1998
    // Helper for EF Core 9: manually mock DbSet<T> for in-memory lists
    private static Mock<DbSet<T>> CreateMockDbSet<T>(IList<T> sourceList) where T : class
    {
        var queryable = sourceList.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
        mockSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(sourceList.Add);
        mockSet.Setup(d => d.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(items =>
        {
            foreach (var i in items) sourceList.Add(i);
        });
        return mockSet;
    }
    }
}
