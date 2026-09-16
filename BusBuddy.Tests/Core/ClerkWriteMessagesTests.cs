using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class ClerkWriteMessagesTests
{
    [Test]
    public void Retired_WithBlockers_JoinsCountsIntoOneSentence()
    {
        ClerkWriteMessages.Retired(
                "Route 5 AM",
                (2, "schedule", "schedules"),
                (0, "student assignment", "student assignments"),
                (1, "trip event", "trip events"))
            .Should()
            .Be("Route 5 AM retired; 2 schedules, 1 trip event still reference it.");
    }

    [Test]
    public void Retired_WithoutBlockers_IsAPlainSentence()
    {
        ClerkWriteMessages.Retired("Bus 5").Should().Be("Bus 5 retired.");
    }

    [Test]
    public void Deleted_And_NotFound_AreClerkSentences()
    {
        ClerkWriteMessages.Deleted("Fuel record 9").Should().Be("Fuel record 9 deleted.");
        ClerkWriteMessages.NotFound("Driver 3").Should().Be("Driver 3 was not found.");
    }

    [Test]
    public void FleetLabel_MarksRetiredBusesOnFuelAndMaintenanceLists()
    {
        var active = new Bus { BusNumber = "5", Status = "Active" };
        var retired = new Bus { BusNumber = "5", Status = "Retired" };

        active.FleetLabel.Should().Be("5");
        retired.FleetLabel.Should().Be("5 (retired)");
        retired.IsRetired.Should().BeTrue();
    }
}
