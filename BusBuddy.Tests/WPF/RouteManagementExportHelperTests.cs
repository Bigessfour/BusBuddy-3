using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Route;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteManagementExportHelperTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "busbuddy-export-tests", Guid.NewGuid().ToString("N"));
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

    [Test]
    public void WriteFallbackCsv_WritesHeaderAndEscapesQuotes()
    {
        var routes = new List<Route>
        {
            new()
            {
                RouteId = 7,
                RouteName = "North \"Loop\"",
                Date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                StudentCount = 12,
                StopCount = 4,
                School = "Wiley Elementary",
                BusNumber = "05",
            },
        };
        var path = Path.Combine(_tempDir, "routes.csv");

        RouteManagementExportHelper.WriteFallbackCsv(routes, path);

        var lines = File.ReadAllLines(path);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("RouteId,RouteName,Date,Active,StudentCount,StopCount,School,BusNumber");
        lines[1].Should().Be("7,\"North \"\"Loop\"\"\",2026-09-15,True,12,4,\"Wiley Elementary\",\"05\"");
    }

    [Test]
    public void WriteFallbackCsv_NullCountsAndStringsBecomeZeroAndEmpty()
    {
        var routes = new List<Route>
        {
            new() { RouteId = 1, RouteName = "Solo", Date = new DateTime(2026, 1, 2), IsActive = false },
        };
        var path = Path.Combine(_tempDir, "sparse.csv");

        RouteManagementExportHelper.WriteFallbackCsv(routes, path);

        File.ReadAllLines(path)[1].Should().Be("1,\"Solo\",2026-01-02,False,0,0,,");
    }

    [Test]
    public void WriteFallbackReport_OneLinePerRouteWithKeyFields()
    {
        var routes = new List<Route>
        {
            new() { RouteId = 3, RouteName = "East", School = "Lamar HS", BusNumber = "12", Date = new DateTime(2026, 3, 4), IsActive = true, StudentCount = 9, StopCount = 3 },
            new() { RouteId = 4, RouteName = "West", Date = new DateTime(2026, 3, 4), IsActive = true },
        };
        var path = Path.Combine(_tempDir, "report.txt");

        RouteManagementExportHelper.WriteFallbackReport(routes, path);

        var lines = File.ReadAllLines(path);
        lines[0].Should().StartWith("Route Summary Export ");
        lines.Should().Contain(l => l.StartsWith("[3] East | School:Lamar HS | Bus:12 | Date:2026-03-04 | Active:True | Students:9 | Stops:3", StringComparison.Ordinal));
        lines.Should().Contain(l => l.StartsWith("[4] West |", StringComparison.Ordinal) && l.EndsWith("Students:0 | Stops:0", StringComparison.Ordinal));
    }

    [Test]
    public void RevealOrOpen_MissingPathIsNoOp()
    {
        var act = () => RouteManagementExportHelper.RevealOrOpen(Path.Combine(_tempDir, "does-not-exist.pdf"));
        act.Should().NotThrow();

        var blank = () => RouteManagementExportHelper.RevealOrOpen("   ");
        blank.Should().NotThrow();
    }

    [Test]
    public async Task TryPersistScheduleAsync_NullServiceReturnsFalse()
    {
        var route = new Route { RouteId = 1, AMVehicleId = 2, AMDriverId = 3 };

        var persisted = await RouteManagementExportHelper.TryPersistScheduleAsync(route, null);

        persisted.Should().BeFalse();
    }

    [Test]
    public async Task TryPersistScheduleAsync_MissingBusOrDriverDoesNotWrite()
    {
        var scheduleService = new Mock<IScheduleService>(MockBehavior.Strict);
        var noBus = new Route { RouteId = 1, AMDriverId = 3 };
        var noDriver = new Route { RouteId = 1, AMVehicleId = 2 };

        (await RouteManagementExportHelper.TryPersistScheduleAsync(noBus, scheduleService.Object)).Should().BeFalse();
        (await RouteManagementExportHelper.TryPersistScheduleAsync(noDriver, scheduleService.Object)).Should().BeFalse();

        scheduleService.Verify(s => s.AddScheduleAsync(It.IsAny<Schedule>()), Times.Never);
    }

    [Test]
    public async Task TryPersistScheduleAsync_WritesUtcDayFromAmBeginTimeAndDuration()
    {
        Schedule? captured = null;
        var scheduleService = new Mock<IScheduleService>();
        scheduleService.Setup(s => s.AddScheduleAsync(It.IsAny<Schedule>()))
            .Callback<Schedule>(s => captured = s)
            .Returns(Task.CompletedTask);

        var route = new Route
        {
            RouteId = 6,
            RouteName = "Hop 5",
            School = "Wiley Elementary",
            AMVehicleId = 2,
            AMDriverId = 4,
            AMBeginTime = new TimeSpan(6, 45, 0),
            EstimatedDuration = 50,
        };

        var persisted = await RouteManagementExportHelper.TryPersistScheduleAsync(route, scheduleService.Object);

        persisted.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.RouteId.Should().Be(6);
        captured.BusId.Should().Be(2);
        captured.DriverId.Should().Be(4);
        captured.ScheduleDate.Kind.Should().Be(DateTimeKind.Utc);
        captured.ScheduleDate.TimeOfDay.Should().Be(TimeSpan.Zero);
        captured.DepartureTime.Should().Be(captured.ScheduleDate.Add(new TimeSpan(6, 45, 0)));
        captured.ArrivalTime.Should().Be(captured.DepartureTime.AddMinutes(50));
        captured.Location.Should().Be("Wiley Elementary");
        captured.Status.Should().Be("Scheduled");
        captured.Notes.Should().Contain("Hop 5");
    }

    [Test]
    public async Task TryPersistScheduleAsync_FallsBackToPmPairAndDefaultTimes()
    {
        Schedule? captured = null;
        var scheduleService = new Mock<IScheduleService>();
        scheduleService.Setup(s => s.AddScheduleAsync(It.IsAny<Schedule>()))
            .Callback<Schedule>(s => captured = s)
            .Returns(Task.CompletedTask);

        var route = new Route { RouteId = 9, PMVehicleId = 11, PMDriverId = 12 };

        (await RouteManagementExportHelper.TryPersistScheduleAsync(route, scheduleService.Object)).Should().BeTrue();

        captured!.BusId.Should().Be(11);
        captured.DriverId.Should().Be(12);
        captured.DepartureTime.Should().Be(captured.ScheduleDate.AddHours(7));
        captured.ArrivalTime.Should().Be(captured.DepartureTime.AddMinutes(45));
    }
}
