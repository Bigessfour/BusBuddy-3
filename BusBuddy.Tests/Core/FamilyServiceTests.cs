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
    public class FamilyServiceTests : IDisposable
    {
        private BusBuddyDbContext _dbContext = null!;
        private Mock<ILogger> _mockLogger = null!;
        private FamilyService _service = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"FamilyDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(options);
            _mockLogger = new Mock<ILogger>();
            _service = new FamilyService(_dbContext, _mockLogger.Object);
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

            var result = await _service.AddFamilyAsync(family);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ParentGuardian, Is.EqualTo("Smith"));
            Assert.That(result.Guardians.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetFamilyById_ReturnsFamily()
        {
            var family = new Family { FamilyId = 1, ParentGuardian = "Smith" };
            _dbContext.Families.Add(family);
            await _dbContext.SaveChangesAsync();

            var result = await _service.GetFamilyAsync(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FamilyId, Is.EqualTo(1));
            Assert.That(result.ParentGuardian, Is.EqualTo("Smith"));
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
