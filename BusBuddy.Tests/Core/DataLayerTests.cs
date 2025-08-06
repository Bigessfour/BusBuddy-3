using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Simple, focused data layer tests for BusBuddy transportation models
    /// Critical for kids waiting on their route schedules - keep it simple and working
    /// MVP Focus: Students and Routes testing
    /// </summary>
    [TestFixture]
    public class DataLayerTests : IDisposable
    {
        private BusBuddyDbContext _context = null!;
        private IServiceProvider _serviceProvider = null!;
        private bool _disposed;

        [SetUp]
        public void Setup()
        {
            // Simple in-memory database setup for each test
            var services = new ServiceCollection();
            services.AddDbContext<BusBuddyDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

            services.AddLogging();

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<BusBuddyDbContext>();

            // Ensure database is created
            _context.Database.EnsureCreated();
        }

        [TearDown]
        public void TearDown()
        {
            Dispose();
        }

        #region MVP Priority: Student Tests

        [Test]
        public async Task Student_CanCreate_BasicTest()
        {
            // Arrange
            var student = new Student
            {
                StudentNumber = "STU001",
                StudentName = "John Doe",
                HomeAddress = "123 Main St",
                HomePhone = "555-1234",
                Grade = "5"
            };

            // Act
            _context.Students.Add(student);
            var result = await _context.SaveChangesAsync();

            // Assert
            Assert.That(result, Is.EqualTo(1));
            Assert.That(student.StudentId, Is.GreaterThan(0));

            var savedStudent = await _context.Students.FirstOrDefaultAsync(s => s.StudentNumber == "STU001");
            Assert.That(savedStudent, Is.Not.Null);
            Assert.That(savedStudent.StudentName, Is.EqualTo("John Doe"));
        }

        [Test]
        public async Task Student_CanUpdate_BasicTest()
        {
            // Arrange
            var student = new Student
            {
                StudentNumber = "STU002",
                StudentName = "Jane Doe",
                HomeAddress = "456 Oak Ave",
                Grade = "3"
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Act
            student.Grade = "4";
            student.HomeAddress = "789 Pine St";
            _context.Entry(student).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Assert
            var updatedStudent = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == student.StudentId);
            Assert.That(updatedStudent, Is.Not.Null);
            Assert.That(updatedStudent.Grade, Is.EqualTo("4"));
            Assert.That(updatedStudent.HomeAddress, Is.EqualTo("789 Pine St"));
        }

        [Test]
        public async Task Student_CanDelete_BasicTest()
        {
            // Arrange
            var student = new Student
            {
                StudentNumber = "STU003",
                StudentName = "Bob Smith",
                HomeAddress = "321 Elm St",
                Grade = "2"
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Act
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            // Assert
            var deletedStudent = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == student.StudentId);
            Assert.That(deletedStudent, Is.Null);
        }

        #endregion

        #region MVP Priority: Route Tests

        [Test]
        public async Task Route_CanCreate_BasicTest()
        {
            // Arrange
            var route = new Route
            {
                RouteName = "Route 101",
                Date = DateTime.Today,
                Description = "Morning Elementary Route",
                IsActive = true,
                School = "Lincoln Elementary"
            };

            // Act
            _context.Routes.Add(route);
            var result = await _context.SaveChangesAsync();

            // Assert
            Assert.That(result, Is.EqualTo(1));
            Assert.That(route.RouteId, Is.GreaterThan(0));

            var savedRoute = await _context.Routes.FirstOrDefaultAsync(r => r.RouteName == "Route 101");
            Assert.That(savedRoute, Is.Not.Null);
            Assert.That(savedRoute.IsActive, Is.True);
            Assert.That(savedRoute.Description, Is.EqualTo("Morning Elementary Route"));
        }

        [Test]
        public async Task Route_CanUpdate_BasicTest()
        {
            // Arrange
            var route = new Route
            {
                RouteName = "Route 102",
                Date = DateTime.Today,
                Description = "Afternoon Route",
                IsActive = true,
                School = "Washington Elementary"
            };
            _context.Routes.Add(route);
            await _context.SaveChangesAsync();

            // Act
            route.Description = "Updated Afternoon Route";
            route.AMRiders = 25;
            _context.Entry(route).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Assert
            var updatedRoute = await _context.Routes.FirstOrDefaultAsync(r => r.RouteId == route.RouteId);
            Assert.That(updatedRoute, Is.Not.Null);
            Assert.That(updatedRoute.Description, Is.EqualTo("Updated Afternoon Route"));
            Assert.That(updatedRoute.AMRiders, Is.EqualTo(25));
        }

        [Test]
        public async Task Route_CanAssignStudents_BasicTest()
        {
            // Arrange
            var route = new Route
            {
                RouteName = "Route 103",
                Date = DateTime.Today,
                Description = "Pine Street Route",
                IsActive = true,
                School = "Pine Street Elementary"
            };

            var student1 = new Student
            {
                StudentNumber = "STU004",
                StudentName = "Alice Johnson",
                HomeAddress = "100 Pine St",
                Grade = "1",
                AMRoute = "Route 103"
            };

            var student2 = new Student
            {
                StudentNumber = "STU005",
                StudentName = "Charlie Brown",
                HomeAddress = "200 Pine St",
                Grade = "2",
                AMRoute = "Route 103"
            };

            _context.Routes.Add(route);
            _context.Students.Add(student1);
            _context.Students.Add(student2);
            await _context.SaveChangesAsync();

            // Act - Verify route and student assignments
            var savedRoute = await _context.Routes.FirstOrDefaultAsync(r => r.RouteName == "Route 103");
            var assignedStudents = await _context.Students.Where(s => s.AMRoute == "Route 103").ToListAsync();

            // Assert
            Assert.That(savedRoute, Is.Not.Null);
            Assert.That(assignedStudents.Count, Is.EqualTo(2));
            Assert.That(assignedStudents.All(s => s.AMRoute == "Route 103"), Is.True);
        }

        #endregion

        #region Deferred: Vehicle Tests (Post-MVP)

        [Test]
        public void Vehicle_CanCreate_BasicTest()
        {
            // TODO: Implement post-MVP - Vehicle management deferred
            Assert.Pass("Vehicle tests deferred to post-MVP phase");
        }

        #endregion

        #region Deferred: Driver Tests (Post-MVP)

        [Test]
        public void Driver_CanCreate_BasicTest()
        {
            // TODO: Implement post-MVP - Driver management deferred
            Assert.Pass("Driver tests deferred to post-MVP phase");
        }

        #endregion

        #region Context Health Tests

        [Test]
        public async Task DbContext_CanConnect_Successfully()
        {
            // Arrange & Act
            var canConnect = await _context.Database.CanConnectAsync();

            // Assert
            Assert.That(canConnect, Is.True);
        }

        [Test]
        public void DbContext_HasRequiredDbSets()
        {
            // Assert
            Assert.That(_context.Students, Is.Not.Null);
            Assert.That(_context.Routes, Is.Not.Null);
            // Vehicle and Driver sets exist but testing deferred
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context?.Dispose();
                    _serviceProvider?.GetService<IServiceScope>()?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
