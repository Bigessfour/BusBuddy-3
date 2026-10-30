using BusBuddy.Core.Services.Trips;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class TripCalendarDisplayTests
{
    private static readonly DateTime Today = new(2026, 10, 6);

    [Test]
    public void Subject_ShowsDateLocationAndDriver()
    {
        var subject = TripCalendarDisplay.Subject(
            new DateTime(2026, 10, 8),
            "La Junta Jr/Sr HS",
            "Rod Jagers");

        Assert.That(subject, Is.EqualTo("10/8 · La Junta Jr/Sr HS · Rod Jagers"));
    }

    [Test]
    public void Subject_UsesPlaceholdersWhenPlaceOrDriverMissing()
    {
        var subject = TripCalendarDisplay.Subject(new DateTime(2026, 11, 6), null, " ");

        Assert.That(subject, Is.EqualTo("11/6 · Location TBD · No driver"));
    }

    [Test]
    public void Resolve_AssignedDriverIsScheduled()
    {
        var tone = TripCalendarDisplay.Resolve(Today.AddDays(1), hasDriver: true, Today);

        Assert.That(tone, Is.EqualTo(TripCalendarDisplay.Tone.Scheduled));
    }

    [Test]
    public void Resolve_NoDriverInsideSevenDaysIsRed()
    {
        Assert.That(
            TripCalendarDisplay.Resolve(Today, hasDriver: false, Today),
            Is.EqualTo(TripCalendarDisplay.Tone.DriverDueWithinOneWeek));
        Assert.That(
            TripCalendarDisplay.Resolve(Today.AddDays(7), hasDriver: false, Today),
            Is.EqualTo(TripCalendarDisplay.Tone.DriverDueWithinOneWeek));
        Assert.That(
            TripCalendarDisplay.Resolve(Today.AddDays(-1), hasDriver: false, Today),
            Is.EqualTo(TripCalendarDisplay.Tone.DriverDueWithinOneWeek));
    }

    [Test]
    public void Resolve_NoDriverBetweenEightAndFourteenDaysIsYellow()
    {
        Assert.That(
            TripCalendarDisplay.Resolve(Today.AddDays(8), hasDriver: false, Today),
            Is.EqualTo(TripCalendarDisplay.Tone.DriverDueWithinTwoWeeks));
        Assert.That(
            TripCalendarDisplay.Resolve(Today.AddDays(14), hasDriver: false, Today),
            Is.EqualTo(TripCalendarDisplay.Tone.DriverDueWithinTwoWeeks));
    }

    [Test]
    public void Resolve_NoDriverBeyondTwoWeeksStaysScheduled()
    {
        var tone = TripCalendarDisplay.Resolve(Today.AddDays(15), hasDriver: false, Today);

        Assert.That(tone, Is.EqualTo(TripCalendarDisplay.Tone.Scheduled));
    }
}
