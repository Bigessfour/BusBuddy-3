using BusBuddy.Core.Services.Trips;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class TripTicketNumberTests
{
    [Test]
    public void Next_IsOnePastTheHighestNumericTicket()
    {
        var next = TripTicketNumbers.Next(
        [
            "319098868",
            "319103986",
            "319103486-Sat-24Oct",
            "playoff"
        ]);

        Assert.That(next, Is.EqualTo("319103987"));
    }

    [Test]
    public void Next_StartsAtOneWhenNoTicketHasANumber()
    {
        Assert.That(TripTicketNumbers.Next([null, "", "playoff"]), Is.EqualTo("1"));
    }
}
