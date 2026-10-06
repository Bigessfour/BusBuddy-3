using System.Linq;
using BusBuddy.Core.Services.RouteDetermination;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class PublishedClockPlannerTests
{
    private static readonly TimeSpan Dwell = TimeSpan.FromMinutes(5);
    private static readonly int[] TenMinuteLegs = { 600, 600, 600, 600, 600, 600 };

    [Test]
    public void Morning_FourSchoolsSharedHomeAndSecondSchoolStop_WalksBackward()
    {
        // Four campuses, one home shared by schools 1 and 2, and a second stop for school 1
        // after every rider of that school is already aboard.
        var stops = new[]
        {
            Depot(),
            Pickup(1, 2),
            Pickup(3),
            Pickup(4),
            School(1, TimeSpan.FromHours(8)),
            School(1, TimeSpan.FromHours(8)),
            School(2, new TimeSpan(8, 10, 0)),
            School(3, new TimeSpan(8, 20, 0)),
            School(4, new TimeSpan(8, 30, 0)),
            Depot()
        };
        var legs = Enumerable.Repeat(600, stops.Length - 1).ToArray();

        var plan = PublishedClockPlanner.Plan(stops, legs, Dwell, afternoon: false, estimated: false);

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Warnings));
        Assert.That(plan.BeginTime, Is.EqualTo(new TimeSpan(6, 55, 0)));
        Assert.That(plan.Arrivals[4], Is.EqualTo(new TimeSpan(7, 50, 0)));
        Assert.That(plan.Arrivals[5], Is.EqualTo(TimeSpan.FromHours(8)));
        Assert.That(plan.Arrivals[8], Is.EqualTo(new TimeSpan(8, 30, 0)));
        Assert.That(plan.Arrivals, Does.Not.Contain(TimeSpan.Zero));
    }

    [Test]
    public void Morning_ThreeSchoolsOneSharedHome_WalksBackwardFromEachBell()
    {
        // Depot, shared home (schools 1 and 2), home (school 3), school 1, school 2, school 3, barn.
        // School 4 is the second visit of school 1's campus only when the order is legal; here each campus is once.
        var stops = new[]
        {
            Depot(),
            Pickup(1, 2),
            Pickup(3),
            School(1, TimeSpan.FromHours(8)),
            School(2, new TimeSpan(8, 5, 0)),
            School(3, new TimeSpan(8, 15, 0)),
            Depot()
        };

        var plan = PublishedClockPlanner.Plan(stops, TenMinuteLegs, Dwell, afternoon: false, estimated: false);

        Assert.That(plan.Success, Is.True, plan.Warnings.Count == 0 ? null : string.Join("; ", plan.Warnings));
        Assert.That(plan.BeginTime, Is.EqualTo(new TimeSpan(7, 15, 0)));
        Assert.That(plan.Arrivals, Is.EqualTo(new[]
        {
            new TimeSpan(7, 15, 0),
            new TimeSpan(7, 25, 0),
            new TimeSpan(7, 40, 0),
            new TimeSpan(7, 55, 0),
            new TimeSpan(8, 5, 0),
            new TimeSpan(8, 15, 0),
            new TimeSpan(8, 25, 0)
        }));
        Assert.That(plan.Departures[1], Is.EqualTo(new TimeSpan(7, 30, 0)));
        Assert.That(plan.Departures[3], Is.EqualTo(new TimeSpan(7, 55, 0)));
    }

    [Test]
    public void Morning_SharedHomeAndDuplicateSchool_FailsWhenSchoolIsVisitedEarly()
    {
        var stops = new[]
        {
            Depot(),
            School(1, TimeSpan.FromHours(8)),
            Pickup(1, 2),
            School(1, TimeSpan.FromHours(8)),
            School(2, new TimeSpan(8, 5, 0))
        };

        var plan = PublishedClockPlanner.Plan(
            stops,
            new[] { 600, 600, 600, 600 },
            Dwell,
            afternoon: false,
            estimated: false);

        Assert.That(plan.Success, Is.False);
        Assert.That(plan.Arrivals, Is.Empty);
        Assert.That(string.Join(" ", plan.Warnings), Does.Contain("before its riders are aboard"));
    }

    [Test]
    public void Morning_PickupAfterItsSchool_FailsWithoutMidnightClocks()
    {
        var stops = new[]
        {
            School(1, TimeSpan.FromHours(8)),
            Pickup(1)
        };

        var plan = PublishedClockPlanner.Plan(stops, new[] { 600 }, Dwell, afternoon: false, estimated: false);

        Assert.That(plan.Success, Is.False);
        Assert.That(plan.Arrivals, Is.Empty);
        Assert.That(plan.Arrivals, Does.Not.Contain(TimeSpan.Zero));
        Assert.That(string.Join(" ", plan.Warnings), Does.Contain("after"));
    }

    [Test]
    public void Morning_TravelExceedsBell_DoesNotPublishMidnight()
    {
        var stops = new[]
        {
            Pickup(1),
            School(1, TimeSpan.FromMinutes(5))
        };

        var plan = PublishedClockPlanner.Plan(
            stops,
            new[] { 3600 },
            Dwell,
            afternoon: false,
            estimated: false);

        Assert.That(plan.Success, Is.False);
        Assert.That(plan.Arrivals, Is.Empty);
        Assert.That(string.Join(" ", plan.Warnings), Does.Contain("exceeds"));
    }

    [Test]
    public void Afternoon_LongRun_StillPublishesAfterALaterBell()
    {
        var stops = new[]
        {
            Depot(),
            School(1, new TimeSpan(15, 30, 0)),
            Pickup(1),
            School(2, new TimeSpan(15, 35, 0))
        };

        var plan = PublishedClockPlanner.Plan(
            stops,
            new[] { 600, 600, 1200 },
            Dwell,
            afternoon: true,
            estimated: false);

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Warnings));
        Assert.That(plan.BeginTime, Is.EqualTo(new TimeSpan(15, 20, 0)));
        Assert.That(plan.Arrivals[3], Is.EqualTo(new TimeSpan(16, 5, 0)));
    }

    [Test]
    public void UnconfirmedBell_IsNotADeadline()
    {
        var stops = new[]
        {
            Pickup(1),
            School(1, bell: null)
        };

        var plan = PublishedClockPlanner.Plan(stops, new[] { 600 }, Dwell, afternoon: false, estimated: false);

        Assert.That(plan.Success, Is.False);
        Assert.That(plan.Arrivals, Is.Empty);
        Assert.That(string.Join(" ", plan.Warnings), Does.Contain("No confirmed school bell"));
    }

    private static ClockStop Depot() =>
        new(ClockStopKind.Depot, null, null, Array.Empty<int>());

    private static ClockStop Pickup(params int[] schoolIds) =>
        new(ClockStopKind.Pickup, null, null, schoolIds);

    private static ClockStop School(int id, TimeSpan? bell) =>
        new(ClockStopKind.School, id, bell, Array.Empty<int>());
}
