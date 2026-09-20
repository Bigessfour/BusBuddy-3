using BusBuddy.Core.Models;
using BusBuddy.Core.Services.RouteDetermination;

namespace BusBuddy.Core.Services;

/// <summary>
/// Writes clerk-facing <see cref="RouteStop.ScheduledArrival"/> / <see cref="RouteStop.ScheduledDeparture"/>
/// from a run start plus real travel, not a dwell-only staircase.
/// Travel minutes come from the last Google Routes drive path when present; otherwise Haversine at
/// <see cref="FallbackSpeedMph"/>.
/// </summary>
public static class PublishedStopClockPlanner
{
    public const int DefaultDwellMinutes = 2;
    public const double FallbackSpeedMph = 25.0;

    public sealed record Plan(
        TimeSpan FirstArrival,
        TimeSpan LastArrival,
        TimeSpan LastDeparture,
        int TravelMinutes,
        int DwellMinutes,
        int StopCount,
        string TravelSource);

    /// <summary>
    /// Mutates <paramref name="stops"/> in StopOrder. First arrival is <paramref name="startOfRun"/>.
    /// Each stop then dwells; the next arrival is that departure plus that leg's share of travel.
    /// </summary>
    public static Plan Apply(
        IEnumerable<RouteStop> stops,
        TimeSpan startOfRun,
        int? pathTravelMinutes,
        DateTime? stampUtc = null,
        double fallbackMph = FallbackSpeedMph)
    {
        ArgumentNullException.ThrowIfNull(stops);
        var ordered = stops.OrderBy(s => s.StopOrder).ToList();
        if (ordered.Count == 0)
        {
            return new Plan(startOfRun, startOfRun, startOfRun, 0, 0, 0, "None");
        }

        var mph = fallbackMph <= 0 ? FallbackSpeedMph : fallbackMph;
        var legMiles = LegMiles(ordered);
        var haversineMinutes = HaversineTravelMinutes(legMiles, mph);
        var travelSource = pathTravelMinutes is > 0 ? "PathDuration" : "HaversineFallback";
        var travelMinutes = pathTravelMinutes is > 0 ? pathTravelMinutes.Value : haversineMinutes;
        var legSeconds = AllocateSeconds(legMiles, travelMinutes * 60);

        var runDate = DateTime.SpecifyKind((stampUtc ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        var stamped = stampUtc ?? DateTime.UtcNow;
        var cursor = startOfRun;
        var dwellTotal = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            var stop = ordered[i];
            var dwellMinutes = stop.StopDuration > 0 ? stop.StopDuration : DefaultDwellMinutes;
            dwellTotal += dwellMinutes;
            var dwell = TimeSpan.FromMinutes(dwellMinutes);
            var arrival = cursor;

            stop.ScheduledArrival = RoundToMinute(arrival);
            stop.ScheduledDeparture = RoundToMinute(arrival + dwell);
            stop.EstimatedArrivalTime = runDate + stop.ScheduledArrival;
            stop.EstimatedDepartureTime = runDate + stop.ScheduledDeparture;
            stop.UpdatedDate = stamped;

            if (i < ordered.Count - 1)
            {
                cursor = arrival + dwell + TimeSpan.FromSeconds(legSeconds[i]);
            }
        }

        var last = ordered[^1];
        return new Plan(
            ordered[0].ScheduledArrival,
            last.ScheduledArrival,
            last.ScheduledDeparture,
            travelMinutes,
            dwellTotal,
            ordered.Count,
            travelSource);
    }

    internal static int[] AllocateSeconds(IReadOnlyList<double> weights, int totalSeconds)
    {
        var n = weights.Count;
        var result = new int[n];
        if (n == 0 || totalSeconds <= 0)
        {
            return result;
        }

        var sum = 0.0;
        for (var i = 0; i < n; i++)
        {
            sum += Math.Max(0, weights[i]);
        }

        if (sum <= 0)
        {
            var baseSec = totalSeconds / n;
            var rem = totalSeconds % n;
            for (var i = 0; i < n; i++)
            {
                result[i] = baseSec + (i < rem ? 1 : 0);
            }

            return result;
        }

        var exact = new double[n];
        var assigned = 0;
        for (var i = 0; i < n; i++)
        {
            exact[i] = totalSeconds * (Math.Max(0, weights[i]) / sum);
            result[i] = (int)Math.Floor(exact[i]);
            assigned += result[i];
        }

        var leftover = totalSeconds - assigned;
        var order = Enumerable.Range(0, n)
            .OrderByDescending(i => exact[i] - result[i])
            .ThenBy(i => i)
            .ToArray();
        for (var k = 0; k < leftover; k++)
        {
            result[order[k % n]]++;
        }

        return result;
    }

    private static double[] LegMiles(IReadOnlyList<RouteStop> ordered)
    {
        var legs = new double[Math.Max(0, ordered.Count - 1)];
        for (var i = 0; i < legs.Length; i++)
        {
            var a = ordered[i];
            var b = ordered[i + 1];
            if (a.Latitude is not decimal aLat ||
                a.Longitude is not decimal aLon ||
                b.Latitude is not decimal bLat ||
                b.Longitude is not decimal bLon)
            {
                continue;
            }

            legs[i] = RoutePacker.HaversineMiles(
                (double)aLat,
                (double)aLon,
                (double)bLat,
                (double)bLon);
        }

        return legs;
    }

    private static int HaversineTravelMinutes(IReadOnlyList<double> legMiles, double mph)
    {
        var miles = 0.0;
        for (var i = 0; i < legMiles.Count; i++)
        {
            miles += legMiles[i];
        }

        if (miles <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Round(miles / mph * 60.0));
    }

    private static TimeSpan RoundToMinute(TimeSpan value) =>
        TimeSpan.FromMinutes(Math.Max(0, Math.Round(value.TotalMinutes)));
}
