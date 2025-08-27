using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [NonParallelizable] // Disable parallel execution to prevent database conflicts
    public class FamilyServiceTests : IDisposable
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private FamilyService _familyService = null!;
        private ILogger _logger = null!;
        private TestDbContextFactory _contextFactory = null!;

        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly DbContextOptions<BusBuddyDbContext> _options;
            public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
            public BusBuddyDbContext CreateDbContext() => new BusBuddyDbContext(_options);
            public BusBuddyDbContext CreateWriteDbContext() => new BusBuddyDbContext(_options);
            public void Dispose() { }
        }

        [SetUp]
        public void SetUp()
        {
            // Use a unique database name for each test to ensure complete isolation
            var databaseName = $"FamiliesDb_{Guid.NewGuid()}_{DateTime.Now.Ticks}_{TestContext.CurrentContext.Test.Name.Replace(" ", "_")}";

            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            _contextFactory = new TestDbContextFactory(_dbOptions);
            _dbContext = _contextFactory.CreateDbContext();
            _logger = Log.ForContext<FamilyService>();
            _familyService = new FamilyService(_dbContext, _logger);

            // Seed minimal test data
            SeedTestData();
        }

        private void SeedTestData()
        {
            // Use a separate context for seeding to avoid tracking conflicts with the main context
            using var seedContext = new BusBuddyDbContext(_dbOptions);

            // Create all test data in a single transaction
            var families = new[]
            {
                new Family
                {
                    FamilyId = 1,
                    ParentGuardian = "John Smith",
                    Address = "123 Main St",
                    City = "Test City",
                    County = "Test County",
                    HomePhone = "555-010-1234",
                    CellPhone = "555-010-5678"
                },
                new Family
                {
                    FamilyId = 2,
                    ParentGuardian = "Bob Johnson",
                    Address = "456 Oak Ave",
                    City = "Test City",
                    County = "Test County",
                    HomePhone = "555-020-1234",
                    CellPhone = "555-020-5678"
                },
                new Family
                {
                    FamilyId = 3,
                    ParentGuardian = "Alice Brown",
                    Address = "789 Pine St",
                    City = "Another City",
                    County = "Another County",
                    HomePhone = "555-030-1234"
                }
            };

            var students = new[]
            {
                new Student
                {
                    StudentId = 1,
                    StudentName = "Alice Smith",
                    Grade = "3",
                    School = "Test School",
                    ParentGuardian = "John Smith",
                    EmergencyPhone = "555-010-9012",
                    HomeAddress = "123 Main St",
                    City = "Test City",
                    State = "TX",
                    Zip = "12345",
                    Active = true,
                    FamilyId = 1
                },
                new Student
                {
                    StudentId = 2,
                    StudentName = "Charlie Smith",
                    Grade = "1",
                    School = "Test School",
                    ParentGuardian = "John Smith",
                    EmergencyPhone = "555-010-9012",
                    HomeAddress = "123 Main St",
                    City = "Test City",
                    State = "TX",
                    Zip = "12345",
                    Active = true,
                    FamilyId = 1
                },
                new Student
                {
                    StudentId = 3,
                    StudentName = "Bob Johnson Jr.",
                    Grade = "4",
                    School = "Test School",
                    ParentGuardian = "Bob Johnson",
                    EmergencyPhone = "555-020-3456",
                    HomeAddress = "456 Oak Ave",
                    City = "Test City",
                    State = "TX",
                    Zip = "12346",
                    Active = true,
                    FamilyId = 2
                }
            };

            // Add all entities and save in one operation
            seedContext.Families.AddRange(families);
            seedContext.Students.AddRange(students);
            seedContext.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_dbContext != null)
                {
                    // Clear change tracker to avoid tracking conflicts
                    _dbContext.ChangeTracker.Clear();

                    // Ensure database is deleted
                    _dbContext.Database.EnsureDeleted();

                    // Dispose context
                    _dbContext.Dispose();
                }
            }
            catch (Exception)
            {
                // Ignore disposal errors in test cleanup
            }
            finally
            {
                _dbContext = null!;
                _contextFactory?.Dispose();
                _contextFactory = null!;
                _logger = null!;
                _familyService = null!;
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                TearDown();
                GC.SuppressFinalize(this);
            }
        }

        [Test]
        public async Task GetAllFamiliesAsync_ReturnsAllFamilies()
        {
            // Act
            var families = await _familyService.GetAllFamiliesAsync();

            // Assert
            families.Should().NotBeNull();
            families.Should().HaveCount(3);
        }

        [Test]
        public async Task GetFamilyAsync_ExistingId_ReturnsFamilyWithRelatedEntities()
        {
            // Act
            var family = await _familyService.GetFamilyAsync(1);

            // Assert
            family.Should().NotBeNull();
            family!.ParentGuardian.Should().Be("John Smith");
            family.Address.Should().Be("123 Main St");
            family.Students.Should().NotBeNull();
            family.Students.Should().HaveCount(2); // Alice and Charlie Smith
        }

        [Test]
        public async Task GetFamilyAsync_NonExistingId_ReturnsNull()
        {
            // Act
            var family = await _familyService.GetFamilyAsync(999);

            // Assert
            family.Should().BeNull();
        }

        [Test]
        public async Task AddFamilyAsync_ValidFamily_PersistsSuccessfully()
        {
            // Arrange
            var newFamily = new Family
            {
                ParentGuardian = "Sarah Davis",
                Address = "999 New St",
                City = "New City",
                County = "New County",
                HomePhone = "555-099-9999",
                CellPhone = "555-099-8888"
            };

            // Act
            var addedFamily = await _familyService.AddFamilyAsync(newFamily);

            // Assert
            addedFamily.Should().NotBeNull();
            addedFamily.FamilyId.Should().BeGreaterThan(0);
            addedFamily.ParentGuardian.Should().Be("Sarah Davis");
            addedFamily.Address.Should().Be("999 New St");

            // Verify in database
            var fromDb = await _dbContext.Families.FindAsync(addedFamily.FamilyId);
            fromDb.Should().NotBeNull();
            fromDb!.ParentGuardian.Should().Be("Sarah Davis");
        }

        [Test]
        public async Task UpdateFamilyAsync_ValidFamily_UpdatesSuccessfully()
        {
            // Arrange
            const int familyId = 1;
            var familyToUpdate = new Family
            {
                FamilyId = familyId,
                ParentGuardian = "John Smith Updated",
                Address = "123 Updated St",
                City = "Updated City",
                County = "Updated County",
                HomePhone = "555-111-1234",
                CellPhone = "555-111-5678"
            };

            // Act
            var updatedFamily = await _familyService.UpdateFamilyAsync(familyToUpdate);

            // Assert
            updatedFamily.Should().NotBeNull();
            updatedFamily!.ParentGuardian.Should().Be("John Smith Updated");
            updatedFamily.Address.Should().Be("123 Updated St");

            // Verify in database
            var fromDb = await _dbContext.Families.FindAsync(familyId);
            fromDb.Should().NotBeNull();
            fromDb!.ParentGuardian.Should().Be("John Smith Updated");
        }

        [Test]
        public async Task DeleteFamilyAsync_ValidId_DeletesSuccessfully()
        {
            // Arrange
            const int familyId = 3;

            // Act
            var result = await _familyService.DeleteFamilyAsync(familyId);

            // Assert
            result.Should().BeTrue();

            // Verify in database
            var deletedFamily = await _dbContext.Families.FindAsync(familyId);
            deletedFamily.Should().BeNull();
        }

        [Test]
        public async Task GetFamilyAsync_IncludesStudents()
        {
            // Act
            var family = await _familyService.GetFamilyAsync(1);

            // Assert
            family.Should().NotBeNull();
            family!.Students.Should().NotBeNull();
            family.Students.Should().HaveCount(2);
            family.Students.All(s => s.FamilyId == 1).Should().BeTrue();
        }

        [Test]
        public async Task AddFamilyAsync_FamilyWithContacts_SavesContactInfo()
        {
            // Arrange
            var familyWithContacts = new Family
            {
                ParentGuardian = "Test Family",
                Address = "123 Test St",
                City = "Test City",
                County = "Test County",
                HomePhone = "555-123-4567",
                CellPhone = "555-567-8901",
                EmergencyContact = "Emergency Contact"
            };

            // Act
            var addedFamily = await _familyService.AddFamilyAsync(familyWithContacts);

            // Assert
            addedFamily.Should().NotBeNull();
            addedFamily.HomePhone.Should().Be("555-123-4567");
            addedFamily.CellPhone.Should().Be("555-567-8901");
            addedFamily.EmergencyContact.Should().Be("Emergency Contact");

            // Verify in database
            var fromDb = await _dbContext.Families.FindAsync(addedFamily.FamilyId);
            fromDb.Should().NotBeNull();
            fromDb!.HomePhone.Should().Be("555-123-4567");
            fromDb.CellPhone.Should().Be("555-567-8901");
        }
    }
}
