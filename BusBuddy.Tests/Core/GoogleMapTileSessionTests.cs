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
}
