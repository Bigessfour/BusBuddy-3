using BusBuddy.Core.Models;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class StudentSpecialNeedsHelperTests
{
    [Test]
    public void RequiresSpecialNeedsTransport_UsesBoolAndLegacyText()
    {
        var student = new Student();
        Assert.That(StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student), Is.False);

        student.RequiresSpecialNeedsBus = true;
        Assert.That(StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student), Is.True);

        student.RequiresSpecialNeedsBus = false;
        student.SpecialNeeds = "IEP transport";
        Assert.That(StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student), Is.True);
    }

    [Test]
    public void IsSpecialNeedsRoute_UsesFlagAndName()
    {
        var route = new Route { RouteName = "North Elementary" };
        Assert.That(StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route), Is.False);

        route.IsSpecialNeedsRoute = true;
        Assert.That(StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route), Is.True);

        route.IsSpecialNeedsRoute = false;
        route.RouteName = "Special Needs Route 2";
        Assert.That(StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route), Is.True);

        Assert.That(
            StudentSpecialNeedsHelper.IsSpecialNeedsRoute("Special Needs Route", isSpecialNeedsRoute: false),
            Is.True);
        Assert.That(
            StudentSpecialNeedsHelper.IsSpecialNeedsRoute("North Elementary", isSpecialNeedsRoute: false),
            Is.False);
    }

    /// <summary>
    /// The district's canonical Bus 5 route name, as spelled in the clerk's database. Both
    /// name-based matchers have to recognise it, or a special-needs run is treated as an ordinary
    /// route and the AM/PM session is mislabelled.
    /// </summary>
    [Test]
    [TestCase("AM Special Needs Bus 5")]
    [TestCase("AM Bus 5 Special Needs")]
    public void CanonicalSpecialNeedsRouteName_IsRecognisedByBothMatchers(string routeName)
    {
        Assert.That(
            StudentSpecialNeedsHelper.IsSpecialNeedsRoute(routeName, isSpecialNeedsRoute: false),
            Is.True);
        Assert.That(
            RouteSession.Infer(routeName, isSpecialNeedsRoute: false, description: null),
            Is.EqualTo(RouteSession.SpecialNeeds));
    }

    [Test]
    public void SyncLegacySpecialNeedsText_SetsAndClearsSentinel()
    {
        var student = new Student();
        student.RequiresSpecialNeedsBus = true;
        StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(student);
        Assert.That(student.SpecialNeeds, Is.EqualTo(StudentSpecialNeedsHelper.LegacyTransportFlag));

        student.RequiresSpecialNeedsBus = false;
        StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(student);
        Assert.That(student.SpecialNeeds, Is.Empty);
    }
}
