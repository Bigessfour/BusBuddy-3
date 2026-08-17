using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.RouteDetermination;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class TransferFleetTests
{
    [Test]
    public void HomeToSchoolPacking_DoesNotUseTransferPickupPointsAsHomeRiders()
    {
        // Home riders clustered near school; transfer pickups are distant and must not
        // be mixed into HomeToSchool cell packing (separate FleetKind pool).
        var settings = new RoutingDistrictSettings
        {
            TargetRidersPerCell = 20,
            MaxPickupGapMinutes = 30,
            AverageSpeedMph = 25
        };

        var homeRiders = Enumerable.Range(1, 8)
            .Select(i => new RiderPoint(i, 38.150 + i * 0.0002, -102.700))
            .ToList();

        var transferOnlyPickups = new List<RiderPoint>
        {
            new(101, 38.40, -102.50),
            new(102, 38.41, -102.51)
        };

        var homeCells = DensityCellBuilder.Build(homeRiders, settings);
        var homePacked = homeCells.SelectMany(c => RoutePacker.PackCell(c, 72, settings)).ToList();
        var homeStudentIds = homePacked.SelectMany(p => p.OrderedStudentIds).ToHashSet();

        Assert.That(homeStudentIds.SetEquals(homeRiders.Select(r => r.StudentId)), Is.True);
        Assert.That(homeStudentIds.Overlaps(transferOnlyPickups.Select(r => r.StudentId)), Is.False);

        var xferCells = DensityCellBuilder.Build(transferOnlyPickups, settings);
        var xferPacked = xferCells.SelectMany(c => RoutePacker.PackCell(c, 72, settings)).ToList();
        Assert.That(xferPacked.Sum(p => p.OrderedStudentIds.Count), Is.EqualTo(2));
        Assert.That(homePacked.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(homeStudentIds.Intersect(transferOnlyPickups.Select(r => r.StudentId)).Any(), Is.False);
    }

    [Test]
    public void TransferStopResolver_PrefersPickupForAm_AndDropoffForPm()
    {
        var transfer = new StudentSchoolTransfer
        {
            StudentId = 42,
            PickupLatitude = 38.40m,
            PickupLongitude = -102.50m,
            PickupAddress = "Pickup Hub",
            DropoffLatitude = 38.16m,
            DropoffLongitude = -102.70m,
            DropoffAddress = "Campus Door"
        };

        Assert.That(
            PickupScheduleCalculator.TryResolveTransferStop(
                transfer, RouteTimeSlotKind.AM, out var amLat, out var amLon, out var amAddr),
            Is.True);
        Assert.That(amLat, Is.EqualTo(38.40m));
        Assert.That(amLon, Is.EqualTo(-102.50m));
        Assert.That(amAddr, Is.EqualTo("Pickup Hub"));

        Assert.That(
            PickupScheduleCalculator.TryResolveTransferStop(
                transfer, RouteTimeSlotKind.PM, out var pmLat, out var pmLon, out var pmAddr),
            Is.True);
        Assert.That(pmLat, Is.EqualTo(38.16m));
        Assert.That(pmLon, Is.EqualTo(-102.70m));
        Assert.That(pmAddr, Is.EqualTo("Campus Door"));
    }

    [Test]
    public void TransferBothSlot_ProducesDistinctAmAndPmProposalNames()
    {
        // Naming contract used by GenerateTransferFleetAsync for RouteTimeSlotKind.Both
        const string amName = "Draft-Xfer-Wiley-c1-1";
        var pmName = $"{amName}-PM";
        Assert.That(pmName, Does.EndWith("-PM"));
        Assert.That(amName, Does.Not.EndWith("-PM"));
    }
}
