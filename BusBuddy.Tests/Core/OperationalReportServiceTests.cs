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
    }
}
