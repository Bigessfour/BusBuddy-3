using BusBuddy.Core.Mapping;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class MapCoordinateFormatterTests
{
    [Test]
    public void FormatLatitude_NorthernHemisphere_UsesNorthSuffix()
    {
        Assert.That(MapCoordinateFormatter.FormatLatitude(MapDefaults.FallbackLatitude), Is.EqualTo("39.8283N"));
    }

    [Test]
    public void FormatLongitude_WesternHemisphere_UsesWestSuffix()
    {
        Assert.That(MapCoordinateFormatter.FormatLongitude(MapDefaults.FallbackLongitude), Is.EqualTo("98.5795W"));
    }

    [Test]
    public void RouteWaypointSerializer_RoundTripsPairs()
    {
        var json = RouteWaypointSerializer.FromPairs(new[]
        {
            (MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude),
            (40.0, -90.0)
        });

        var parsed = RouteWaypointSerializer.Parse(json);

        Assert.That(parsed, Has.Count.EqualTo(2));
        Assert.That(parsed[0].Latitude, Is.EqualTo(MapDefaults.FallbackLatitude).Within(0.0001));
        Assert.That(parsed[1].Longitude, Is.EqualTo(-90.0).Within(0.0001));
    }

    [Test]
    public void RouteWaypointSerializer_Parse_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.That(RouteWaypointSerializer.Parse(null), Is.Empty);
        Assert.That(RouteWaypointSerializer.Parse("not-json"), Is.Empty);
        Assert.That(RouteWaypointSerializer.Parse("{}"), Is.Empty);
    }
}
