using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels.Student;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class FactoryAndViewModelIntegrationTests
    {
        private class TestFactory : IBusBuddyDbContextFactory
        {
            private readonly DbContextOptions<BusBuddyDbContext> _options;

            public TestFactory(DbContextOptions<BusBuddyDbContext> options)
            {
                _options = options;
            }

            public BusBuddyDbContext CreateDbContext()
            {
                var ctx = new BusBuddyDbContext(_options);
                ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return ctx;
            }

            public BusBuddyDbContext CreateWriteDbContext()
            {
                var ctx = new BusBuddyDbContext(_options);
                ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
                return ctx;
            }
        }

        [Test]
        public async Task StudentsViewModel_Loads_From_InMemory_Context()
        {
            // Arrange
            var dbName = $"bb_tests_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            // Seed a couple of students
            using (var seed = new BusBuddyDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.Students.Add(new Student { StudentNumber = "S-001", StudentName = "Alice" });
                seed.Students.Add(new Student { StudentNumber = "S-002", StudentName = "Bob" });
                await seed.SaveChangesAsync();
            }

            var factory = new TestFactory(options);
            var vm = new StudentsViewModel(factory /* other deps are optional in ctor */);

            // Act
            await vm.LoadStudentsAsync();

            // Assert
            vm.Students.Should().NotBeNull();
            vm.Students.Count.Should().BeGreaterOrEqualTo(2);
            vm.Students.Any(s => s.StudentName == "Alice").Should().BeTrue();
            vm.Students.Any(s => s.StudentName == "Bob").Should().BeTrue();
        }
    }
}
