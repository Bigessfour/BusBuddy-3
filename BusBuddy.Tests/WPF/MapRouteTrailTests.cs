using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapRouteTrailTests
{
    [Test]
    public void Build_NullRoute_ClearsTrail()
    {
        var plot = MapRouteTrail.Build(null);
        Assert.That(plot.Line, Is.Empty);
        Assert.That(plot.Markers, Is.Empty);
        Assert.That(plot.StatusMessage, Does.Contain("Select a route"));
    }

    [Test]
    public void Build_EncodedPolyline_PlotsStopsNotEveryVertex()
    {
        const string encoded = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
        var route = new Route
        {
            RouteName = "AM-Road",
            WaypointsJson = RouteWaypointSerializer.FromEncodedPolyline(
                encoded,
                new[] { (38.15, -102.72), (38.16, -102.71) })
        };

        var plot = MapRouteTrail.Build(route);

        Assert.That(plot.Markers, Has.Count.EqualTo(2));
        Assert.That(plot.Line.Count, Is.EqualTo(EncodedPolylineCodec.Decode(encoded).Count));
        Assert.That(plot.Line.Count, Is.GreaterThan(2));
        Assert.That(plot.StatusMessage, Does.Contain("trail"));
        Assert.That(plot.StatusMessage, Does.Contain("point(s)"));
    }

    [Test]
    public void Build_RenderableLineCount_DrivesStatusNotRawGeometry()
    {
        var route = new Route
        {
            RouteName = "AM-1",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71),
            }),
        };

        var plot = MapRouteTrail.Build(route, publishedValidatedStopCount: 5, renderableLinePointCount: 0);

        Assert.That(plot.StatusMessage, Does.Contain("Refresh"));
        Assert.That(plot.StatusMessage, Does.Contain("5 published"));
    }

    [Test]
    public void Build_UsesPublishedStopCountInStatusWhenProvided()
    {
        var route = new Route
        {
            RouteName = "AM Special Needs Bus 5",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71),
                (38.17, -102.70),
                (38.18, -102.69),
            }),
        };

        var plot = MapRouteTrail.Build(route, publishedValidatedStopCount: 14);

        Assert.That(plot.StatusMessage, Does.Contain("14 published stop"));
        Assert.That(plot.StatusMessage, Does.Not.Match(@"(?<!\d)4 published"));
    }

    [Test]
    public void MarkerLabel_UsesStartEndAndStopIndex()
    {
        Assert.That(MapRouteTrail.MarkerLabel(0, 3), Is.EqualTo("WP Start"));
        Assert.That(MapRouteTrail.MarkerLabel(1, 3), Is.EqualTo("WP Stop 1"));
        Assert.That(MapRouteTrail.MarkerLabel(2, 3), Is.EqualTo("WP End"));
    }

    [Test]
    public async Task RefreshStoredPathAsync_WhenRouteServiceMissing_ReportsNotPersisted()
    {
        var routing = new Mock<IRoutingService>();
        routing
            .Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrivePathResult
            {
                EncodedPolyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
                Points = new[] { (38.15, -102.72), (38.16, -102.71) },
            });

        var trail = new MapRouteTrail(routing.Object, scopes: null);
        var route = new Route
        {
            RouteId = 1,
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71),
            }),
        };

        var persist = await trail.RefreshStoredPathAsync(route);

        Assert.That(persist.Computed, Is.True);
        Assert.That(persist.Persisted, Is.False);
        Assert.That(persist.Message, Does.Contain("not saved"));
        Assert.That(route.WaypointsJson, Does.Contain("encodedPolyline"));
    }
}
