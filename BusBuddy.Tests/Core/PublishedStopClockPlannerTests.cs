using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class PublishedStopClockPlannerTests
{
    [Test]
    public void Apply_UsesPathTravel_NotDwellOnlyStaircase()
    {
        var stops = FourteenStops(dwellMinutes: 1);
        var plan = PublishedStopClockPlanner.Apply(
            stops,
            new TimeSpan(7, 30, 0),
            pathTravelMinutes: 60,
            stampUtc: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(plan.TravelSource, Is.EqualTo("PathDuration"));
        Assert.That(plan.TravelMinutes, Is.EqualTo(60));
        Assert.That(plan.FirstArrival, Is.EqualTo(new TimeSpan(7, 30, 0)));
        Assert.That(stops[0].ScheduledArrival, Is.EqualTo(new TimeSpan(7, 30, 0)));
        Assert.That(stops[0].ScheduledDeparture, Is.EqualTo(new TimeSpan(7, 31, 0)));

        // Dwell-only used to print 07:30–07:43 (13 minutes). Path is ~60 minutes of driving.
        Assert.That(plan.LastArrival, Is.EqualTo(new TimeSpan(8, 43, 0)));
        Assert.That(stops[^1].ScheduledArrival, Is.EqualTo(new TimeSpan(8, 43, 0)));
        Assert.That(stops[^1].ScheduledDeparture, Is.EqualTo(new TimeSpan(8, 44, 0)));
        Assert.That((plan.LastArrival - plan.FirstArrival).TotalMinutes, Is.EqualTo(73));
    }

    [Test]
    public void Apply_TwoStops_PutsEntirePathOnTheOneLeg()
    {
        var stops = new[]
        {
            Stop(1, 38.15m, -102.72m, 1),
            Stop(2, 38.09m, -102.62m, 1),
        };

        var plan = PublishedStopClockPlanner.Apply(stops, new TimeSpan(7, 30, 0), 60);

        Assert.That(stops[1].ScheduledArrival, Is.EqualTo(new TimeSpan(8, 31, 0)));
        Assert.That(plan.TravelMinutes, Is.EqualTo(60));
    }

    [Test]
    public void Apply_WithoutPathMinutes_UsesHaversineFallback()
    {
        var stops = new[]
        {
            Stop(1, 38.15m, -102.72m, 1),
            Stop(2, 38.09m, -102.62m, 1),
        };

        var plan = PublishedStopClockPlanner.Apply(stops, new TimeSpan(7, 30, 0), pathTravelMinutes: null);

        Assert.That(plan.TravelSource, Is.EqualTo("HaversineFallback"));
        Assert.That(plan.TravelMinutes, Is.GreaterThanOrEqualTo(10));
        Assert.That((stops[1].ScheduledArrival - stops[0].ScheduledDeparture).TotalMinutes,
            Is.EqualTo(plan.TravelMinutes));
    }

    [Test]
    public void AllocateSeconds_PreservesTotal()
    {
        var parts = PublishedStopClockPlanner.AllocateSeconds(new[] { 1.0, 2.0, 1.0 }, 3580);
        Assert.That(parts.Sum(), Is.EqualTo(3580));
        Assert.That(parts[1], Is.GreaterThan(parts[0]));
    }

    private static List<RouteStop> FourteenStops(int dwellMinutes)
    {
        var stops = new List<RouteStop>(14);
        for (var i = 0; i < 14; i++)
        {
            stops.Add(Stop(i + 1, 38.15m + (i * 0.01m), -102.72m + (i * 0.01m), dwellMinutes));
        }

        return stops;
    }

    private static RouteStop Stop(int order, decimal lat, decimal lon, int dwell) =>
        new()
        {
            RouteStopId = order,
            StopOrder = order,
            StopName = $"Stop {order}",
            Latitude = lat,
            Longitude = lon,
            StopDuration = dwell,
        };
}
