using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class RouteSummarySheetBuilderTests
{
    [Test]
    public void Build_UsesStopClocks_NotEstimatedUtcOrDuration()
    {
        var route = new Route
        {
            RouteName = "Draft-Hop1_Proof_School_20260909224028-R0C0-1",
            School = "Wiley School",
            Date = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            Session = RouteSession.AM,
            AMBeginTime = new TimeSpan(7, 0, 0),
            EstimatedDuration = 361
        };
        var stops = new[]
        {
            Stop(1, "TEST_HOP2_STUDENT", new TimeSpan(7, 59, 0), new TimeSpan(7, 59, 0), "StudentId=2"),
            Stop(50, "WallClockProof", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0), "StudentId=9"),
            Stop(51, "WallClockProof2", new TimeSpan(7, 30, 0), new TimeSpan(7, 31, 0), notes: null),
        };
        stops[0].EstimatedArrivalTime = new DateTime(2026, 9, 17, 13, 59, 0, DateTimeKind.Utc);
        stops[0].EstimatedDepartureTime = new DateTime(2026, 9, 17, 13, 59, 0, DateTimeKind.Utc);

        var sheet = RouteSummarySheetBuilder.Build(
            route, stops, Array.Empty<Student>(), bus: null, driver: null, RouteTimeSlot.AM);

        Assert.That(sheet.DepartureText, Is.EqualTo("07:00"));
        Assert.That(sheet.ArrivalText, Is.EqualTo("07:59"));
        Assert.That(sheet.ArrivalText, Does.Not.Contain("13:01"));
        Assert.That(sheet.Stops.Select(s => s.Sequence).ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(sheet.Stops.Select(s => s.Name).ToArray(), Is.EqualTo(new[]
        {
            "WallClockProof", "WallClockProof2", "TEST_HOP2_STUDENT"
        }));
        Assert.That(sheet.Stops.All(s => s.Miles == "—" && s.Cumulative == "—"), Is.True);
        Assert.That(sheet.TotalMilesText, Is.EqualTo("—"));
        Assert.That(sheet.DriveTimeText, Is.EqualTo("361 min"));
        Assert.That(sheet.DepartureText, Is.EqualTo("07:00"));
        Assert.That(sheet.RosterCount, Is.EqualTo(0));
        Assert.That(sheet.GenerateStopsOnlyNote, Is.EqualTo(RouteSummarySheetBuilder.GenerateStopsOnlyMessage));
        Assert.That(sheet.DisplayName, Does.Contain("Wiley School"));
        Assert.That(sheet.FullRouteName, Does.Contain("Draft-Hop1"));
        Assert.That(sheet.Title, Is.EqualTo("Wiley School AM Route Sheet"));
        Assert.That(sheet.ServiceDate, Is.EqualTo("2026-09-17"));
        Assert.That(sheet.BusLabel, Is.EqualTo("Unassigned"));
    }

    [Test]
    public void Build_ComputesLegMiles_WhenStopsHaveValidatedCoordinates()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School" };
        var a = Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0));
        a.Latitude = 38.0872m;
        a.Longitude = -102.6208m;
        var b = Stop(2, "School", new TimeSpan(7, 20, 0), new TimeSpan(7, 20, 0));
        b.Latitude = 38.1000m;
        b.Longitude = -102.6100m;

        var sheet = RouteSummarySheetBuilder.Build(route, new[] { a, b }, Array.Empty<Student>(), null, null, RouteTimeSlot.AM);

        Assert.That(sheet.Stops[0].Miles, Is.EqualTo("—"));
        Assert.That(double.Parse(sheet.Stops[1].Miles), Is.GreaterThan(0.5).And.LessThan(5));
        Assert.That(sheet.TotalMilesText, Is.EqualTo(sheet.Stops[1].Cumulative));
    }

    [Test]
    public void Build_PrefersRouteDistance_ForHeaderMiles()
    {
        var route = new Route { RouteName = "Town AM", Distance = 12.4m, EstimatedDuration = 36 };
        var a = Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0));
        a.Latitude = 38.0872m;
        a.Longitude = -102.6208m;
        var b = Stop(2, "School", new TimeSpan(7, 20, 0), new TimeSpan(7, 20, 0));
        b.Latitude = 38.1000m;
        b.Longitude = -102.6100m;

        var sheet = RouteSummarySheetBuilder.Build(route, new[] { a, b }, Array.Empty<Student>(), null, null, RouteTimeSlot.AM);

        Assert.That(sheet.TotalMilesText, Is.EqualTo("12.4"));
        Assert.That(sheet.DriveTimeText, Is.EqualTo("36 min"));
        Assert.That(sheet.Stops.All(s => s.Miles == "—" && s.Cumulative == "—"), Is.True);
        Assert.That(sheet.DepartureText, Is.EqualTo("07:00"));
        Assert.That(sheet.ArrivalText, Is.EqualTo("07:20"));
    }

    [Test]
    public void Build_AssignedRoster_IncludesGradeAndStop()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School", Session = RouteSession.AM };
        var stop = Stop(1, "100 Main", new TimeSpan(7, 10, 0), new TimeSpan(7, 11, 0));
        var student = new Student
        {
            StudentName = "Ada Clark",
            Grade = "3",
            HomeAddress = "100 Main",
            AmRouteId = 1
        };

        var sheet = RouteSummarySheetBuilder.Build(
            route,
            new[] { stop },
            new[] { student },
            new Bus { BusNumber = "5" },
            new Driver { DriverName = "Robert Truitt" },
            RouteTimeSlot.AM);

        Assert.That(sheet.RosterCount, Is.EqualTo(1));
        Assert.That(sheet.GenerateStopsOnlyNote, Is.Null);
        Assert.That(sheet.Students[0].Name, Is.EqualTo("Ada Clark"));
        Assert.That(sheet.Students[0].Grade, Is.EqualTo("3"));
        Assert.That(sheet.Students[0].Stop, Is.EqualTo("Ada Clark"));
        Assert.That(sheet.Stops[0].Name, Is.EqualTo("Ada Clark"));
        Assert.That(sheet.Stops[0].Riders, Is.EqualTo("1"));
        Assert.That(sheet.BusLabel, Is.EqualTo("Bus 5"));
        Assert.That(sheet.DriverLabel, Is.EqualTo("Robert Truitt"));
    }

    [Test]
    public void Build_HomePickupStop_UsesStudentNameAndRiderCount()
    {
        var route = new Route
        {
            RouteName = "AM Special Needs Bus 5",
            School = "Lamar High School",
            Session = RouteSession.SpecialNeeds
        };
        var stop = new RouteStop
        {
            StopOrder = 1,
            StopName = "Home pickup (household)",
            StopAddress = "312 S 4th St, Lamar, CO",
            ScheduledArrival = new TimeSpan(7, 30, 0),
            ScheduledDeparture = new TimeSpan(7, 31, 0),
            Status = "Active"
        };
        var student = new Student
        {
            StudentId = 21,
            StudentName = "Azariah Gonzales",
            Grade = "7",
            HomeAddress = "312 S 4th St"
        };

        var sheet = RouteSummarySheetBuilder.Build(
            route, new[] { stop }, new[] { student }, null, null, RouteTimeSlot.AM);

        Assert.That(sheet.Title, Is.EqualTo("Lamar High School Special Needs Route Sheet"));
        Assert.That(sheet.SessionLabel, Is.EqualTo("Special Needs"));
        Assert.That(sheet.Stops[0].Name, Is.EqualTo("Azariah Gonzales"));
        Assert.That(sheet.Stops[0].Address, Does.Contain("312 S 4th St"));
        Assert.That(sheet.Stops[0].Riders, Is.EqualTo("1"));
        Assert.That(sheet.Students[0].Stop, Is.EqualTo("Azariah Gonzales"));
    }

    [Test]
    public void Build_OmitsStudentStopThatIsNotOnTheRoster()
    {
        var route = new Route
        {
            RouteName = "Special Needs Route",
            School = "Wiley K-12 School",
            Session = RouteSession.SpecialNeeds
        };
        var barn = Stop(1, "District Bus Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0));
        var orphan = Stop(2, "TEST_STUDENT_SN_01", new TimeSpan(7, 10, 0), new TimeSpan(7, 11, 0));
        orphan.StopAddress = "100 Test St";
        var school = Stop(3, "Wiley K-12 School", new TimeSpan(7, 40, 0), new TimeSpan(7, 41, 0));
        var rider = new Student
        {
            StudentId = 21,
            StudentName = "Assigned Rider",
            HomeAddress = "710 S 4th Street"
        };

        var sheet = RouteSummarySheetBuilder.Build(
            route,
            new[] { barn, orphan, school },
            new[] { rider },
            null,
            null,
            RouteTimeSlot.AM);

        Assert.That(sheet.Stops.Select(s => s.Name), Is.EqualTo(new[] { "District Bus Barn", "Wiley K-12 School" }));
        Assert.That(sheet.Students, Has.Count.EqualTo(1));
    }

    [Test]
    public void Build_UsesRouteSession_ForPmRow()
    {
        var route = new Route
        {
            RouteName = "Town-PM",
            Session = RouteSession.PM,
            School = "Wiley School"
        };

        var sheet = RouteSummarySheetBuilder.Build(
            route, Array.Empty<RouteStop>(), Array.Empty<Student>(), null, null, RouteTimeSlot.AM);

        Assert.That(sheet.SessionLabel, Is.EqualTo(RouteSession.PM));
        Assert.That(sheet.Title, Does.Contain("PM Route Sheet"));
    }

    private static RouteStop Stop(int order, string name, TimeSpan arr, TimeSpan dep, string? notes = null) =>
        new()
        {
            StopOrder = order,
            StopName = name,
            StopAddress = name.Contains("Main", StringComparison.Ordinal) ? "100 Main St" : string.Empty,
            ScheduledArrival = arr,
            ScheduledDeparture = dep,
            Notes = notes,
            Status = "Active"
        };
}
