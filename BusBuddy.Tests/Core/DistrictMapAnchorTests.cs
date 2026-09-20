using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DistrictMapAnchorTests
{
    [Test]
    public void ResolveCamera_Unconfigured_UsesUsOverview()
    {
        var camera = DistrictMapAnchor.ResolveCamera(new RoutingDistrictSettings());

        Assert.That(camera.Latitude, Is.EqualTo(MapDefaults.UnconfiguredLatitude).Within(0.0001));
        Assert.That(camera.Longitude, Is.EqualTo(MapDefaults.UnconfiguredLongitude).Within(0.0001));
        Assert.That(camera.ZoomLevel, Is.EqualTo(MapDefaults.UnconfiguredZoomLevel));
    }

    [Test]
    public void ResolveCamera_PrefersSchoolOverDepot()
    {
        var settings = new RoutingDistrictSettings
        {
            DepotLatitude = 38.0866,
            DepotLongitude = -102.6201
        };

        var camera = DistrictMapAnchor.ResolveCamera(settings, schoolLatitude: 38.1535, schoolLongitude: -102.7195);

        Assert.That(camera.Latitude, Is.EqualTo(38.1535).Within(0.0001));
        Assert.That(camera.Longitude, Is.EqualTo(-102.7195).Within(0.0001));
        Assert.That(camera.ZoomLevel, Is.EqualTo(MapDefaults.SchoolZoomLevel));
    }

    [Test]
    public void ResolveHomeCamera_PrefersDepotOverSchool()
    {
        var settings = new RoutingDistrictSettings
        {
            DepotLatitude = 38.0866,
            DepotLongitude = -102.6201
        };

        var camera = DistrictMapAnchor.ResolveHomeCamera(settings, schoolLatitude: 38.1535, schoolLongitude: -102.7195);

        Assert.That(camera.Latitude, Is.EqualTo(38.0866).Within(0.0001));
        Assert.That(camera.Longitude, Is.EqualTo(-102.6201).Within(0.0001));
        Assert.That(camera.ZoomLevel, Is.EqualTo(MapDefaults.DistrictZoomLevel));
    }

    [Test]
    public void TryGetConfiguredCenter_UsesDepotThenBboxCentroid()
    {
        var depot = new RoutingDistrictSettings
        {
            DepotLatitude = 38.0866,
            DepotLongitude = -102.6201,
            BoundingBoxMinLat = 38.05,
            BoundingBoxMaxLat = 38.30,
            BoundingBoxMinLon = -102.85,
            BoundingBoxMaxLon = -102.55
        };

        Assert.That(DistrictMapAnchor.TryGetConfiguredCenter(depot, out var lat, out var lon), Is.True);
        Assert.That(lat, Is.EqualTo(38.0866).Within(0.0001));
        Assert.That(lon, Is.EqualTo(-102.6201).Within(0.0001));

        var bboxOnly = new RoutingDistrictSettings
        {
            BoundingBoxMinLat = 38.05,
            BoundingBoxMaxLat = 38.25,
            BoundingBoxMinLon = -102.80,
            BoundingBoxMaxLon = -102.60
        };

        Assert.That(DistrictMapAnchor.TryGetConfiguredCenter(bboxOnly, out lat, out lon), Is.True);
        Assert.That(lat, Is.EqualTo(38.15).Within(0.0001));
        Assert.That(lon, Is.EqualTo(-102.70).Within(0.0001));
    }

    [Test]
    public void MapDefaults_AreNotANamedTown()
    {
        Assert.That(MapDefaults.UnconfiguredLatitude, Is.EqualTo(39.8283).Within(0.0001));
        Assert.That(MapDefaults.UnconfiguredZoomLevel, Is.EqualTo(5));
        var src = CoreSourceFile.Read("Mapping/MapDefaults.cs");
        Assert.That(src, Does.Not.Contain("Wiley"));
        Assert.That(src, Does.Not.Contain("Lamar"));
        Assert.That(src, Does.Not.Contain("38.0872"));
    }
}
