using BusBuddy.Core.Configuration;
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
}
