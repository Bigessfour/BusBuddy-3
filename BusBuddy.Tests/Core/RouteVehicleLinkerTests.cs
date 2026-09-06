using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class RouteVehicleLinkerTests
{
    [Test]
    public void Apply_BothSlots_SetsVehicleIdsAndBusNumber()
    {
        var route = new Route { RouteName = "Special Needs Route" };
        var bus = new Bus { BusId = 5, BusNumber = "Bus-5", Status = "Active" };

        RouteVehicleLinker.Apply(route, bus, RouteTimeSlot.Both);

        Assert.That(route.AMVehicleId, Is.EqualTo(5));
        Assert.That(route.PMVehicleId, Is.EqualTo(5));
        Assert.That(route.BusNumber, Is.EqualTo("Bus-5"));
    }

    [Test]
    public void TrySyncFromBusNumber_MatchesCatalogIgnoringCase()
    {
        var route = new Route { RouteName = "Special Needs Route", BusNumber = "bus-5" };
        var buses = new[]
        {
            new Bus { BusId = 5, BusNumber = "Bus-5", Status = "Active" }
        };

        Assert.That(RouteVehicleLinker.TrySyncFromBusNumber(route, buses), Is.True);
        Assert.That(route.AMVehicleId, Is.EqualTo(5));
        Assert.That(route.BusNumber, Is.EqualTo("Bus-5"));
    }

    [Test]
    public void TrySyncFromBusNumber_DoesNotCollapseSplitAmPmAssignments()
    {
        var route = new Route
        {
            RouteName = "Special Needs Route",
            BusNumber = "Bus-5",
            AMVehicleId = 5,
            PMVehicleId = 9
        };
        var buses = new[]
        {
            new Bus { BusId = 5, BusNumber = "Bus-5", Status = "Active" },
            new Bus { BusId = 9, BusNumber = "Bus-9", Status = "Active" }
        };

        Assert.That(RouteVehicleLinker.TrySyncFromBusNumber(route, buses), Is.False);
        Assert.That(route.AMVehicleId, Is.EqualTo(5));
        Assert.That(route.PMVehicleId, Is.EqualTo(9));
    }

    [Test]
    public void IsAssignableStatus_AllowsActiveAndInService()
    {
        Assert.That(RouteVehicleLinker.IsAssignableStatus("Active"), Is.True);
        Assert.That(RouteVehicleLinker.IsAssignableStatus("InService"), Is.True);
        Assert.That(RouteVehicleLinker.IsAssignableStatus("Maintenance"), Is.False);
    }
}
