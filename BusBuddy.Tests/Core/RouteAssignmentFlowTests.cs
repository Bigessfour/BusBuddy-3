using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// End-to-end student → route → report flow (roadmap proof #12 at Core layer).
    /// </summary>
    [TestFixture]
    public class RouteAssignmentFlowTests
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private RouteService _routeService = null!;
        private PdfReportService _pdfService = null!;

        [SetUp]
        public void SetUp()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RouteAssignFlow_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);
            _dbContext.Database.EnsureCreated();

            var route = new Route
            {
                RouteName = "East Route",
                Date = DateTime.Today,
                IsActive = true,
                School = "Wiley"
            };
            _dbContext.Routes.Add(route);

            var student = new Student
            {
                StudentName = "Flow Student",
                Grade = "3",
                School = "Wiley",
                ParentGuardian = "Guardian",
                EmergencyPhone = "555-0100",
                Active = true
            };
            _dbContext.Students.Add(student);
            _dbContext.SaveChanges();

            _routeService = new RouteService(new TestDbContextFactory(_dbOptions));
            _pdfService = new PdfReportService();
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Dispose();
        }

        [Test]
        public async Task AssignStudent_GenerateRouteSummaryReport_ReturnsValidPdf()
        {
            var route = await _dbContext.Routes.FirstAsync();
            var student = await _dbContext.Students.FirstAsync();

            var assignResult = await _routeService.AssignStudentToRouteAsync(
                student.StudentId, route.RouteId, RouteTimeSlot.AM);
            Assert.That(assignResult.IsSuccess, Is.True);

            var assigned = await _routeService.GetStudentsForRouteAsync(route.RouteId, RouteTimeSlot.AM);
            Assert.That(assigned.IsSuccess, Is.True);
            Assert.That(assigned.Value!.Any(s => s.StudentId == student.StudentId), Is.True);

            var pdf = _pdfService.GenerateRouteSummaryReport(
                route,
                Array.Empty<RouteStop>(),
                assigned.Value!,
                null,
                null,
                RouteTimeSlot.AM);

            Assert.That(pdf, Is.Not.Null);
            Assert.That(pdf.Length, Is.GreaterThan(100));
            Assert.That(pdf[0], Is.EqualTo((byte)'%'));
            Assert.That(pdf[1], Is.EqualTo((byte)'P'));
            Assert.That(pdf[2], Is.EqualTo((byte)'D'));
            Assert.That(pdf[3], Is.EqualTo((byte)'F'));
        }
    }
}
