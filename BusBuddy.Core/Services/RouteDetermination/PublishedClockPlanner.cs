using System;
using System.Collections.Generic;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>
/// One published-clock plan. Morning walks backward from each confirmed school start.
/// Afternoon walks forward from each dismissal. No database and no Google call.
/// </summary>
public static class PublishedClockPlanner
{
    public static PublishedClockPlan Plan(
        IReadOnlyList<ClockStop> stops,
        IReadOnlyList<int> legSeconds,
        TimeSpan dwell,
        bool afternoon,
        bool estimated)
    {
        if (stops.Count == 0)
        {
            return PublishedClockPlan.Failed("Route has no stops.");
        }

        if (legSeconds.Count != stops.Count - 1)
        {
            return PublishedClockPlan.Failed("Drive legs do not match the stop list.");
        }

        var warnings = new List<string>();
        if (estimated)
        {
            warnings.Add("Clocks use straight-line estimates.");
        }

        if (!afternoon)
        {
            var order = CheckMorningOrder(stops);
            if (order is not null)
            {
                return PublishedClockPlan.Failed(order);
            }
        }

        var arrivals = new TimeSpan?[stops.Count];
        var anchored = false;
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            if (stop.Kind != ClockStopKind.School)
            {
                continue;
            }

            if (stop.Bell is not TimeSpan bell)
            {
                warnings.Add("A school on this route has no confirmed bell.");
                continue;
            }

            if (afternoon && anchored)
            {
                continue;
            }

            if (!TryPullBackward(stops, legSeconds, dwell, arrivals, i, bell, out var error))
            {
                return PublishedClockPlan.Failed(error);
            }

            anchored = true;
        }

        if (!anchored)
        {
            return PublishedClockPlan.Failed("No confirmed school bell.");
        }

        if (!TryFillForward(stops, legSeconds, dwell, arrivals, afternoon, out var fillError))
        {
            return PublishedClockPlan.Failed(fillError);
        }

        var publishedArrivals = new TimeSpan[stops.Count];
        var publishedDepartures = new TimeSpan[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            publishedArrivals[i] = RoundToMinute(arrivals[i]!.Value);
            publishedDepartures[i] = RoundToMinute(Departure(arrivals[i]!.Value, stops[i], dwell));
        }

        var begin = stops[0].Kind == ClockStopKind.Depot
            ? publishedDepartures[0]
            : publishedArrivals[0];

        return new PublishedClockPlan(true, estimated, begin, publishedArrivals, publishedDepartures, warnings);
    }

    private static string? CheckMorningOrder(IReadOnlyList<ClockStop> stops)
    {
        var schoolIds = new HashSet<int>();
        foreach (var stop in stops)
        {
            if (stop.Kind != ClockStopKind.Pickup)
            {
                continue;
            }

            foreach (var schoolId in stop.RiderSchoolIds)
            {
                schoolIds.Add(schoolId);
            }
        }

        foreach (var schoolId in schoolIds)
        {
            var pickupIndexes = new List<int>();
            var schoolIndexes = new List<int>();
            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                if (stop.Kind == ClockStopKind.Pickup && stop.RiderSchoolIds.Contains(schoolId))
                {
                    pickupIndexes.Add(i);
                }

                if (stop.Kind == ClockStopKind.School && stop.SchoolDestinationId == schoolId)
                {
                    schoolIndexes.Add(i);
                }
            }

            if (schoolIndexes.Count == 0 || schoolIndexes[^1] < pickupIndexes[0])
            {
                return "A rider's pickup is ordered after that rider's school.";
            }

            if (schoolIndexes[0] < pickupIndexes[0])
            {
                return "A school is visited before its riders are aboard.";
            }

            if (pickupIndexes[^1] > schoolIndexes[0])
            {
                return "A rider's pickup is ordered after that rider's school.";
            }
        }

        return null;
    }

    private static bool TryPullBackward(
        IReadOnlyList<ClockStop> stops,
        IReadOnlyList<int> legSeconds,
        TimeSpan dwell,
        TimeSpan?[] arrivals,
        int index,
        TimeSpan deadline,
        out string error)
    {
        error = string.Empty;
        if (arrivals[index] is TimeSpan already && already <= deadline)
        {
            return true;
        }

        arrivals[index] = deadline;
        for (var i = index - 1; i >= 0; i--)
        {
            var next = arrivals[i + 1]!.Value;
            var need = next - TimeSpan.FromSeconds(legSeconds[i]);
            if (stops[i].Kind == ClockStopKind.Pickup)
            {
                need -= dwell;
            }

            if (stops[i].Kind == ClockStopKind.School && stops[i].Bell is TimeSpan bell && bell < need)
            {
                need = bell;
            }

            if (need < TimeSpan.Zero)
            {
                error = "Travel exceeds the school bell.";
                return false;
            }

            if (arrivals[i] is TimeSpan have && have <= need)
            {
                break;
            }

            arrivals[i] = need;
        }

        return true;
    }

    private static bool TryFillForward(
        IReadOnlyList<ClockStop> stops,
        IReadOnlyList<int> legSeconds,
        TimeSpan dwell,
        TimeSpan?[] arrivals,
        bool afternoon,
        out string error)
    {
        error = string.Empty;
        for (var i = 0; i < stops.Count; i++)
        {
            if (arrivals[i] is not null)
            {
                continue;
            }

            if (i == 0)
            {
                error = "No clock for the first stop.";
                return false;
            }

            var arrive = Departure(arrivals[i - 1]!.Value, stops[i - 1], dwell) + TimeSpan.FromSeconds(legSeconds[i - 1]);
            if (afternoon
                && stops[i].Kind == ClockStopKind.School
                && stops[i].Bell is TimeSpan bell
                && arrive < bell)
            {
                arrive = bell;
            }

            if (arrive < TimeSpan.Zero)
            {
                error = "Travel exceeds the school bell.";
                return false;
            }

            arrivals[i] = arrive;
        }

        return true;
    }

    private static TimeSpan Departure(TimeSpan arrival, ClockStop stop, TimeSpan dwell) =>
        stop.Kind == ClockStopKind.Pickup ? arrival + dwell : arrival;

    private static TimeSpan RoundToMinute(TimeSpan value) =>
        TimeSpan.FromMinutes(Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero));
}

public enum ClockStopKind
{
    Depot = 0,
    School = 1,
    Pickup = 2
}

/// <summary>One ordered stop already classified. Bell is the morning start or the afternoon dismissal.</summary>
public sealed record ClockStop(
    ClockStopKind Kind,
    int? SchoolDestinationId,
    TimeSpan? Bell,
    IReadOnlyList<int> RiderSchoolIds);

public sealed record PublishedClockPlan(
    bool Success,
    bool Estimated,
    TimeSpan? BeginTime,
    IReadOnlyList<TimeSpan> Arrivals,
    IReadOnlyList<TimeSpan> Departures,
    IReadOnlyList<string> Warnings)
{
    public static PublishedClockPlan Failed(string warning) =>
        new(false, false, null, Array.Empty<TimeSpan>(), Array.Empty<TimeSpan>(), new[] { warning });
}
