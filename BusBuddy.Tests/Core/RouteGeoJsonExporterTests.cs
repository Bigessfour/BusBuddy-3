using System.Text.Json;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class RouteGeoJsonExporterTests
{
    [Test]
    public void TryBuild_EmptyWaypoints_ReturnsNull()
    {
        var route = new Route { RouteId = 1, RouteName = "AM-1" };
        Assert.That(RouteGeoJsonExporter.TryBuild(route), Is.Null);
    }

    [Test]
    public void TryBuild_LineString_UsesLonLatOrderAndRouteProperties()
    {
        var route = new Route
        {
            RouteId = 9,
            RouteName = "AM-North",
            School = "Rural K-12",
            Date = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc),
            Distance = 12.5m,
            StopCount = 2,
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71)
            })
        };

        var json = RouteGeoJsonExporter.TryBuild(route);
        Assert.That(json, Is.Not.Null);

        using var doc = JsonDocument.Parse(json!);
        Assert.That(doc.RootElement.GetProperty("type").GetString(), Is.EqualTo("FeatureCollection"));

        var feature = doc.RootElement.GetProperty("features")[0];
        var props = feature.GetProperty("properties");
        Assert.That(props.GetProperty("routeId").GetInt32(), Is.EqualTo(9));
        Assert.That(props.GetProperty("routeName").GetString(), Is.EqualTo("AM-North"));
        Assert.That(props.GetProperty("school").GetString(), Is.EqualTo("Rural K-12"));
        Assert.That(props.GetProperty("date").GetString(), Is.EqualTo("2026-09-06"));

        var geometry = feature.GetProperty("geometry");
        Assert.That(geometry.GetProperty("type").GetString(), Is.EqualTo("LineString"));
        var first = geometry.GetProperty("coordinates")[0];
        Assert.That(first[0].GetDouble(), Is.EqualTo(-102.72).Within(0.0001));
        Assert.That(first[1].GetDouble(), Is.EqualTo(38.15).Within(0.0001));
    }

    [Test]
    public void TryBuild_SinglePoint_UsesPointGeometry()
    {
        var route = new Route
        {
            RouteId = 2,
            RouteName = "Depot",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[] { (38.0, -102.0) })
        };

        using var doc = JsonDocument.Parse(RouteGeoJsonExporter.TryBuild(route)!);
        var geometry = doc.RootElement.GetProperty("features")[0].GetProperty("geometry");
        Assert.That(geometry.GetProperty("type").GetString(), Is.EqualTo("Point"));
        Assert.That(geometry.GetProperty("coordinates")[0].GetDouble(), Is.EqualTo(-102.0).Within(0.0001));
    }

    [Test]
    public void TryBuild_IncludesEncodedPolylineWhenPresent()
    {
        var route = new Route
        {
            RouteId = 3,
            RouteName = "Encoded",
            WaypointsJson = RouteWaypointSerializer.FromEncodedPolyline(
                "abc123",
                new[] { (38.0, -102.0), (38.1, -102.1) })
        };

        using var doc = JsonDocument.Parse(RouteGeoJsonExporter.TryBuild(route)!);
        var encoded = doc.RootElement.GetProperty("features")[0]
            .GetProperty("properties")
            .GetProperty("encodedPolyline")
            .GetString();
        Assert.That(encoded, Is.EqualTo("abc123"));
    }
}
