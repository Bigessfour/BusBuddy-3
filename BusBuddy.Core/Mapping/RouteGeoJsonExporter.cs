using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Builds RFC 7946 GeoJSON for a route's stored waypoints (stop coordinates, not student names).
/// </summary>
public static class RouteGeoJsonExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Returns a FeatureCollection, or null when the route has no parseable waypoints.
    /// </summary>
    public static string? TryBuild(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
        if (payload.Points.Count == 0)
        {
            return null;
        }

        var properties = new JsonObject
        {
            ["kind"] = "route",
            ["routeId"] = route.RouteId,
            ["routeName"] = route.RouteName,
            ["date"] = route.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["isActive"] = route.IsActive,
            ["stopCount"] = route.StopCount ?? (payload.Stops.Count > 0 ? payload.Stops.Count : payload.Points.Count)
        };
        if (!string.IsNullOrWhiteSpace(route.School))
        {
            properties["school"] = route.School;
        }

        if (route.Distance is { } miles)
        {
            properties["distanceMiles"] = miles;
        }

        if (payload.EncodedPolyline is not null)
        {
            properties["encodedPolyline"] = payload.EncodedPolyline;
        }

        var geometry = new JsonObject
        {
            ["type"] = payload.Points.Count == 1 ? "Point" : "LineString",
            ["coordinates"] = payload.Points.Count == 1
                ? Position(payload.Points[0])
                : LineString(payload.Points)
        };

        var root = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "Feature",
                    ["properties"] = properties,
                    ["geometry"] = geometry
                })
        };

        return root.ToJsonString(JsonOptions);
    }

    private static JsonArray Position((double Latitude, double Longitude) point) =>
        [point.Longitude, point.Latitude];

    private static JsonArray LineString(IReadOnlyList<(double Latitude, double Longitude)> points)
    {
        var coords = new JsonArray();
        foreach (var point in points)
        {
            coords.Add(Position(point));
        }

        return coords;
    }
}
