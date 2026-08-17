using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.RouteDetermination;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class PickupScheduleTests
{
    private static RoutingDistrictSettings Settings() => new()
    {
        AverageSpeedMph = 30,
        MaxPickupGapMinutes = 15
    };

    [Test]
    public void AmBackward_FromStartTime_IsMonotonicAndEndsBeforeStart()
    {
        var start = new TimeSpan(8, 0, 0);
        var stops = new List<(double, double)>
        {
            (38.20, -102.70),
            (38.18, -102.70),
            (38.16, -102.70)
        };
        var schoolLat = 38.15;
        var schoolLon = -102.70;

        var arrivals = PickupScheduleCalculator.ComputeAmPickupArrivals(
            stops, schoolLat, schoolLon, start, Settings(), out var underflow);

        Assert.That(underflow, Is.False);
        Assert.That(arrivals.Count, Is.EqualTo(3));
        Assert.That(PickupScheduleCalculator.IsMonotonicNonDecreasing(arrivals), Is.True);
        Assert.That(arrivals[^1], Is.LessThan(start));
        Assert.That(arrivals[0], Is.LessThanOrEqualTo(arrivals[1]));
    }

    [Test]
    public void PmForward_FromDismissal_IsMonotonicAndStartsAfterDismissal()
    {
        var dismissal = new TimeSpan(15, 30, 0);
        var stops = new List<(double, double)>
        {
            (38.16, -102.70),
            (38.18, -102.70),
            (38.20, -102.70)
        };

        var arrivals = PickupScheduleCalculator.ComputePmDropoffArrivals(
            stops, 38.15, -102.70, dismissal, Settings());

        Assert.That(arrivals.Count, Is.EqualTo(3));
        Assert.That(PickupScheduleCalculator.IsMonotonicNonDecreasing(arrivals), Is.True);
        Assert.That(arrivals[0], Is.GreaterThan(dismissal));
    }

    [Test]
    public void AmGeneration_EmptyStops_ReturnsEmpty()
    {
        var arrivals = PickupScheduleCalculator.ComputeAmPickupArrivals(
            Array.Empty<(double, double)>(), 38.15, -102.70, TimeSpan.FromHours(8), Settings(), out var underflow);
        Assert.That(arrivals, Is.Empty);
        Assert.That(underflow, Is.False);
    }

    [Test]
    public void AmBackward_ImpossibleTravel_SetsUnderflowAndClamps()
    {
        // Very early start with distant stops → underflow
        var start = new TimeSpan(0, 5, 0);
        var stops = new List<(double, double)>
        {
            (40.0, -104.0),
            (39.0, -103.0),
            (38.5, -102.8)
        };

        var arrivals = PickupScheduleCalculator.ComputeAmPickupArrivals(
            stops, 38.15, -102.70, start, Settings(), out var underflow);

        Assert.That(underflow, Is.True);
        Assert.That(arrivals.All(t => t >= TimeSpan.Zero), Is.True);
    }
}
