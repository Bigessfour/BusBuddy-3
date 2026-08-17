using System.Globalization;
using System.Text;
using System.Text.Json;
using Serilog;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Compact waypoint JSON [[lat,lon], ...] used by Route.WaypointsJson and SfMap polylines.
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

    public static IReadOnlyList<(double Latitude, double Longitude)> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<(double, double)>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                Logger.Warning("Waypoint JSON was not an array");
                return Array.Empty<(double, double)>();
            }

            var list = new List<(double, double)>();
            foreach (var el in doc.RootElement.EnumerateArray())
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
        catch (JsonException ex)
        {
            Logger.Warning(ex, "Waypoint JSON parse failed");
            return Array.Empty<(double, double)>();
        }
    }
}
