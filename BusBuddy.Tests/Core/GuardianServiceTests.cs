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
    public class GuardianServiceTests
    {
        private Mock<BusBuddyDbContext> _mockDbContext = null!;
        private Mock<ILogger> _mockLogger = null!;
        private GuardianService _service = null!;

        [SetUp]
        public void Setup()
        {
            _mockDbContext = new Mock<BusBuddyDbContext>();
            _mockLogger = new Mock<ILogger>();
            _service = new GuardianService(_mockDbContext.Object, _mockLogger.Object);
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

            var guardiansDbSet = new Mock<DbSet<Guardian>>();
            _mockDbContext.Setup(c => c.Guardians).Returns(guardiansDbSet.Object);
            guardiansDbSet.Setup(d => d.Add(It.IsAny<Guardian>()));
            _mockDbContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

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
            var guardians = new List<Guardian> { guardian };
            var queryable = guardians.AsQueryable();

            var guardiansDbSet = new Mock<DbSet<Guardian>>();
            guardiansDbSet.As<IQueryable<Guardian>>().Setup(m => m.Provider).Returns(queryable.Provider);
            guardiansDbSet.As<IQueryable<Guardian>>().Setup(m => m.Expression).Returns(queryable.Expression);
            guardiansDbSet.As<IQueryable<Guardian>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            guardiansDbSet.As<IQueryable<Guardian>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

            _mockDbContext.Setup(c => c.Guardians).Returns(guardiansDbSet.Object);

            var result = await _service.GetGuardiansForStudentAsync(0); // Use actual studentId if needed
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Notes, Is.EqualTo("Emergency contact"));
        }
    }
}
