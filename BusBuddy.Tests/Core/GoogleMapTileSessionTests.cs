using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services.GoogleMaps;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class GoogleMapTileSessionTests
{
    [Test]
    public void TileUrlTemplate_UsesOfficialMapTilesPathAndPlaceholders()
    {
        var url = MapBasemap.TileUrlTemplate("sess-1", "key-1");

        Assert.That(url, Does.StartWith("https://tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}"));
        Assert.That(url, Does.Contain("session=sess-1"));
        Assert.That(url, Does.Contain("key=key-1"));
        Assert.That(url, Does.Not.Contain("mt1.google.com"));
        Assert.That(url, Does.Not.Contain("lyrs="));
    }

    [Test]
    public void IsGoogle_RecognizesRoadAndSatelliteOnly()
    {
        Assert.That(MapBasemap.IsGoogle(MapBasemap.GoogleRoad), Is.True);
        Assert.That(MapBasemap.IsGoogle(MapBasemap.GoogleSatellite), Is.True);
        Assert.That(MapBasemap.IsGoogle(MapBasemap.OpenStreetMap), Is.False);
        Assert.That(MapBasemap.MapTypeFor(MapBasemap.GoogleSatellite), Is.EqualTo("satellite"));
        Assert.That(MapBasemap.MapTypeFor(MapBasemap.GoogleRoad), Is.EqualTo("roadmap"));
    }

    [Test]
    public void ParseExpiry_AcceptsUnixSecondsAndIso()
    {
        var now = DateTimeOffset.Parse("2026-09-07T00:00:00Z");
        var unix = now.AddHours(2).ToUnixTimeSeconds().ToString();
        Assert.That(GoogleMapTileSessionService.ParseExpiry(unix, now), Is.EqualTo(now.AddHours(2)));
        Assert.That(
            GoogleMapTileSessionService.ParseExpiry("2026-09-07T12:00:00Z", now).UtcDateTime,
            Is.EqualTo(DateTime.Parse("2026-09-07T12:00:00Z").ToUniversalTime()));
        Assert.That(GoogleMapTileSessionService.ParseExpiry(null, now), Is.EqualTo(now.AddHours(12)));
    }

    [Test]
    public void NormalizeMapType_DefaultsToRoadmap()
    {
        Assert.That(GoogleMapTileSessionService.NormalizeMapType("satellite"), Is.EqualTo("satellite"));
        Assert.That(GoogleMapTileSessionService.NormalizeMapType("nope"), Is.EqualTo("roadmap"));
    }

    [Test]
    public void BuildViewportUri_UsesDocumentedViewportEndpointAndBounds()
    {
        var bounds = new MapViewportBounds(North: 38.2, South: 37.9, East: -102.4, West: -102.9);
        var uri = GoogleMapTileSessionService.BuildViewportUri("sess-1", "key-1", 12, bounds);

        Assert.That(uri.Host, Is.EqualTo("tile.googleapis.com"));
        Assert.That(uri.AbsolutePath, Is.EqualTo("/tile/v1/viewport"));
        Assert.That(uri.Query, Does.Contain("session=sess-1"));
        Assert.That(uri.Query, Does.Contain("key=key-1"));
        Assert.That(uri.Query, Does.Contain("zoom=12"));
        Assert.That(uri.Query, Does.Contain("north=38.2"));
        Assert.That(uri.Query, Does.Contain("south=37.9"));
        Assert.That(uri.Query, Does.Contain("east=-102.4"));
        Assert.That(uri.Query, Does.Contain("west=-102.9"));
    }

    [Test]
    public void ParseViewportCopyright_ReadsCopyrightAndToleratesGarbage()
    {
        Assert.That(
            GoogleMapTileSessionService.ParseViewportCopyright(
                """{"copyright":"Map data ©2026 Google","maxZoomRects":[{"maxZoom":19,"north":38.2,"south":37.9,"east":-102.4,"west":-102.9}]}"""),
            Is.EqualTo("Map data ©2026 Google"));
        Assert.That(GoogleMapTileSessionService.ParseViewportCopyright("""{"copyright":"  "}"""), Is.Null);
        Assert.That(GoogleMapTileSessionService.ParseViewportCopyright("not json"), Is.Null);
        Assert.That(GoogleMapTileSessionService.ParseViewportCopyright(null), Is.Null);
    }

    [Test]
    public void BoundsForViewport_IsInverseOfZoomForBounds()
    {
        // Clerk default center (38.0872, -102.6208), 1024x768 viewport.
        var bounds = MapDefaults.BoundsForViewport(38.0872, -102.6208, 12, 1024, 768);

        Assert.That(bounds.North, Is.GreaterThan(38.0872));
        Assert.That(bounds.South, Is.LessThan(38.0872));
        Assert.That(bounds.East, Is.GreaterThan(-102.6208));
        Assert.That(bounds.West, Is.LessThan(-102.6208));
        // 1024px at z12 = 1024/(256*4096) of 360° ≈ 0.3516° total → ±0.1758°.
        Assert.That(bounds.East - bounds.West, Is.EqualTo(0.3516).Within(0.001));
        Assert.That(bounds.North - bounds.South, Is.EqualTo(0.3516 * 768 / 1024 * Math.Cos(38.0872 * Math.PI / 180)).Within(0.01));

        // Refitting the box we just computed lands back on the same zoom (padding shifts it by at most one level).
        var refit = MapDefaults.ZoomForBounds(bounds.South, bounds.North, bounds.West, bounds.East, 1024, 768);
        Assert.That(refit, Is.InRange(11, 12));
    }

    [Test]
    public void BoundsForViewport_ClampsToWorldAndFallsBackOnBadViewport()
    {
        var world = MapDefaults.BoundsForViewport(0, 0, 1, 5000, 5000);
        Assert.That(world.East, Is.EqualTo(180));
        Assert.That(world.West, Is.EqualTo(-180));
        Assert.That(world.North, Is.LessThanOrEqualTo(85.06));
        Assert.That(world.South, Is.GreaterThanOrEqualTo(-85.06));

        var fallback = MapDefaults.BoundsForViewport(38, -102, 12, double.NaN, 0);
        var expected = MapDefaults.BoundsForViewport(38, -102, 12, MapDefaults.DefaultViewportWidth, MapDefaults.DefaultViewportHeight);
        Assert.That(fallback, Is.EqualTo(expected));
    }
}
