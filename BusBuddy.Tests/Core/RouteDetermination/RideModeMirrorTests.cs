using BusBuddy.Core.Models;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class RideModeMirrorTests
{
    [Test]
    public void AmOnly_RetainsStopOnPmMirror()
    {
        var mode = StudentRideModeHelper.FromRouteNames("Draft-School-R0C0-1", null);
        Assert.That(mode, Is.EqualTo(StudentRideMode.AM));
        Assert.That(StudentRideModeHelper.RetainStopOnPmMirror(mode), Is.True);
        Assert.That(StudentRideModeHelper.RetainStopOnAmMirror(mode), Is.False);
    }

    [Test]
    public void PmOnly_RetainsStopOnAmMirror()
    {
        var mode = StudentRideModeHelper.FromRouteNames(null, "Draft-School-R0C0-1-PM");
        Assert.That(mode, Is.EqualTo(StudentRideMode.PM));
        Assert.That(StudentRideModeHelper.RetainStopOnAmMirror(mode), Is.True);
    }

    [Test]
    public void Both_RetainsOnBothMirrors()
    {
        var mode = StudentRideModeHelper.FromRouteNames("AM-1", "PM-1");
        Assert.That(mode, Is.EqualTo(StudentRideMode.Both));
        Assert.That(StudentRideModeHelper.RetainStopOnPmMirror(mode), Is.True);
        Assert.That(StudentRideModeHelper.RetainStopOnAmMirror(mode), Is.True);
    }
}
