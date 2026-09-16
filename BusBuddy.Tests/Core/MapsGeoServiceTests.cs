using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using System.IO;
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
        var now = DateTimeOffset.Parse("2026-09-06T12:00:00Z");
        var cache = new MapsAddressCache(null, new FixedTimeProvider(now));
        var key = MapsAddressCache.BuildCacheKey("1 Main", "Wiley", "CO", "81092");
        var result = new MapsGeocodeResult
        {
            Ok = true,
            Latitude = 37.1,
            Longitude = -102.7,
            PlaceId = "ChIJ_test",
            Precision = "ROOFTOP",
        };

        cache.Set(key, result);
        cache.TryGet(key, out var hit).Should().BeTrue();
        hit!.Latitude.Should().Be(37.1);
        hit.PlaceId.Should().Be("ChIJ_test");
        hit.CachedAtUtc.Should().Be(now);
    }

    [Test]
    public void Set_DoesNotStoreFailedResults()
    {
        var cache = new MapsAddressCache();
        var key = MapsAddressCache.BuildCacheKey("bad", "addr", "CO", "00000");
        cache.Set(key, new MapsGeocodeResult { Ok = false, ErrorMessage = "nope" });
        cache.TryGet(key, out _).Should().BeFalse();
    }

    [Test]
    public void TryGet_ExpiresCoordinatesAfterThirtyDays()
    {
        var t0 = DateTimeOffset.Parse("2026-08-01T12:00:00Z");
        var t1 = t0.AddDays(31);
        var path = Path.Combine(Path.GetTempPath(), $"bb-maps-cache-{Guid.NewGuid():N}.json");
        try
        {
            var cache = new MapsAddressCache(path, new FixedTimeProvider(t0));
            var key = MapsAddressCache.BuildCacheKey("1 Main", "Wiley", "CO", "81092");
            cache.Set(key, new MapsGeocodeResult
            {
                Ok = true,
                Latitude = 37.1,
                Longitude = -102.7,
                PlaceId = "ChIJ_keep",
            });

            var later = new MapsAddressCache(path, new FixedTimeProvider(t1));
            later.TryGet(key, out _).Should().BeFalse();

            var json = File.ReadAllText(path);
            json.Should().NotContain("37.1");
            json.Should().NotContain("ChIJ_keep");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
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
    public void ApplyPathMetrics_SetsMilesMinutesAndCaption()
    {
        var route = new Route();
        RouteDrivePathRefresher.ApplyPathMetrics(route, new DrivePathResult
        {
            DistanceMeters = 1609,
            Duration = "180s"
        });

        route.Distance.Should().Be(1.00m);
        route.EstimatedDuration.Should().Be(3);
        route.Path.Should().Be("1.0 mi · 180s");
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

    [Test]
    public async Task TryRefresh_OnFailure_KeepsStoredJson()
    {
        var stored = RouteWaypointSerializer.FromPairs(new[]
        {
            (38.15, -102.72),
            (38.16, -102.71),
        });
        var route = new Route { RouteId = 2, WaypointsJson = stored };
        var routing = new Mock<IRoutingService>();
        routing.Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default))
            .ReturnsAsync(new DrivePathResult { Error = "quota" });

        var result = await RouteDrivePathRefresher.TryRefreshAsync(routing.Object, route);

        result.Success.Should().BeFalse();
        route.WaypointsJson.Should().Be(stored);
    }

    [Test]
    public async Task TryRefresh_WhenRoutingMissing_KeepsStoredJson()
    {
        var stored = RouteWaypointSerializer.FromPairs(new[]
        {
            (38.15, -102.72),
            (38.16, -102.71),
        });
        var route = new Route { RouteId = 3, WaypointsJson = stored };

        var result = await RouteDrivePathRefresher.TryRefreshAsync(null, route);

        result.Skipped.Should().BeTrue();
        result.Success.Should().BeFalse();
        route.WaypointsJson.Should().Be(stored);
    }

    [Test]
    public async Task TryRefresh_OnException_KeepsStoredJson()
    {
        var stored = RouteWaypointSerializer.FromPairs(new[]
        {
            (38.15, -102.72),
            (38.16, -102.71),
        });
        var route = new Route { RouteId = 4, WaypointsJson = stored };
        var routing = new Mock<IRoutingService>();
        routing.Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default))
            .ThrowsAsync(new InvalidOperationException("network down"));

        var result = await RouteDrivePathRefresher.TryRefreshAsync(routing.Object, route);

        result.Success.Should().BeFalse();
        result.Skipped.Should().BeFalse();
        route.WaypointsJson.Should().Be(stored);
    }
}
