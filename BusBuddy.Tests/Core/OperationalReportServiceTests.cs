using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    [Category("Core")]
    public class OperationalReportServiceTests
    {
        private string _dir = null!;
        private Mock<IStudentService> _students = null!;
        private Mock<IRouteService> _routes = null!;
        private OperationalReportService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BusBuddyReports", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _students = new Mock<IStudentService>();
            _routes = new Mock<IRouteService>();
            _students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync(new List<Student>
            {
                new() { StudentName = "Ada Rider", Grade = "4", School = "Wiley", AMRoute = "North", PMRoute = "North" },
                new() { StudentName = "Ben Rider", Grade = "2", School = "Wiley" }
            });
            _routes.Setup(r => r.GetAllActiveRoutesAsync()).ReturnsAsync(
                Result.SuccessResult<IEnumerable<Route>>(new[]
                {
                    new Route { RouteName = "North", Date = DateTime.Today, IsActive = true, School = "Wiley" }
                }));
            _service = new OperationalReportService(new PdfReportService(), _students.Object, _routes.Object);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_dir))
                {
                    Directory.Delete(_dir, true);
                }
            }
            catch
            {
                // ignore temp cleanup
            }
        }

        [Test]
        public async Task GenerateAsync_StudentRoster_WritesPdfWithTwoRows()
        {
            var result = await _service.GenerateAsync(OperationalReportKind.StudentRoster, _dir);

            Assert.That(result.FileBytes.Length, Is.GreaterThan(100));
            Assert.That(result.FileBytes[0], Is.EqualTo((byte)'%'));
            Assert.That(File.Exists(result.FilePath), Is.True);
            Assert.That(result.Status, Does.Contain("2 row"));
            Assert.That(result.AiSummary, Does.Contain("unassigned"));
        }

        [Test]
        public async Task GenerateAsync_UnassignedStudents_OnlyIncludesStudentsWithoutRoutes()
        {
            var result = await _service.GenerateAsync(OperationalReportKind.UnassignedStudents, _dir);

            Assert.That(result.Status, Does.Contain("1 row"));
            Assert.That(result.FileBytes[0], Is.EqualTo((byte)'%'));
        }

        [Test]
        public async Task GenerateAsync_CsvExport_WritesCsvNotPdf()
        {
            var result = await _service.GenerateAsync(OperationalReportKind.CsvExport, _dir);

            Assert.That(result.FilePath, Does.EndWith(".csv"));
            var text = await File.ReadAllTextAsync(result.FilePath);
            Assert.That(text, Does.Contain("Ada Rider"));
            Assert.That(text, Does.StartWith("Name,"));
        }

        [Test]
        public async Task GenerateAsync_Request_WritesExactOutputPathAsCsv()
        {
            var path = Path.Combine(_dir, "cli-roster.csv");

            var result = await _service.GenerateAsync(new OperationalReportRequest
            {
                Kind = OperationalReportKind.StudentRoster,
                OutputFilePath = path,
                AsCsv = true
            });

            Assert.That(result.FilePath, Is.EqualTo(path));
            Assert.That(await File.ReadAllTextAsync(path), Does.Contain("Ben Rider"));
        }

        [Test]
        public void GenerateAsync_UnknownRouteId_Throws()
        {
            var request = new OperationalReportRequest
            {
                Kind = OperationalReportKind.RouteSummary,
                OutputDirectory = _dir,
                RouteId = 999
            };

            Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(() => _service.GenerateAsync(request)));
        }

        [Test]
        public async Task GenerateAsync_RouteSummaryWithoutRouteId_UsesAllRoutesTable()
        {
            _routes.Setup(r => r.GetAllActiveRoutesAsync()).ReturnsAsync(
                Result.SuccessResult<IEnumerable<Route>>(new[]
                {
                    new Route { RouteId = 1, RouteName = "North", Date = DateTime.Today, IsActive = true, School = "Wiley" },
                    new Route { RouteId = 2, RouteName = "South", Date = DateTime.Today, IsActive = true, School = "Wiley" }
                }));

            var result = await _service.GenerateAsync(OperationalReportKind.RouteSummary, _dir);

            Assert.That(result.Status, Does.Contain("2 row"));
            Assert.That(result.Status, Does.Not.Contain("route North"));
            Assert.That(result.FileBytes[0], Is.EqualTo((byte)'%'));
        }

        [Test]
        public async Task GenerateAsync_RouteSummaryWithRouteId_NamesThatRoute()
        {
            _routes.Setup(r => r.GetAllActiveRoutesAsync()).ReturnsAsync(
                Result.SuccessResult<IEnumerable<Route>>(new[]
                {
                    new Route { RouteId = 1, RouteName = "North", Date = DateTime.Today, IsActive = true, School = "Wiley" },
                    new Route { RouteId = 2, RouteName = "South", Date = DateTime.Today, IsActive = true, School = "Wiley" }
                }));

            var result = await _service.GenerateAsync(new OperationalReportRequest
            {
                Kind = OperationalReportKind.RouteSummary,
                OutputDirectory = _dir,
                RouteId = 2
            });

            Assert.That(result.Status, Does.Contain("route South"));
        }

        [Test]
        public async Task GenerateAsync_CsvFormat_RewritesPdfExtension()
        {
            var requested = Path.Combine(_dir, "roster.pdf");

            var result = await _service.GenerateAsync(new OperationalReportRequest
            {
                Kind = OperationalReportKind.StudentRoster,
                OutputFilePath = requested,
                AsCsv = true
            });

            Assert.That(result.FilePath, Does.EndWith(".csv"));
            Assert.That(File.Exists(result.FilePath), Is.True);
            Assert.That(File.Exists(requested), Is.False);
            Assert.That(await File.ReadAllTextAsync(result.FilePath), Does.StartWith("Name,"));
        }
    }

    [TestFixture]
    [Category("Core")]
    public class OperationalReportKindParserTests
    {
        [TestCase("Roster", OperationalReportKind.StudentRoster)]
        [TestCase("student-list", OperationalReportKind.StudentRoster)]
        [TestCase("RouteManifest", OperationalReportKind.RouteSummary)]
        [TestCase("DriverSchedule", OperationalReportKind.DriverRoster)]
        [TestCase("schedule", OperationalReportKind.DailySchedule)]
        [TestCase("UnassignedStudents", OperationalReportKind.UnassignedStudents)]
        public void TryParse_AliasesAndEnumNames_Succeed(string input, OperationalReportKind expected)
        {
            Assert.That(OperationalReportKindParser.TryParse(input, out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(expected));
        }

        [Test]
        public void TryParse_Unknown_Fails()
        {
            Assert.That(OperationalReportKindParser.TryParse("not-a-report", out _), Is.False);
        }

        [TestCase("csv", true)]
        [TestCase("Excel", true)]
        [TestCase("pdf", false)]
        public void IsCsvFormat_MatchesHelpAliases(string format, bool expected)
        {
            Assert.That(OperationalReportKindParser.IsCsvFormat(format), Is.EqualTo(expected));
        }
    }
}
