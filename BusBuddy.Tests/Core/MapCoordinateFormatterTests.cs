using BusBuddy.Core.Mapping;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class MapCoordinateFormatterTests
{
    [Test]
    public void FormatLatitude_WileySchool_UsesNorthSuffix()
    {
        Assert.That(MapCoordinateFormatter.FormatLatitude(WileyMapDefaults.SchoolLatitude), Is.EqualTo("38.1527N"));
    }

    [Test]
    public void FormatLongitude_WileySchool_UsesWestSuffix()
    {
        Assert.That(MapCoordinateFormatter.FormatLongitude(WileyMapDefaults.SchoolLongitude), Is.EqualTo("102.7204W"));
    }

    [Test]
    public void RouteWaypointSerializer_RoundTripsPairs()
    {
        var json = RouteWaypointSerializer.FromPairs(new[]
        {
            (WileyMapDefaults.SchoolLatitude, WileyMapDefaults.SchoolLongitude),
            (38.16, -102.70)
        });

        var parsed = RouteWaypointSerializer.Parse(json);

        Assert.That(parsed, Has.Count.EqualTo(2));
        Assert.That(parsed[0].Latitude, Is.EqualTo(WileyMapDefaults.SchoolLatitude).Within(0.0001));
        Assert.That(parsed[1].Longitude, Is.EqualTo(-102.70).Within(0.0001));
    }

    [Test]
    public void RouteWaypointSerializer_Parse_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.That(RouteWaypointSerializer.Parse(null), Is.Empty);
        Assert.That(RouteWaypointSerializer.Parse("not-json"), Is.Empty);
        Assert.That(RouteWaypointSerializer.Parse("{}"), Is.Empty);
    }
}
