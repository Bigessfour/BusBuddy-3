using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using System.Linq;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class MapsAddressCacheTests
{
    [Test]
    public void BuildCacheKey_IsCaseInsensitive()
    {
        var a = MapsAddressCache.BuildCacheKey("1 Main St", "Wiley", "CO", "81092");
        var b = MapsAddressCache.BuildCacheKey("1 main st", "wiley", "co", "81092");
        a.Should().Be(b);
    }

    [Test]
    public void SetAndTryGet_RoundTripsSuccessfulResult()
    {
        var cache = new MapsAddressCache();
        var key = MapsAddressCache.BuildCacheKey("1 Main", "Wiley", "CO", "81092");
        var result = new MapsGeocodeResult
        {
            Ok = true,
            Latitude = 37.1,
            Longitude = -102.7,
            Precision = "ROOFTOP",
        };

        cache.Set(key, result);
        cache.TryGet(key, out var hit).Should().BeTrue();
        hit!.Latitude.Should().Be(37.1);
    }

    [Test]
    public void Set_DoesNotStoreFailedResults()
    {
        var cache = new MapsAddressCache();
        var key = MapsAddressCache.BuildCacheKey("bad", "addr", "CO", "00000");
        cache.Set(key, new MapsGeocodeResult { Ok = false, ErrorMessage = "nope" });
        cache.TryGet(key, out _).Should().BeFalse();
    }
}

[TestFixture]
[Category("Unit")]
public class RouteDrivePathRefresherTests
{
    [Test]
    public async Task TryRefresh_UpdatesWaypointsJsonOnSuccess()
    {
        var route = new Route
        {
            RouteId = 1,
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71),
            }),
        };

        var routing = new Mock<IRoutingService>();
        routing.Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default))
            .ReturnsAsync(new DrivePathResult
            {
                EncodedPolyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
                Points = new List<(double, double)> { (38.15, -102.72), (38.16, -102.71) },
                DistanceMeters = 500,
                Duration = "60s",
            });

        var result = await RouteDrivePathRefresher.TryRefreshAsync(routing.Object, route);

        result.Success.Should().BeTrue();
        route.WaypointsJson.Should().Contain("encodedPolyline");
        route.WaypointsJson.Should().Contain("stops");
        route.WaypointsJson.Should().NotContain("\"points\"");
    }

    [Test]
    public async Task TryRefresh_LegacyDensePoints_AreNotSentAsIntermediates()
    {
        var json = """
            {"encodedPolyline":"_p~iF~ps|U_ulLnnqC_mqNvxq`@","points":[[38.0,-102.0],[38.01,-102.01],[38.02,-102.02]]}
            """;
        var route = new Route { WaypointsJson = json };
        var routing = new Mock<IRoutingService>();

        var result = await RouteDrivePathRefresher.TryRefreshAsync(routing.Object, route);

        result.Skipped.Should().BeTrue();
        routing.Verify(
            r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default),
            Times.Never);
    }

    [Test]
    public void CapIntermediateWaypoints_SamplesEvenlyToMax()
    {
        var many = Enumerable.Range(0, 80)
            .Select(i => (38.0 + i * 0.001, -102.0))
            .ToList();

        var capped = RouteDrivePathRefresher.CapIntermediateWaypoints(many);

        capped.Should().HaveCount(RouteDrivePathRefresher.MaxIntermediateWaypoints);
        capped[0].Should().Be(many[0]);
        capped[^1].Should().Be(many[^1]);
    }

    [Test]
    public async Task TryRefresh_SkipsWhenFewerThanTwoStops()
    {
        var route = new Route { WaypointsJson = RouteWaypointSerializer.FromPairs(new[] { (1.0, 2.0) }) };
        var routing = new Mock<IRoutingService>();

        var result = await RouteDrivePathRefresher.TryRefreshAsync(routing.Object, route);

        result.Skipped.Should().BeTrue();
        routing.Verify(
            r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default),
            Times.Never);
    }
}
