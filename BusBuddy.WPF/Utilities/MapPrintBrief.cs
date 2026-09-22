using BusBuddy.WPF.ViewModels.Map;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// What the District Map print page says beside the picture: where to start, the stop order, and the road instructions.
/// </summary>
internal sealed class MapPrintBrief
{
    public string Title { get; init; } = "Route map";

    public string Subtitle { get; init; } = "Published path — not live tracking";

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    public static MapPrintBrief Create(
        string? routeName,
        string? busLabel,
        IEnumerable<MapMarker> markers,
        IReadOnlyList<string>? directions)
    {
        var title = string.IsNullOrWhiteSpace(routeName) ? "District map" : routeName.Trim();
        var subtitle = string.IsNullOrWhiteSpace(busLabel)
            ? "Published path — not live tracking. Begin at Start."
            : $"{busLabel.Trim()} · Published path — not live tracking. Begin at Start.";

        var lines = new List<string>();
        var stops = OrderStops(markers).ToList();
        if (stops.Count > 0)
        {
            lines.Add("Stops");
            for (var i = 0; i < stops.Count; i++)
            {
                var name = stops[i].Name;
                if (i == 0)
                {
                    lines.Add($"Start — {name}");
                }
                else if (i == stops.Count - 1)
                {
                    lines.Add($"End — {name}");
                }
                else
                {
                    lines.Add($"{i + 1}. {name}");
                }
            }
        }

        var steps = (directions ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
        if (steps.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Follow the route");
            for (var i = 0; i < steps.Count; i++)
            {
                lines.Add($"{i + 1}. {steps[i]}");
            }
        }
        else if (stops.Count >= 2)
        {
            lines.Add(string.Empty);
            lines.Add("Follow the stops in order from Start to End.");
        }

        return new MapPrintBrief
        {
            Title = title,
            Subtitle = subtitle,
            Lines = lines
        };
    }

    private static IEnumerable<(string Name, int Rank)> OrderStops(IEnumerable<MapMarker> markers)
    {
        return markers
            .Select((marker, index) => (marker, index))
            .Where(x => x.marker.Kind == MapMarkerLabels.Kind.Waypoint
                || !string.IsNullOrWhiteSpace(x.marker.RouteStopLabel))
            .Select(x =>
            {
                var name = string.IsNullOrWhiteSpace(x.marker.DisplayCaption)
                    ? x.marker.Caption
                    : x.marker.DisplayCaption;
                return (Name: name.Trim(), Rank: StopRank(x.marker.RouteStopLabel, x.index));
            })
            .Where(x => x.Name.Length > 0)
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Select(g => g.OrderBy(x => x.Rank).First())
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Name, StringComparer.Ordinal);
    }

    private static int StopRank(string? routeStopLabel, int index)
    {
        var label = routeStopLabel?.Trim() ?? string.Empty;
        if (label.Equals("Start", StringComparison.OrdinalIgnoreCase))
        {
            return -1_000_000;
        }

        if (label.Equals("End", StringComparison.OrdinalIgnoreCase))
        {
            return 1_000_000;
        }

        const string stopPrefix = "Stop ";
        if (label.StartsWith(stopPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(label[stopPrefix.Length..], out var number))
        {
            return number;
        }

        return 10_000 + index;
    }
}
