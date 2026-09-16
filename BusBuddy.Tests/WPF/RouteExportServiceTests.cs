using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Services;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Route exports carry student names, so the clerk's chosen destination is the only place a file
/// may land — an extra copy on the Desktop is an unrequested roster.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteExportServiceTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "busbuddy-route-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private static RouteExportService CreateService()
    {
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route>
            {
                new()
                {
                    RouteId = 3,
                    RouteName = "Bus 5 AM",
                    School = "Wiley Elementary",
                    Description = "Special needs AM",
                    Date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                },
            }));

        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetAllStudentsAsync())
            .ReturnsAsync(new List<Student>
            {
                new() { StudentId = 11, StudentName = "Ada Rider", Grade = "3", AMRoute = "Bus 5 AM" },
            });

        return new RouteExportService(routes.Object, students.Object);
    }

    private static string[] DesktopFiles(string pattern) =>
        Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), pattern);

    [Test]
    public async Task ExportRoutesToCsvAsync_WritesToRequestedPathOnly()
    {
        var desktopBefore = DesktopFiles("BusBuddy_Routes_*.csv");
        var path = Path.Combine(_tempDir, "nested", "routes.csv");

        var written = await CreateService().ExportRoutesToCsvAsync(path);

        written.Should().Be(path);
        File.Exists(path).Should().BeTrue();
        File.ReadAllText(path).Should().Contain("Ada Rider");
        DesktopFiles("BusBuddy_Routes_*.csv").Should().BeEquivalentTo(
            desktopBefore,
            "the clerk chose a destination, so no roster copy belongs on the Desktop");
    }

    [Test]
    public async Task GenerateRouteReportAsync_WritesToRequestedPathOnly()
    {
        var desktopBefore = DesktopFiles("BusBuddy_Report_*.txt");
        var path = Path.Combine(_tempDir, "report.txt");

        var written = await CreateService().GenerateRouteReportAsync(path);

        written.Should().Be(path);
        File.ReadAllText(path).Should().Contain("Bus 5 AM");
        DesktopFiles("BusBuddy_Report_*.txt").Should().BeEquivalentTo(desktopBefore);
    }

    [Test]
    public async Task ExportRoutesToCsvAsync_MissingPath_Throws()
    {
        var act = () => CreateService().ExportRoutesToCsvAsync(" ");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("outputPath");
    }
}
