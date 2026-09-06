using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

/// <summary>
/// MapViewModel calls IMapsGeoService, DistrictDepot, and RouteDrivePathRefresher.
/// These types must live in BusBuddy.Core — mixing an attached VM with an older Core tree will not build.
/// </summary>
[TestFixture]
[Category("Unit")]
public class MapViewCoreDependencyTests
{
    [Test]
    public void IMapsGeoService_DeclaresGeocodeAndIsConfigured()
    {
        var type = typeof(IMapsGeoService);
        Assert.That(type.IsInterface, Is.True);
        Assert.That(type.GetProperty(nameof(IMapsGeoService.IsConfigured)), Is.Not.Null);
        Assert.That(
            type.GetMethods().Any(m => m.Name == nameof(IMapsGeoService.GeocodeAsync)),
            Is.True);
    }

    [Test]
    public void DistrictDepot_TryGetCoordinates_ReturnsFalseWhenUnconfigured()
    {
        Assert.That(typeof(DistrictDepot).IsAbstract, Is.True);
        Assert.That(DistrictDepot.TryGetCoordinates(null, out var lat, out var lon), Is.False);
        Assert.That(lat, Is.EqualTo(0));
        Assert.That(lon, Is.EqualTo(0));
    }

    [Test]
    public async Task RouteDrivePathRefresher_SkipsWhenRoutingServiceMissing()
    {
        Assert.That(typeof(RouteDrivePathRefresher).IsAbstract, Is.True);
        var result = await RouteDrivePathRefresher.TryRefreshAsync(null, new Route());
        Assert.That(result.Skipped, Is.True);
    }
}
