using BusBuddy.Core.Mapping;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class MapCoordinateFormatterTests
{
    [Test]
    public void FormatLatitude_NorthernHemisphere_UsesNorthSuffix()
    {
        Assert.That(MapCoordinateFormatter.FormatLatitude(39.8283), Is.EqualTo("39.8283N"));
    }

    [Test]
    public void FormatLongitude_WesternHemisphere_UsesWestSuffix()
    {
        Assert.That(MapCoordinateFormatter.FormatLongitude(-98.5795), Is.EqualTo("98.5795W"));
    }

    [Test]
    public void RouteWaypointSerializer_RoundTripsPairs()
    {
        var json = RouteWaypointSerializer.FromPairs(new[]
        {
            (MapDefaults.UnconfiguredLatitude, MapDefaults.UnconfiguredLongitude),
            (40.0, -90.0)
        });

        var parsed = RouteWaypointSerializer.Parse(json);

        Assert.That(parsed, Has.Count.EqualTo(2));
        Assert.That(parsed[0].Latitude, Is.EqualTo(MapDefaults.UnconfiguredLatitude).Within(0.0001));
        Assert.That(parsed[1].Longitude, Is.EqualTo(-90.0).Within(0.0001));
    }

    [Test]
    public void RouteWaypointSerializer_ParsePayload_ReadsEncodedPolylineOnce()
    {
        const string encoded = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
        var json = RouteWaypointSerializer.FromEncodedPolyline(
            encoded,
            new[] { (38.0, -102.0), (38.1, -102.1) });

        var payload = RouteWaypointSerializer.ParsePayload(json);

        Assert.That(payload.EncodedPolyline, Is.EqualTo(encoded));
        Assert.That(payload.Stops, Has.Count.EqualTo(2));
        Assert.That(json, Does.Contain("stops"));
        Assert.That(json, Does.Not.Contain("\"points\""));
        Assert.That(payload.PathPoints.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(payload.Stops[0].Latitude, Is.EqualTo(38.0).Within(0.0001));
    }

    [Test]
    public void RouteWaypointSerializer_LegacyDensePoints_ArePathNotStops()
    {
        var json = """
            {"encodedPolyline":"_p~iF~ps|U_ulLnnqC_mqNvxq`@","points":[[38.0,-102.0],[38.01,-102.01],[38.02,-102.02],[38.03,-102.03]]}
            """;

        var payload = RouteWaypointSerializer.ParsePayload(json);

        Assert.That(payload.Stops, Is.Empty);
        Assert.That(payload.MarkerStops, Has.Count.EqualTo(2));
        Assert.That(payload.PathPoints.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(RouteWaypointSerializer.ParseStops(json), Is.Empty);
    }

    [Test]
    public void EncodedPolylineCodec_InvalidPayload_DoesNotThrow()
    {
        Assert.That(EncodedPolylineCodec.Decode("poly-1"), Is.Not.Null);
        Assert.That(EncodedPolylineCodec.Decode(null), Is.Empty);
    }

    [Test]
    public void RouteWaypointSerializer_Parse_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.That(RouteWaypointSerializer.Parse(null), Is.Empty);
        Assert.That(RouteWaypointSerializer.Parse("not-json"), Is.Empty);
        Assert.That(RouteWaypointSerializer.Parse("{}"), Is.Empty);
    }

    [Test]
    public void ZoomForBounds_SinglePoint_UsesSchoolZoom()
    {
        Assert.That(
            MapDefaults.ZoomForBounds(38.15, 38.15, -102.72, -102.72),
            Is.EqualTo(MapDefaults.SchoolZoomLevel));
    }

    [Test]
    public void ZoomForBounds_CountySpan_IsWiderThanSchool()
    {
        var zoom = MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0);
        Assert.That(zoom, Is.LessThan(MapDefaults.SchoolZoomLevel));
        Assert.That(zoom, Is.GreaterThanOrEqualTo(MapDefaults.MinFitZoomLevel));
    }

    [Test]
    public void ZoomForBounds_Neighborhood_IsTighterThanDistrict()
    {
        var zoom = MapDefaults.ZoomForBounds(38.15, 38.155, -102.72, -102.715);
        Assert.That(zoom, Is.GreaterThan(MapDefaults.DistrictZoomLevel));
        Assert.That(zoom, Is.LessThanOrEqualTo(MapDefaults.MaxFitZoomLevel));
    }

    [Test]
    public void ZoomForBounds_LargeCounty_IsNotClampedToSchoolZoom()
    {
        var zoom = MapDefaults.ZoomForBounds(37.6, 38.4, -103.4, -102.0);
        Assert.That(zoom, Is.LessThan(MapDefaults.DistrictZoomLevel));
        Assert.That(zoom, Is.GreaterThanOrEqualTo(MapDefaults.MinFitZoomLevel));
    }

    [Test]
    public void RadiusKilometers_CountySpan_IsLargerThanNeighborhood()
    {
        var county = MapDefaults.RadiusKilometers(38.0, 38.2, -103.0, -102.0);
        var block = MapDefaults.RadiusKilometers(38.15, 38.151, -102.72, -102.719);
        Assert.That(county, Is.GreaterThan(block));
        Assert.That(county, Is.GreaterThan(20));
    }
}
