using NUnit.Framework;
using BusBuddy.Core.Data;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Moq;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [Category("Unit")]
    public class FamilyServiceTests
    {
        private Mock<BusBuddyDbContext> _mockDbContext = null!;
        private Mock<ILogger> _mockLogger = null!;
        private FamilyService _service = null!;

        [SetUp]
        public void Setup()
        {
            _mockDbContext = new Mock<BusBuddyDbContext>();
            _mockLogger = new Mock<ILogger>();
            _service = new FamilyService(_mockDbContext.Object, _mockLogger.Object);
        }

        [Test]
        public async Task AddFamilyWithGuardians_Succeeds()
        {
            var family = new Family { ParentGuardian = "Smith" };
            var guardians = new List<Guardian> {
                new Guardian {
                    FirstName = "John",
                    LastName = "Smith",
                    Address = "123 Main St",
                    Phone = "555-1234",
                    FamilyId = 1
                }
            };
            family.Guardians = guardians;

            var familiesDbSet = new Mock<DbSet<Family>>();
            _mockDbContext.Setup(c => c.Families).Returns(familiesDbSet.Object);
            familiesDbSet.Setup(d => d.Add(It.IsAny<Family>()));
            _mockDbContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

            var result = await _service.AddFamilyAsync(family);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ParentGuardian, Is.EqualTo("Smith"));
            Assert.That(result.Guardians.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetFamilyById_ReturnsFamily()
        {
            var family = new Family { FamilyId = 1, ParentGuardian = "Smith" };
            var families = new List<Family> { family };
            var queryable = families.AsQueryable();

            var familiesDbSet = new Mock<DbSet<Family>>();
            familiesDbSet.As<IQueryable<Family>>().Setup(m => m.Provider).Returns(queryable.Provider);
            familiesDbSet.As<IQueryable<Family>>().Setup(m => m.Expression).Returns(queryable.Expression);
            familiesDbSet.As<IQueryable<Family>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            familiesDbSet.As<IQueryable<Family>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

            _mockDbContext.Setup(c => c.Families).Returns(familiesDbSet.Object);

            var result = await _service.GetFamilyAsync(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FamilyId, Is.EqualTo(1));
            Assert.That(result.ParentGuardian, Is.EqualTo("Smith"));
        }
    }
}
