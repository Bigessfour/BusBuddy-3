using System.Globalization;
using System.Text;
using System.Text.Json;
using Serilog;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Compact stop list plus optional Routes API polyline.
/// Draw <see cref="PathPoints"/>; send <see cref="Stops"/> to Google — never decoded vertices.
/// </summary>
public readonly record struct RouteWaypointPayload(
    IReadOnlyList<(double Latitude, double Longitude)> Stops,
    IReadOnlyList<(double Latitude, double Longitude)> PathPoints,
    string? EncodedPolyline)
{
    /// <summary>Line geometry: decoded road path, else the stop list.</summary>
    public IReadOnlyList<(double Latitude, double Longitude)> Points =>
        PathPoints.Count > 0 ? PathPoints : Stops;

    /// <summary>
    /// Pins to plot: stored stops, or Start/End of a legacy road path that has no stop list.
    /// </summary>
    public IReadOnlyList<(double Latitude, double Longitude)> MarkerStops
    {
        get
        {
            if (Stops.Count > 0)
            {
                return Stops;
            }

            if (PathPoints.Count >= 2)
            {
                return new[] { PathPoints[0], PathPoints[^1] };
            }

            return PathPoints;
        }
    }
}

/// <summary>
/// Compact waypoint JSON: either <c>[[lat,lon], ...]</c> or
/// <c>{"encodedPolyline":"...","stops":[[lat,lon],...]}</c>.
/// Legacy <c>points</c> arrays are still read.
/// </summary>
public static class RouteWaypointSerializer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteWaypointSerializer));

    public static string FromPairs(IEnumerable<(double Latitude, double Longitude)> points)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        var first = true;
        var count = 0;
        foreach (var (lat, lon) in points)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            count++;
            sb.Append('[')
                .Append(lat.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(lon.ToString(CultureInfo.InvariantCulture))
                .Append(']');
        }

        sb.Append(']');
        Logger.Debug("Serialized {Count} waypoints to JSON", count);
        return sb.ToString();
    }

    /// <summary>
    /// Store encoded road polyline plus the stop list used to request it (not decoded vertices).
    /// </summary>
    public static string FromEncodedPolyline(
        string encodedPolyline,
        IEnumerable<(double Latitude, double Longitude)> stops)
    {
        var payload = new
        {
            encodedPolyline,
            stops = stops.Select(p => new[] { p.Latitude, p.Longitude }).ToArray()
        };
        var json = JsonSerializer.Serialize(payload);
        Logger.Debug("Serialized encoded polyline with stop list");
        return json;
    }

    /// <summary>Path points for drawing (decoded polyline or stop-to-stop segments).</summary>
    public static IReadOnlyList<(double Latitude, double Longitude)> Parse(string? json) =>
        ParsePayload(json).Points;

    public static IReadOnlyList<(double Latitude, double Longitude)> ParseStops(string? json) =>
        ParsePayload(json).Stops;

    public static RouteWaypointPayload ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyPayload();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                string? encoded = null;
                if (doc.RootElement.TryGetProperty("encodedPolyline", out var encodedEl) &&
                    encodedEl.ValueKind == JsonValueKind.String)
                {
                    encoded = encodedEl.GetString();
                }

                var stops = ReadCoordinateArray(doc.RootElement, "stops");
                var legacyPoints = ReadCoordinateArray(doc.RootElement, "points");
                var decoded = EncodedPolylineCodec.Decode(encoded);
                var path = decoded.Count >= 2
                    ? decoded
                    : legacyPoints.Count > 0
                        ? legacyPoints
                        : stops;
                var stopList = stops.Count > 0
                    ? stops
                    : string.IsNullOrWhiteSpace(encoded)
                        ? legacyPoints
                        : Array.Empty<(double, double)>();

                if (path.Count == 0 && stopList.Count == 0 && string.IsNullOrWhiteSpace(encoded))
                {
                    Logger.Warning("Waypoint object JSON missing stops, points, and encoded polyline");
                }

                return new RouteWaypointPayload(stopList, path, encoded);
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                Logger.Warning("Waypoint JSON was not an array");
                return EmptyPayload();
            }

            var compact = ParseArray(doc.RootElement);
            return new RouteWaypointPayload(compact, compact, EncodedPolyline: null);
        }
        catch (JsonException ex)
        {
            Logger.Warning(ex, "Waypoint JSON parse failed");
            return EmptyPayload();
        }
    }

    private static RouteWaypointPayload EmptyPayload() =>
        new(Array.Empty<(double, double)>(), Array.Empty<(double, double)>(), EncodedPolyline: null);

    private static IReadOnlyList<(double Latitude, double Longitude)> ReadCoordinateArray(
        JsonElement root,
        string name)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array)
        {
            return ParseArray(el);
        }

        return Array.Empty<(double, double)>();
    }

    private static IReadOnlyList<(double Latitude, double Longitude)> ParseArray(JsonElement array)
    {
        var list = new List<(double, double)>();
        foreach (var el in array.EnumerateArray())
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    if (el.TryGetProperty("Latitude", out var latProp) &&
                        el.TryGetProperty("Longitude", out var lonProp) &&
                        latProp.TryGetDouble(out var lat) &&
                        lonProp.TryGetDouble(out var lon))
                    {
                        list.Add((lat, lon));
                    }

                    break;
                case JsonValueKind.Array when el.GetArrayLength() >= 2:
                    list.Add((el[0].GetDouble(), el[1].GetDouble()));
                    break;
            }
        }

        Logger.Debug("Parsed {Count} waypoints from JSON", list.Count);
        return list;
    }
}
