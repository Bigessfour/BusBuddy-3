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
        var mode = StudentRideModeHelper.FromFlags(ridesAm: true, ridesPm: false);
        Assert.That(mode, Is.EqualTo(StudentRideMode.AM));
        Assert.That(StudentRideModeHelper.RetainStopOnPmMirror(mode), Is.True);
        Assert.That(StudentRideModeHelper.RetainStopOnAmMirror(mode), Is.False);
    }

    [Test]
    public void PmOnly_RetainsStopOnAmMirror()
    {
        var mode = StudentRideModeHelper.FromFlags(ridesAm: false, ridesPm: true);
        Assert.That(mode, Is.EqualTo(StudentRideMode.PM));
        Assert.That(StudentRideModeHelper.RetainStopOnAmMirror(mode), Is.True);
    }

    [Test]
    public void Both_RetainsOnBothMirrors()
    {
        var mode = StudentRideModeHelper.FromFlags(ridesAm: true, ridesPm: true);
        Assert.That(mode, Is.EqualTo(StudentRideMode.Both));
        Assert.That(StudentRideModeHelper.RetainStopOnPmMirror(mode), Is.True);
        Assert.That(StudentRideModeHelper.RetainStopOnAmMirror(mode), Is.True);
    }

    [Test]
    public void ShouldAssignPmMirror_SkipsAmOnly_AllowsNeitherAndBoth()
    {
        Assert.That(StudentRideModeHelper.ShouldAssignPmMirror(StudentRideMode.AM), Is.False);
        Assert.That(StudentRideModeHelper.ShouldAssignPmMirror(StudentRideMode.Neither), Is.True);
        Assert.That(StudentRideModeHelper.ShouldAssignPmMirror(StudentRideMode.Both), Is.True);
        Assert.That(StudentRideModeHelper.ShouldAssignPmMirror(StudentRideMode.PM), Is.True);
    }
}
