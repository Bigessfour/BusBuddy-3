using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class RouteSummaryDocumentTests
{
    [Test]
    public void From_OrdersByClockTime_AndNumbersSequenceNotStopOrder()
    {
        var route = new Route
        {
            RouteName = "Draft-Hop1_Proof_School_20260909224028-R0C0-1",
            School = "Wiley School",
            AMBeginTime = new TimeSpan(7, 0, 0),
            EstimatedDuration = 361,
            Session = RouteSession.AM
        };
        var stops = new[]
        {
            Stop(1, "TEST_HOP2_STUDENT", new TimeSpan(7, 59, 0), new TimeSpan(7, 59, 0)),
            Stop(50, "WallClockProof", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0)),
            Stop(51, "WallClockProof2", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0)),
        };

        var doc = RouteSummaryDocument.From(route, stops, Array.Empty<Student>(), bus: null, driver: null, RouteTimeSlot.AM);

        Assert.That(doc.DisplayName, Is.EqualTo("Wiley School"));
        Assert.That(doc.Stops.Select(s => s.Sequence).ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(doc.Stops[0].Name, Is.EqualTo("WallClockProof"));
        Assert.That(doc.Stops[2].Name, Is.EqualTo("TEST_HOP2_STUDENT"));
        Assert.That(doc.DepartureText, Is.EqualTo("7:00 AM"));
        Assert.That(doc.ArrivalText, Is.EqualTo("7:59 AM"));
        Assert.That(doc.ArrivalText, Does.Not.Contain("1:01"));
        Assert.That(doc.Stops.All(s => s.Miles == "—" && s.Cumulative == "—"), Is.True);
        Assert.That(doc.TotalMilesText, Is.EqualTo("—"));
        Assert.That(doc.Students, Is.Empty);
        Assert.That(doc.BusLabel, Is.EqualTo("Unassigned"));
    }

    [Test]
    public void From_ComputesLegMiles_WhenStopsHaveValidatedCoordinates()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School" };
        var a = Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0));
        a.Latitude = 38.0872m;
        a.Longitude = -102.6208m;
        var b = Stop(2, "School", new TimeSpan(7, 20, 0), new TimeSpan(7, 20, 0));
        b.Latitude = 38.1000m;
        b.Longitude = -102.6100m;

        var doc = RouteSummaryDocument.From(route, new[] { a, b }, Array.Empty<Student>(), null, null, RouteTimeSlot.AM);

        Assert.That(doc.Stops[0].Miles, Is.EqualTo("—"));
        Assert.That(double.Parse(doc.Stops[1].Miles), Is.GreaterThan(0.5).And.LessThan(5));
        Assert.That(doc.TotalMilesText, Is.EqualTo(doc.Stops[1].Cumulative));
    }

    [Test]
    public void Render_WritesPdfHeader()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School", Date = new DateTime(2026, 9, 17) };
        var bytes = RouteSummaryPdfRenderer.Render(
            route,
            new[] { Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0)) },
            new[] { new Student { StudentName = "Ada Clark", Grade = "3", HomeAddress = "100 Main" } },
            new Bus { BusNumber = "5" },
            new Driver { DriverName = "Robert Truitt" },
            RouteTimeSlot.AM);

        Assert.That(bytes.Length, Is.GreaterThan(200));
        Assert.That(bytes[0], Is.EqualTo((byte)'%'));
        Assert.That(bytes[1], Is.EqualTo((byte)'P'));
        Assert.That(bytes[2], Is.EqualTo((byte)'D'));
        Assert.That(bytes[3], Is.EqualTo((byte)'F'));
    }

    private static RouteStop Stop(int order, string name, TimeSpan arr, TimeSpan dep) =>
        new()
        {
            StopOrder = order,
            StopName = name,
            ScheduledArrival = arr,
            ScheduledDeparture = dep,
            Status = "Active"
        };
}
