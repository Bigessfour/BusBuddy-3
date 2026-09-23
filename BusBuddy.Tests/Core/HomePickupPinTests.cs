using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class HomePickupPinTests
{
    [Test]
    public void ApplyOnSave_SameStreet_RestoresClerkPinWhenFormDroppedTheFlag()
    {
        var stored = Home("100 Main St", 38.0872m, -102.6208m, clerkAdjusted: true);
        var incoming = Home("100 Main St", 38.2000m, -102.8000m, clerkAdjusted: false);

        HomePickupPin.ApplyOnSave(incoming, stored);

        incoming.HomePickupClerkAdjusted.Should().BeTrue();
        incoming.Latitude.Should().Be(38.0872m);
        incoming.Longitude.Should().Be(-102.6208m);
    }

    [Test]
    public void ApplyOnSave_SameStreet_TrustsANewClerkClick()
    {
        var stored = Home("100 Main St", 38.0872m, -102.6208m, clerkAdjusted: true);
        var incoming = Home("100 main st", 38.0875m, -102.6210m, clerkAdjusted: true);

        HomePickupPin.ApplyOnSave(incoming, stored);

        incoming.Latitude.Should().Be(38.0875m);
        incoming.Longitude.Should().Be(-102.6210m);
    }

    [Test]
    public void ApplyOnSave_NewStreet_ClearsTheClerkPinSoGeocodeCanRun()
    {
        var stored = Home("100 Main St", 38.0872m, -102.6208m, clerkAdjusted: true);
        var incoming = Home("200 Oak Ave", 38.0872m, -102.6208m, clerkAdjusted: true);

        HomePickupPin.ApplyOnSave(incoming, stored);

        incoming.HomePickupClerkAdjusted.Should().BeFalse();
        incoming.Latitude.Should().BeNull();
        incoming.Longitude.Should().BeNull();
    }

    private static Student Home(string street, decimal? latitude, decimal? longitude, bool clerkAdjusted) =>
        new()
        {
            HomeAddress = street,
            City = "Wiley",
            State = "CO",
            Zip = "81092",
            Latitude = latitude,
            Longitude = longitude,
            HomePickupClerkAdjusted = clerkAdjusted,
        };
}
