using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.RouteDetermination;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class RoutePackingTests
{
    [Test]
    public void Pack_TwelveNearbyUnderSeating_SingleRoute()
    {
        var settings = new RoutingDistrictSettings
        {
            TargetRidersPerCell = 20,
            MaxPickupGapMinutes = 30,
            AverageSpeedMph = 25
        };

        var riders = new List<RiderPoint>();
        for (var i = 0; i < 12; i++)
        {
            // ~0.2 mile spacing — well under gap threshold
            riders.Add(new RiderPoint(i + 1, 38.150 + i * 0.0003, -102.700));
        }

        var cells = DensityCellBuilder.Build(riders, settings);
        Assert.That(cells.Count, Is.EqualTo(1));

        var packed = RoutePacker.PackCell(cells[0], seatingCapacity: 72, settings);
        Assert.That(packed.Count, Is.EqualTo(1), "SC-001: 12 nearby riders pack into one route");
        Assert.That(packed[0].OrderedStudentIds.Count, Is.EqualTo(12));
    }

    [Test]
    public void Pack_OutlierGap_ForcesSplit()
    {
        var settings = new RoutingDistrictSettings
        {
            TargetRidersPerCell = 50,
            MaxPickupGapMinutes = 5,
            AverageSpeedMph = 25
        };

        // Cluster A near 38.15, outlier ~10+ miles north → long gap minutes
        var riders = new List<RiderPoint>
        {
            new(1, 38.150, -102.700),
            new(2, 38.151, -102.701),
            new(3, 38.152, -102.700),
            new(4, 38.300, -102.700) // distant outlier
        };

        var cells = DensityCellBuilder.Build(riders, settings);
        var packed = cells.SelectMany(c => RoutePacker.PackCell(c, seatingCapacity: 72, settings)).ToList();

        Assert.That(packed.Count, Is.GreaterThan(1), "SC-002: distant gap forces >1 route");
    }
}
