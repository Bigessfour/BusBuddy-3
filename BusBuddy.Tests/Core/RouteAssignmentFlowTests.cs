using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// End-to-end student → route → report proof at the Core layer
    /// (roadmap item #12 / action-items P1).
    /// SeedDataService → StudentService → RouteService → PdfReportService.
    /// </summary>
    [TestFixture]
    [Category("Core")]
    public class RouteAssignmentFlowTests
    {
        private const string ProofStudentName = "E2E Proof Student";

        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private SeedDataService _seedService = null!;
        private StudentService _studentService = null!;
        private RouteService _routeService = null!;
        private PdfReportService _pdfService = null!;

        [SetUp]
        public void SetUp()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RouteAssignFlow_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);
            _dbContext.Database.EnsureCreated();

            var factory = new TestDbContextFactory(_dbOptions);
            _seedService = new SeedDataService(factory);
            _studentService = new StudentService(factory);
            _routeService = new RouteService(factory);
            _pdfService = new PdfReportService();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _dbContext.Database.EnsureDeleted();
            }
            catch
            {
                // ignore teardown exceptions
            }
            finally
            {
                _dbContext.Dispose();
            }
        }

        [Test]
        public async Task Seed_AddStudent_AssignToRoute_GenerateRouteSummaryPdf_ContainsStudentAndRoute()
        {
            await _seedService.SeedDriversAsync(2);
            await _seedService.SeedBusesAsync(2);
            await _seedService.SeedRoutesAsync(3);
            await _seedService.SeedStudentsFromCsvAsync();

            var csvStudents = await _dbContext.Students.AsNoTracking().ToListAsync();
            Assert.That(csvStudents.Count, Is.GreaterThanOrEqualTo(2), "CSV seed should import the embedded Wiley riders");

            var route = await EnsureActiveRouteAsync();
            var stop = await AddProofStopAsync(route.RouteId);

            var added = await _studentService.AddStudentAsync(new Student
            {
                StudentName = ProofStudentName,
                Grade = "3",
                School = "Wiley",
                ParentGuardian = "Proof Guardian",
                EmergencyPhone = "555-010-1234",
                HomePhone = "555-010-5678",
                HomeAddress = "100 Proof Lane",
                City = "Wiley",
                State = "CO",
                Zip = "81092",
                Active = true
            });
            Assert.That(added.StudentId, Is.GreaterThan(0));

            var assignResult = await _routeService.AssignStudentToRouteAsync(
                added.StudentId, route.RouteId, RouteTimeSlot.AM);
            Assert.That(assignResult.IsSuccess, Is.True, assignResult.Error);

            var csvStudent = csvStudents.First();
            var csvAssign = await _routeService.AssignStudentToRouteAsync(
                csvStudent.StudentId, route.RouteId, RouteTimeSlot.AM);
            Assert.That(csvAssign.IsSuccess, Is.True, csvAssign.Error);

            var assigned = await _routeService.GetStudentsForRouteAsync(route.RouteId, RouteTimeSlot.AM);
            Assert.That(assigned.IsSuccess, Is.True, assigned.Error);
            Assert.That(assigned.Value!.Any(s => s.StudentId == added.StudentId), Is.True);
            Assert.That(assigned.Value.Any(s => s.StudentId == csvStudent.StudentId), Is.True);

            var bus = route.AMVehicleId.HasValue
                ? await _dbContext.Buses.AsNoTracking().FirstOrDefaultAsync(b => b.BusId == route.AMVehicleId.Value)
                : null;
            var driver = route.AMDriverId.HasValue
                ? await _dbContext.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == route.AMDriverId.Value)
                : null;

            var pdf = _pdfService.GenerateRouteSummaryReport(
                route,
                new[] { stop },
                assigned.Value!,
                bus,
                driver,
                RouteTimeSlot.AM);

            Assert.That(pdf, Is.Not.Null);
            Assert.That(pdf.Length, Is.GreaterThan(100));
            Assert.That(pdf[0], Is.EqualTo((byte)'%'));
            Assert.That(pdf[1], Is.EqualTo((byte)'P'));
            Assert.That(pdf[2], Is.EqualTo((byte)'D'));
            Assert.That(pdf[3], Is.EqualTo((byte)'F'));

            Assert.That(PdfContainsText(pdf, ProofStudentName), Is.True, "Route PDF should include the assigned student name");
            Assert.That(PdfContainsText(pdf, route.RouteName!), Is.True, "Route PDF should include the route name");
            Assert.That(PdfContainsText(pdf, "AM"), Is.True, "Route PDF should include the AM slot");
        }

        /// <summary>
        /// Syncfusion may emit literal or hex-encoded PDF strings; accept either.
        /// </summary>
        private static bool PdfContainsText(byte[] pdf, string text)
        {
            var latin1 = Encoding.Latin1.GetString(pdf);
            if (latin1.Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            var hex = Convert.ToHexString(Encoding.ASCII.GetBytes(text));
            return latin1.Contains(hex, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Route> EnsureActiveRouteAsync()
        {
            var route = await _dbContext.Routes.AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RouteId)
                .FirstOrDefaultAsync();

            if (route != null)
            {
                return route;
            }

            var inactive = await _dbContext.Routes.OrderBy(r => r.RouteId).FirstAsync();
            inactive.IsActive = true;
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            return await _dbContext.Routes.AsNoTracking().FirstAsync(r => r.RouteId == inactive.RouteId);
        }

        private async Task<RouteStop> AddProofStopAsync(int routeId)
        {
            var arrival = DateTime.Today.AddHours(7).AddMinutes(15);
            var stop = new RouteStop
            {
                RouteId = routeId,
                StopName = "Proof Stop",
                StopAddress = "100 Proof Lane, Wiley, CO",
                StopOrder = 1,
                ScheduledArrival = new TimeSpan(7, 15, 0),
                ScheduledDeparture = new TimeSpan(7, 18, 0),
                EstimatedArrivalTime = arrival,
                EstimatedDepartureTime = arrival.AddMinutes(3),
                StopDuration = 3,
                Status = "Active",
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.RouteStops.Add(stop);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            return stop;
        }
    }
}
