using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [Category("Core")]
    public class StudentRouteOptimizerTests
    {
        private DbContextOptions<BusBuddyDbContext> _options = null!;
        private BusBuddyDbContext _context = null!;
        private StudentRouteOptimizer _optimizer = null!;

        [SetUp]
        public void SetUp()
        {
            _options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"Optimize_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context = new BusBuddyDbContext(_options);
            _context.Database.EnsureCreated();
            _optimizer = new StudentRouteOptimizer(new RouteService(new TestDbContextFactory(_options)));
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task OptimizeUnassignedAsync_AssignsAmAndPm_OnMatchingSessionRows()
        {
            _context.Routes.AddRange(
                new Route { RouteName = "North", Date = DateTime.Today, IsActive = true, Session = RouteSession.AM, School = "Oakridge" },
                new Route { RouteName = "North-PM", Date = DateTime.Today, IsActive = true, Session = RouteSession.PM, School = "Oakridge" },
                new Route { RouteName = "South", Date = DateTime.Today, IsActive = false, School = "Oakridge" });
            _context.Students.AddRange(
                NewStudent("Ada Rider"),
                NewStudent("Ben Rider"));
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var result = await _optimizer.OptimizeUnassignedAsync();

            Assert.That(result.AssignedCount, Is.EqualTo(4), result.Status);
            Assert.That(result.RemainingUnassigned, Is.EqualTo(0));
            var students = await _context.Students.AsNoTracking().ToListAsync();
            Assert.That(students.All(s => s.AMRoute == "North" && s.PMRoute == "North-PM"), Is.True);
            Assert.That(students.Any(s => s.AMRoute == "South" || s.PMRoute == "South"), Is.False);
        }

        [Test]
        public async Task OptimizeUnassignedAsync_LoneAmRow_DoesNotWritePmOntoThatRow()
        {
            _context.Routes.Add(new Route { RouteName = "North", Date = DateTime.Today, IsActive = true, Session = RouteSession.AM, School = "Oakridge" });
            _context.Students.Add(NewStudent("Ada Rider"));
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var result = await _optimizer.OptimizeUnassignedAsync();

            Assert.That(result.AssignedCount, Is.EqualTo(1), result.Status);
            var student = await _context.Students.AsNoTracking().SingleAsync();
            Assert.That(student.AMRoute, Is.EqualTo("North"));
            Assert.That(student.PMRoute, Is.Null.Or.Empty);
            Assert.That(student.PmRouteId, Is.Null);
        }

        [Test]
        public async Task OptimizeUnassignedAsync_WhenNoActiveRoutes_ReturnsStatus()
        {
            _context.Students.Add(NewStudent("Solo Rider"));
            await _context.SaveChangesAsync();

            var result = await _optimizer.OptimizeUnassignedAsync();

            Assert.That(result.AssignedCount, Is.EqualTo(0));
            Assert.That(result.Status, Does.Contain("No active routes"));
        }

        [Test]
        public async Task OptimizeUnassignedAsync_WhenEveryoneAssigned_DoesNotReassign()
        {
            _context.Routes.Add(new Route { RouteName = "East", Date = DateTime.Today, IsActive = true, School = "Oakridge" });
            _context.Students.Add(new Student
            {
                StudentName = "Done Rider",
                Grade = "3",
                School = "Oakridge",
                ParentGuardian = "P",
                EmergencyPhone = "555-0100",
                Active = true,
                AMRoute = "East",
                PMRoute = "East"
            });
            await _context.SaveChangesAsync();

            var result = await _optimizer.OptimizeUnassignedAsync();

            Assert.That(result.AssignedCount, Is.EqualTo(0));
            Assert.That(result.Status, Does.Contain("already have"));
        }

        [Test]
        public async Task OptimizeUnassignedAsync_StudentMissingOnlyPm_IsStillFilled()
        {
            _context.Routes.AddRange(
                new Route { RouteName = "West", Date = DateTime.Today, IsActive = true, Session = RouteSession.AM, School = "Oakridge" },
                new Route { RouteName = "West-PM", Date = DateTime.Today, IsActive = true, Session = RouteSession.PM, School = "Oakridge" });
            var halfDone = NewStudent("Half Rider");
            halfDone.AMRoute = "West";
            _context.Students.Add(halfDone);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var result = await _optimizer.OptimizeUnassignedAsync();

            Assert.That(result.AssignedCount, Is.EqualTo(1), result.Status);
            Assert.That(result.RemainingUnassigned, Is.EqualTo(0));
            var student = await _context.Students.AsNoTracking().SingleAsync();
            Assert.That(student.PMRoute, Is.EqualTo("West-PM"));
        }

        private static Student NewStudent(string name) => new()
        {
            StudentName = name,
            Grade = "4",
            School = "Oakridge",
            ParentGuardian = "P",
            EmergencyPhone = "555-0100",
            Active = true
        };
    }
}
