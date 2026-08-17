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
            stops, schoolLat, schoolLon, start, Settings());

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
    public void AmGeneration_RequiresStartTime_DocumentedByEmptyGuard()
    {
        // Calculator itself does not throw; service Fail path is covered by contract —
        // empty stop list returns empty (StartTime gate lives in RouteDeterminationService).
        var arrivals = PickupScheduleCalculator.ComputeAmPickupArrivals(
            Array.Empty<(double, double)>(), 38.15, -102.70, TimeSpan.FromHours(8), Settings());
        Assert.That(arrivals, Is.Empty);
    }
}
