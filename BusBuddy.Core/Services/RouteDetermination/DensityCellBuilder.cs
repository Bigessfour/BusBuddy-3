using BusBuddy.Core.Configuration;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>Student point used by density / packing.</summary>
public readonly record struct RiderPoint(int StudentId, double Latitude, double Longitude);

/// <summary>One density grid cell and its riders.</summary>
public sealed class DensityCell
{
    public string CellId { get; init; } = string.Empty;
    public int Row { get; init; }
    public int Col { get; init; }
    public List<RiderPoint> Riders { get; } = new();
}

/// <summary>
/// Builds an N-cell density grid over a bbox (settings or rider extent) — Q3:B.
/// </summary>
public static class DensityCellBuilder
{
    public static IReadOnlyList<DensityCell> Build(
        IEnumerable<RiderPoint> riders,
        RoutingDistrictSettings settings)
    {
        ArgumentNullException.ThrowIfNull(riders);
        ArgumentNullException.ThrowIfNull(settings);

        var withCoords = riders
            .Where(r => IsFinite(r.Latitude) && IsFinite(r.Longitude))
            .ToList();

        if (withCoords.Count == 0)
        {
            return Array.Empty<DensityCell>();
        }

        var (minLat, maxLat, minLon, maxLon) = ResolveBbox(withCoords, settings);
        var target = Math.Max(1, settings.TargetRidersPerCell);
        var cellCountHint = Math.Max(1, (int)Math.Ceiling(withCoords.Count / (double)target));
        var gridSide = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(cellCountHint)));

        var latSpan = Math.Max(maxLat - minLat, 1e-6);
        var lonSpan = Math.Max(maxLon - minLon, 1e-6);
        var cells = new Dictionary<(int Row, int Col), DensityCell>();

        foreach (var rider in withCoords)
        {
            var row = Math.Min(gridSide - 1, (int)((rider.Latitude - minLat) / latSpan * gridSide));
            var col = Math.Min(gridSide - 1, (int)((rider.Longitude - minLon) / lonSpan * gridSide));
            row = Math.Max(0, row);
            col = Math.Max(0, col);
            var key = (row, col);
            if (!cells.TryGetValue(key, out var cell))
            {
                cell = new DensityCell
                {
                    CellId = $"R{row}C{col}",
                    Row = row,
                    Col = col
                };
                cells[key] = cell;
            }

            cell.Riders.Add(rider);
        }

        return cells.Values.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
    }

    private static (double MinLat, double MaxLat, double MinLon, double MaxLon) ResolveBbox(
        IReadOnlyList<RiderPoint> riders,
        RoutingDistrictSettings settings)
    {
        if (settings.BoundingBoxMinLat is double minLatCfg &&
            settings.BoundingBoxMaxLat is double maxLatCfg &&
            settings.BoundingBoxMinLon is double minLonCfg &&
            settings.BoundingBoxMaxLon is double maxLonCfg &&
            maxLatCfg > minLatCfg &&
            maxLonCfg > minLonCfg)
        {
            return (minLatCfg, maxLatCfg, minLonCfg, maxLonCfg);
        }

        return (
            riders.Min(r => r.Latitude),
            riders.Max(r => r.Latitude),
            riders.Min(r => r.Longitude),
            riders.Max(r => r.Longitude));
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
