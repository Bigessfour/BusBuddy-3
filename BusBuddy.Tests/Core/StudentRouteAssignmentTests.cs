using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class StudentRouteAssignmentTests
{
    [Test]
    public void Matches_UsesKeyEvenWhenNamesCollide()
    {
        var routeA = new Route { RouteId = 1, RouteName = "North" };
        var routeB = new Route { RouteId = 2, RouteName = "North" };
        var student = new Student { AMRoute = "North", AmRouteId = 1 };

        Assert.That(StudentRouteAssignment.Matches(student, routeA, RouteTimeSlot.AM), Is.True);
        Assert.That(StudentRouteAssignment.Matches(student, routeB, RouteTimeSlot.AM), Is.False);
    }

    [Test]
    public void IsUnassigned_FalseWhenOnlyKeyIsSet()
    {
        var student = new Student { AmRouteId = 9, AMRoute = null };
        Assert.That(StudentRouteAssignment.IsUnassignedAm(student), Is.False);
    }

    [Test]
    public void SetSlot_WritesKeyAndNameTogether()
    {
        var student = new Student();
        var route = new Route { RouteId = 4, RouteName = "East AM" };
        StudentRouteAssignment.SetSlot(student, RouteTimeSlot.AM, route);
        Assert.That(student.AMRoute, Is.EqualTo("East AM"));
        Assert.That(student.AmRouteId, Is.EqualTo(4));
        StudentRouteAssignment.SetSlot(student, RouteTimeSlot.AM, route: null);
        Assert.That(student.AMRoute, Is.Null);
        Assert.That(student.AmRouteId, Is.Null);
    }

    [Test]
    public void MatchesEither_TrueWhenOnlyPmKeyMatches()
    {
        var route = new Route { RouteId = 8, RouteName = "West" };
        var student = new Student { PmRouteId = 8, PMRoute = "West" };
        Assert.That(StudentRouteAssignment.MatchesEither(student, route), Is.True);
        Assert.That(StudentRouteAssignment.Matches(student, route, RouteTimeSlot.AM), Is.False);
    }
}
