using BusBuddy.Core.Mapping;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DistrictBoundaryFileTests
{
    [Test]
    public void TryReadExtent_ReadsAMultiPolygon()
    {
        const string json = """
            {"type":"MultiPolygon","coordinates":[[[[-102.5,37.7],[-102.4,37.7],[-102.4,37.8],[-102.5,37.7]]],[[[-102.2,38.0],[-102.1,38.0],[-102.1,38.2],[-102.2,38.0]]]]}
            """;

        Assert.That(DistrictBoundaryFile.TryReadExtent(json, out var extent, out var error), Is.True);
        Assert.That(error, Is.Empty);
        Assert.That(extent.South, Is.EqualTo(37.7).Within(0.0001));
        Assert.That(extent.North, Is.EqualTo(38.2).Within(0.0001));
        Assert.That(extent.West, Is.EqualTo(-102.5).Within(0.0001));
        Assert.That(extent.East, Is.EqualTo(-102.1).Within(0.0001));
    }

    [Test]
    public void TryReadExtent_RejectsCoordinatesOutsideTheValidRange()
    {
        const string json = """
            {"type":"Polygon","coordinates":[[[0,0],[200,0],[200,10],[0,0]]]}
            """;

        Assert.That(DistrictBoundaryFile.TryReadExtent(json, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("The boundary file has coordinates outside the valid range."));
    }

    [Test]
    public void TryReadExtent_RejectsTextThatIsNotGeoJson()
    {
        Assert.That(DistrictBoundaryFile.TryReadExtent("not json", out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("The boundary file is not GeoJSON."));
    }
}
