using System;
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
    public void ZoomForBounds_WiderViewportFitsDeeper()
    {
        var narrow = MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0, 400, 300);
        var wide = MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0, 2400, 1400);
        Assert.That(wide, Is.GreaterThan(narrow));
    }

    [Test]
    public void ZoomForBounds_DefaultOverloadMatchesDefaultViewport()
    {
        Assert.That(
            MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0),
            Is.EqualTo(MapDefaults.ZoomForBounds(
                38.0, 38.2, -103.0, -102.0,
                MapDefaults.DefaultViewportWidth, MapDefaults.DefaultViewportHeight)));
    }

    [Test]
    public void ZoomForBounds_InvalidViewportFallsBackToDefault()
    {
        var expected = MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0);
        Assert.That(MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0, 0, 0), Is.EqualTo(expected));
        Assert.That(MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0, double.NaN, -5), Is.EqualTo(expected));
    }

    [Test]
    public void ZoomForBounds_CountySpanFitsInsideDefaultViewport()
    {
        // 1 degree of longitude at zoom z spans 256 * 2^z / 360 px; the fit must leave the padded width unexceeded.
        const double minLon = -103.0, maxLon = -102.0;
        var zoom = MapDefaults.ZoomForBounds(38.0, 38.2, minLon, maxLon);
        var worldPx = MapDefaults.TilePixels * Math.Pow(2, zoom);
        var spanPx = worldPx * (maxLon - minLon) / 360d;
        Assert.That(spanPx, Is.LessThanOrEqualTo(MapDefaults.DefaultViewportWidth * MapDefaults.FitPaddingFraction));

        var nextWorldPx = MapDefaults.TilePixels * Math.Pow(2, zoom + 1);
        var nextSpanPx = nextWorldPx * (maxLon - minLon) / 360d;
        Assert.That(nextSpanPx, Is.GreaterThan(MapDefaults.DefaultViewportWidth * MapDefaults.FitPaddingFraction),
            "one more zoom step would overflow the padded viewport, so this is the tightest fit");
    }

    [Test]
    public void ZoomForBounds_TallSpanIsTightestFitOnLatitudeAxis()
    {
        // Tall, narrow box: latitude is the binding axis. Mercator Y covers [-π, π] for the whole world,
        // so the pixel height of the span is worldPx * ΔY / 2π.
        const double minLat = 37.6, maxLat = 38.6, minLon = -102.7, maxLon = -102.6;
        var zoom = MapDefaults.ZoomForBounds(minLat, maxLat, minLon, maxLon);

        static double MercY(double lat)
        {
            var sin = Math.Sin(lat * Math.PI / 180d);
            return Math.Log((1 + sin) / (1 - sin)) / 2d;
        }

        var dy = Math.Abs(MercY(maxLat) - MercY(minLat));
        var spanPx = MapDefaults.TilePixels * Math.Pow(2, zoom) * dy / (2 * Math.PI);
        var usable = MapDefaults.DefaultViewportHeight * MapDefaults.FitPaddingFraction;
        Assert.That(spanPx, Is.LessThanOrEqualTo(usable));
        Assert.That(spanPx * 2, Is.GreaterThan(usable), "one more zoom step would overflow the padded height");
    }

    [Test]
    public void ClampZoom_AndDetailLabels_FollowImageryLayerRange()
    {
        Assert.That(MapDefaults.ClampZoom(0), Is.EqualTo(MapDefaults.MinZoomLevel));
        Assert.That(MapDefaults.ClampZoom(99), Is.EqualTo(MapDefaults.MaxZoomLevel));
        Assert.That(MapDefaults.MaxZoomLevel, Is.EqualTo(19));
        Assert.That(MapDefaults.ShowsDetailLabels(MapDefaults.DetailLabelZoomLevel - 1), Is.False);
        Assert.That(MapDefaults.ShowsDetailLabels(MapDefaults.DetailLabelZoomLevel), Is.True);
        Assert.That(MapDefaults.SchoolZoomLevel, Is.GreaterThanOrEqualTo(MapDefaults.DetailLabelZoomLevel),
            "a school-zoomed view must always show captions");
    }

    [Test]
    public void ResolveTileUrl_ExpandsOfficialMapTilesTemplate()
    {
        var template = MapBasemap.TileUrlTemplate("sess-1", "key-1");
        var url = MapBasemap.ResolveTileUrl(template, 12, 845, 1611);
        Assert.That(url, Does.StartWith("https://tile.googleapis.com/v1/2dtiles/12/845/1611?"));
        Assert.That(url, Does.Not.Contain("{"));
    }
}
