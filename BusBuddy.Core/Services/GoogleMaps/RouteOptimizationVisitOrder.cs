namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Maps clerk stop lists onto Route Optimization <c>ShipmentModel</c> without rewriting
/// published times. First and last stops stay pinned (depot/school); the solver only
/// permutes intermediate pickups.
/// </summary>
public static class RouteOptimizationVisitOrder
{
    public static OptimizeToursProblem ForPinnedEnds(
        IReadOnlyList<RouteOptimizationStop> orderedStops,
        int seatingCapacity,
        DateTime utcDay)
    {
        ArgumentNullException.ThrowIfNull(orderedStops);
        if (orderedStops.Count < 3)
        {
            throw new ArgumentException("Need origin, at least one intermediate, and destination.", nameof(orderedStops));
        }

        var start = orderedStops[0];
        var end = orderedStops[^1];
        var middles = orderedStops.Skip(1).Take(orderedStops.Count - 2).ToList();
        return new OptimizeToursProblem
        {
            Vehicles =
            [
                new RouteOptimizationVehicle
                {
                    Label = "route",
                    StartLatitude = start.Latitude,
                    StartLongitude = start.Longitude,
                    EndLatitude = end.Latitude,
                    EndLongitude = end.Longitude,
                    Capacity = Math.Max(1, seatingCapacity),
                },
            ],
            Shipments = middles.Select(stop => new RouteOptimizationShipment
            {
                Label = stop.Label,
                Pickup = stop,
                Load = Math.Max(1, stop.Load),
            }).ToList(),
            GlobalStartUtc = DateTime.SpecifyKind(utcDay.Date, DateTimeKind.Utc),
            GlobalEndUtc = DateTime.SpecifyKind(utcDay.Date.AddHours(16), DateTimeKind.Utc),
        };
    }

    public static OptimizeToursProblem ForPickupRun(
        RouteOptimizationStop start,
        IReadOnlyList<RouteOptimizationStop> pickups,
        RouteOptimizationStop end,
        int seatingCapacity,
        DateTime utcDay)
    {
        ArgumentNullException.ThrowIfNull(pickups);
        var ordered = new List<RouteOptimizationStop>(pickups.Count + 2) { start };
        ordered.AddRange(pickups);
        ordered.Add(end);
        return ForPinnedEnds(ordered, seatingCapacity, utcDay);
    }

    /// <summary>
    /// PM dropoffs: pin school as start and depot as end. Intermediate homes are the only
    /// stops the solver may permute. Do not reuse <see cref="ForPickupRun"/>'s depot→school
    /// topology — PM ETAs walk school → homes.
    /// </summary>
    public static OptimizeToursProblem ForDropoffRun(
        RouteOptimizationStop school,
        IReadOnlyList<RouteOptimizationStop> dropoffs,
        RouteOptimizationStop depot,
        int seatingCapacity,
        DateTime utcDay)
        => ForPickupRun(school, dropoffs, depot, seatingCapacity, utcDay);

    public static OptimizeToursProblem ForSameDayTrips(
        IReadOnlyList<RouteOptimizationShipment> trips,
        IReadOnlyList<RouteOptimizationVehicle> vehicles,
        DateTime utcDay)
    {
        ArgumentNullException.ThrowIfNull(trips);
        ArgumentNullException.ThrowIfNull(vehicles);
        if (trips.Count == 0 || vehicles.Count == 0)
        {
            throw new ArgumentException("Need at least one trip shipment and one vehicle.");
        }

        return new OptimizeToursProblem
        {
            Shipments = trips,
            Vehicles = vehicles,
            GlobalStartUtc = DateTime.SpecifyKind(utcDay.Date, DateTimeKind.Utc),
            GlobalEndUtc = DateTime.SpecifyKind(utcDay.Date.AddHours(18), DateTimeKind.Utc),
            Timeout = "15s",
        };
    }

    /// <summary>
    /// Rebuilds stop labels as start + optimizer pickup order + end. Unknown or skipped
    /// intermediates keep their original relative order at the tail of the middle section.
    /// </summary>
    public static IReadOnlyList<string> MergePinnedOrder(
        IReadOnlyList<string> originalLabels,
        IReadOnlyList<OptimizedVisit> visits)
    {
        ArgumentNullException.ThrowIfNull(originalLabels);
        if (originalLabels.Count < 3)
        {
            return originalLabels;
        }

        var start = originalLabels[0];
        var end = originalLabels[^1];
        var middle = originalLabels.Skip(1).Take(originalLabels.Count - 2).ToList();
        var middleSet = middle.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var orderedMiddle = new List<string>();
        foreach (var visit in visits)
        {
            if (!visit.IsPickup)
            {
                continue;
            }

            if (!middleSet.Contains(visit.ShipmentLabel) || !seen.Add(visit.ShipmentLabel))
            {
                continue;
            }

            orderedMiddle.Add(visit.ShipmentLabel);
        }

        foreach (var label in middle)
        {
            if (seen.Add(label))
            {
                orderedMiddle.Add(label);
            }
        }

        var merged = new List<string>(originalLabels.Count) { start };
        merged.AddRange(orderedMiddle);
        merged.Add(end);
        return merged;
    }
}
