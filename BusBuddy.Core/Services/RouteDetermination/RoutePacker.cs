using BusBuddy.Core.Configuration;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>One packed route proposal within a density cell.</summary>
public sealed class PackedRoute
{
    public string CellId { get; init; } = string.Empty;
    public List<int> OrderedStudentIds { get; } = new();
    public int SeatingCapacity { get; init; }
    public double EstimatedMiles { get; set; }
    public double EstimatedMinutes { get; set; }
}

/// <summary>
/// Greedy seating packer with outlier gap splits (hard capacity = bus seating).
/// </summary>
public static class RoutePacker
{
    public static IReadOnlyList<PackedRoute> PackCell(
        DensityCell cell,
        int seatingCapacity,
        RoutingDistrictSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(settings);

        var capacity = Math.Max(1, seatingCapacity);
        if (cell.Riders.Count == 0)
        {
            return Array.Empty<PackedRoute>();
        }

        // Nearest-neighbor order from cell centroid
        var centroidLat = cell.Riders.Average(r => r.Latitude);
        var centroidLon = cell.Riders.Average(r => r.Longitude);
        var ordered = cell.Riders
            .OrderBy(r => HaversineMiles(centroidLat, centroidLon, r.Latitude, r.Longitude))
            .ToList();

        var routes = new List<PackedRoute>();
        var current = NewRoute(cell.CellId, capacity);
        RiderPoint? previous = null;

        foreach (var rider in ordered)
        {
            var gapMinutes = previous is null
                ? 0
                : EstimateMinutes(previous.Value, rider, settings.AverageSpeedMph);

            var wouldExceedGap = previous is not null && gapMinutes > settings.MaxPickupGapMinutes;
            var wouldExceedSeats = current.OrderedStudentIds.Count >= capacity;

            if ((wouldExceedSeats || wouldExceedGap) && current.OrderedStudentIds.Count > 0)
            {
                FinalizeEstimates(current, cell.Riders, settings.AverageSpeedMph);
                routes.Add(current);
                current = NewRoute(cell.CellId, capacity);
                previous = null;
            }

            current.OrderedStudentIds.Add(rider.StudentId);
            previous = rider;
        }

        if (current.OrderedStudentIds.Count > 0)
        {
            FinalizeEstimates(current, cell.Riders, settings.AverageSpeedMph);
            routes.Add(current);
        }

        return routes;
    }

    private static PackedRoute NewRoute(string cellId, int capacity) =>
        new() { CellId = cellId, SeatingCapacity = capacity };

    private static void FinalizeEstimates(
        PackedRoute route,
        IReadOnlyList<RiderPoint> allRiders,
        double avgMph)
    {
        var byId = allRiders.ToDictionary(r => r.StudentId);
        double miles = 0;
        RiderPoint? prev = null;
        foreach (var id in route.OrderedStudentIds)
        {
            if (!byId.TryGetValue(id, out var pt))
            {
                continue;
            }

            if (prev is not null)
            {
                miles += HaversineMiles(prev.Value.Latitude, prev.Value.Longitude, pt.Latitude, pt.Longitude);
            }

            prev = pt;
        }

        route.EstimatedMiles = Math.Round(miles, 2);
        route.EstimatedMinutes = avgMph <= 0 ? 0 : Math.Round(miles / avgMph * 60.0, 1);
    }

    private static double EstimateMinutes(RiderPoint a, RiderPoint b, double avgMph)
    {
        if (avgMph <= 0)
        {
            return 0;
        }

        var miles = HaversineMiles(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
        return miles / avgMph * 60.0;
    }

    /// <summary>Great-circle distance in miles.</summary>
    public static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3958.8;
        static double ToRad(double d) => d * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
