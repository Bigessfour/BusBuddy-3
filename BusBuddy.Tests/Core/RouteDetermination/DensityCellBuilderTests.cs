using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.RouteDetermination;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class DensityCellBuilderTests
{
    [Test]
    public void Build_NearbyRiders_YieldsCellsAndExcludesEmptyCoords()
    {
        var settings = new RoutingDistrictSettings
        {
            TargetRidersPerCell = 6,
            BoundingBoxMinLat = 38.0,
            BoundingBoxMaxLat = 38.2,
            BoundingBoxMinLon = -102.8,
            BoundingBoxMaxLon = -102.6
        };

        var riders = new List<RiderPoint>();
        for (var i = 0; i < 12; i++)
        {
            riders.Add(new RiderPoint(i + 1, 38.10 + (i % 4) * 0.01, -102.70 + (i / 4) * 0.01));
        }

        // Invalid / empty coords should be excluded by builder (NaN)
        riders.Add(new RiderPoint(999, double.NaN, -102.7));

        var cells = DensityCellBuilder.Build(riders, settings);

        Assert.That(cells, Is.Not.Empty);
        Assert.That(cells.Sum(c => c.Riders.Count), Is.EqualTo(12));
        Assert.That(cells.SelectMany(c => c.Riders).Any(r => r.StudentId == 999), Is.False);
        Assert.That(cells.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        var cells = DensityCellBuilder.Build(Array.Empty<RiderPoint>(), new RoutingDistrictSettings());
        Assert.That(cells, Is.Empty);
    }
}
