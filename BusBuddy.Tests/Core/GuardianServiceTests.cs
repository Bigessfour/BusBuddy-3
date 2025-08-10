using NUnit.Framework;
using BusBuddy.Core.Data;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Moq;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [Category("Unit")]
    public class GuardianServiceTests : IDisposable
    {
        private BusBuddyDbContext _dbContext = null!;
        private Mock<ILogger> _mockLogger = null!;
        private GuardianService _service = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"GuardianDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(options);
            _mockLogger = new Mock<ILogger>();
            _service = new GuardianService(_dbContext, _mockLogger.Object);
        }

        [Test]
        public async Task AddGuardian_Succeeds()
        {
            var guardian = new Guardian {
                FirstName = "Jane",
                LastName = "Doe",
                Address = "456 Oak St",
                Phone = "555-5678",
                FamilyId = 2
            };

            var result = await _service.AddGuardianAsync(guardian);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FirstName, Is.EqualTo("Jane"));
            Assert.That(result.LastName, Is.EqualTo("Doe"));
        }

        [Test]
        public async Task GetGuardiansForStudent_ReturnsNotes()
        {
            var guardian = new Guardian {
                GuardianId = 1,
                FirstName = "Jane",
                LastName = "Doe",
                Address = "456 Oak St",
                Phone = "555-5678",
                FamilyId = 2,
                Notes = "Emergency contact"
            };
            // Seed family and student to satisfy query include/any predicate
            var family = new Family { FamilyId = 2, ParentGuardian = "Doe" };
            var student = new BusBuddy.Core.Models.Student { StudentId = 100, Family = family, FamilyId = family.FamilyId, StudentName = "Test" };
            family.Students = new List<BusBuddy.Core.Models.Student> { student };
            _dbContext.Families.Add(family);
            _dbContext.Students.Add(student);
            guardian.Family = family;
            _dbContext.Guardians.Add(guardian);
            await _dbContext.SaveChangesAsync();

            var result = await _service.GetGuardiansForStudentAsync(100);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Notes, Is.EqualTo("Emergency contact"));
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
