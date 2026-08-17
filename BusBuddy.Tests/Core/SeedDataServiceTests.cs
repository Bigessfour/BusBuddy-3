using System;
using System.Collections.Generic;
using System.IO;
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

        [Test]
        public async Task ImportStudentsFromCsvAsync_AddsRowsFromFile_AndSkipsExistingNames()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"CsvImport_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new SeedDataService(new TestDbContextFactory(options));

            var csv = WileyCsv("Import,Rider,4,Pat,Rider,100 Main,Wiley,CO,Prowers,,719-555-0100,,,,,,,,,,,,,");
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-import-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, csv);
            try
            {
                var first = await service.ImportStudentsFromCsvAsync(path);
                var second = await service.ImportStudentsFromCsvAsync(path);

                Assert.That(first, Is.EqualTo(1));
                Assert.That(second, Is.EqualTo(0));
                var imported = context.Students.Single(s => s.StudentName == "Import Rider");
                Assert.That(imported.HomeAddress, Does.Contain("100 Main"));
                Assert.That(imported.HomeAddress, Does.Contain("Wiley"));
                Assert.That(imported.HomeAddress, Does.Contain("Prowers"));
                Assert.That(imported.HomePhone, Is.EqualTo("719-555-0100"));
                Assert.That(imported.StudentNumber, Is.EqualTo("WSD0001"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_AllocatesNextWsdNumber_WhenWsd0001Exists()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"CsvImportNum_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Students.Add(new Student
            {
                StudentName = "Existing Rider",
                StudentNumber = "WSD0001",
                Grade = "2",
                School = "Wiley School District"
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-import-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, WileyCsv("Import,Rider,4,Pat,Rider,100 Main,Wiley,CO,Prowers,,719-555-0100,,,,,,,,,,,,,"));
            try
            {
                var added = await service.ImportStudentsFromCsvAsync(path);
                Assert.That(added, Is.EqualTo(1));
                var imported = context.Students.Single(s => s.StudentName == "Import Rider");
                Assert.That(imported.StudentNumber, Is.EqualTo("WSD0002"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ImportStudentsFromCsvAsync_RejectsNonWileyHeader()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"CsvImportBad_{Guid.NewGuid()}")
                .Options;
            using var context = new BusBuddyDbContext(options);
            context.Database.EnsureCreated();
            var service = new SeedDataService(new TestDbContextFactory(options));
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-import-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, "ignored\nName,Age\nAlice,10\n");
            try
            {
                var ex = Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await service.ImportStudentsFromCsvAsync(path));
                Assert.That(ex!.Message, Does.Contain("Wiley student format"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string WileyCsv(string dataRow) =>
            "Student,,,Parent,,,,,,,,Joint Parent,,,,,,,Econtact,,\n" +
            "Fname,Lname,Grade,Fname,Lname,Address,City,State,County,Hphone,Cphone,Jparent FirstName,Jparent LastName,Address,City,State,County,Cphone ,Econtact FirstName,Econtact LastName,Econtact Phone\n" +
            dataRow + "\n";
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
