using System.Globalization;
using System.Text.RegularExpressions;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Clerk-facing projection for a route sheet. Sorts stops by clock time, numbers
/// them 1..n, and takes header departure/arrival from the stop list — not
/// <see cref="Route.EstimatedDuration"/>.
/// </summary>
public sealed class RouteSummaryDocument
{
    private static readonly Regex GenerateSlug = new(
        @"^(Draft[-_])|(_\d{10,})|(-R\d+C\d+(-\d+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public required string DisplayName { get; init; }
    public required string School { get; init; }
    public required string SlotLabel { get; init; }
    public required string ServiceDate { get; init; }
    public required string DriverLabel { get; init; }
    public required string BusLabel { get; init; }
    public required string DepartureText { get; init; }
    public required string ArrivalText { get; init; }
    public required string TotalMilesText { get; init; }
    public required IReadOnlyList<StopRow> Stops { get; init; }
    public required IReadOnlyList<StudentRow> Students { get; init; }

    public sealed record StopRow(
        int Sequence,
        string Name,
        string Address,
        string Arrival,
        string Departure,
        string Miles,
        string Cumulative);

    public sealed record StudentRow(string Name, string Grade, string Address);

    public static RouteSummaryDocument From(
        Route route,
        IEnumerable<RouteStop>? stops,
        IEnumerable<Student>? students,
        Bus? bus,
        Driver? driver,
        RouteTimeSlot slot)
    {
        ArgumentNullException.ThrowIfNull(route);

        var ordered = (stops ?? Array.Empty<RouteStop>())
            .OrderBy(s => ArrivalClock(s))
            .ThenBy(s => s.StopOrder)
            .ThenBy(s => s.StopName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var stopRows = new List<StopRow>(ordered.Count);
        double? cumulative = 0;
        var anyLeg = false;
        RouteStop? previous = null;
        foreach (var stop in ordered)
        {
            string milesText = "—";
            if (previous is not null
                && previous.HasValidatedCoordinates
                && stop.HasValidatedCoordinates)
            {
                var leg = HaversineMiles(
                    (double)previous.Latitude!.Value,
                    (double)previous.Longitude!.Value,
                    (double)stop.Latitude!.Value,
                    (double)stop.Longitude!.Value);
                milesText = leg.ToString("0.0", CultureInfo.InvariantCulture);
                cumulative = (cumulative ?? 0) + leg;
                anyLeg = true;
            }
            else
            {
                cumulative = anyLeg ? cumulative : null;
            }

            var cumText = cumulative is double cum
                ? cum.ToString("0.0", CultureInfo.InvariantCulture)
                : "—";

            stopRows.Add(new StopRow(
                stopRows.Count + 1,
                string.IsNullOrWhiteSpace(stop.StopName) ? "(unnamed stop)" : stop.StopName.Trim(),
                stop.StopAddress?.Trim() ?? string.Empty,
                FormatClock(ArrivalClock(stop)),
                FormatClock(DepartureClock(stop)),
                milesText,
                cumText));
            previous = stop;
        }

        var roster = (students ?? Array.Empty<Student>())
            .OrderBy(s => s.StudentName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new StudentRow(
                string.IsNullOrWhiteSpace(s.StudentName) ? "(unnamed)" : s.StudentName.Trim(),
                s.Grade?.Trim() ?? string.Empty,
                s.HomeAddress?.Trim() ?? string.Empty))
            .ToList();

        var departure = ordered.Count > 0
            ? FormatClock(ArrivalClock(ordered[0]))
            : FormatOptional(slot == RouteTimeSlot.PM ? route.PMBeginTime : route.AMBeginTime);
        var arrival = ordered.Count > 0
            ? FormatClock(ArrivalClock(ordered[^1]))
            : "—";

        string totalMiles;
        if (anyLeg && cumulative is double total)
        {
            totalMiles = total.ToString("0.0", CultureInfo.InvariantCulture);
        }
        else if (route.Distance is decimal dist && dist > 0)
        {
            totalMiles = dist.ToString("0.0", CultureInfo.InvariantCulture);
        }
        else
        {
            totalMiles = "—";
        }

        var busNumber = bus?.BusNumber ?? route.BusNumber;
        var driverName = driver?.DriverName ?? route.DriverName;

        return new RouteSummaryDocument
        {
            DisplayName = DisplayNameFor(route),
            School = string.IsNullOrWhiteSpace(route.School) ? "—" : route.School.Trim(),
            SlotLabel = SlotText(slot, route.Session),
            ServiceDate = route.Date == default
                ? DateTime.Today.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                : route.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
            DriverLabel = string.IsNullOrWhiteSpace(driverName) ? "Unassigned" : driverName.Trim(),
            BusLabel = string.IsNullOrWhiteSpace(busNumber) ? "Unassigned" : $"Bus {busNumber.Trim()}",
            DepartureText = departure,
            ArrivalText = arrival,
            TotalMilesText = totalMiles,
            Stops = stopRows,
            Students = roster
        };
    }

    public static string DisplayNameFor(Route route)
    {
        var raw = route.RouteName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.IsNullOrWhiteSpace(route.School) ? "Unnamed route" : route.School.Trim();
        }

        if (!LooksGenerated(raw))
        {
            return raw;
        }

        if (!string.IsNullOrWhiteSpace(route.School))
        {
            return route.School.Trim();
        }

        var cleaned = raw;
        if (cleaned.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[6..];
        }

        cleaned = Regex.Replace(cleaned, @"_\d{8,}.*$", string.Empty);
        cleaned = Regex.Replace(cleaned, @"-R\d+C\d+(-\d+)?$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? raw : cleaned;
    }

    internal static bool LooksGenerated(string name) =>
        name.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase)
        || GenerateSlug.IsMatch(name)
        || Regex.IsMatch(name, @"-R\d+C\d+", RegexOptions.IgnoreCase);

    private static TimeSpan ArrivalClock(RouteStop stop)
    {
        if (stop.ScheduledArrival != default)
        {
            return stop.ScheduledArrival;
        }

        if (stop.EstimatedArrivalTime != default)
        {
            return stop.EstimatedArrivalTime.TimeOfDay;
        }

        return default;
    }

    private static TimeSpan DepartureClock(RouteStop stop)
    {
        if (stop.ScheduledDeparture != default)
        {
            return stop.ScheduledDeparture;
        }

        return ArrivalClock(stop);
    }

    private static string FormatClock(TimeSpan value)
    {
        if (value == default)
        {
            return "—";
        }

        var hours = value.Hours;
        var minutes = value.Minutes;
        var suffix = hours >= 12 ? "PM" : "AM";
        var hour12 = hours % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{hour12}:{minutes:00} {suffix}");
    }

    private static string FormatOptional(TimeSpan? value) =>
        value is TimeSpan t && t != default ? FormatClock(t) : "—";

    private static string SlotText(RouteTimeSlot slot, string? session)
    {
        if (slot is RouteTimeSlot.AM or RouteTimeSlot.PM)
        {
            return slot.ToString();
        }

        return string.IsNullOrWhiteSpace(session) ? "AM" : session.Trim();
    }

    internal static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthMiles = 3958.8;
        static double Rad(double deg) => deg * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + (Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2));
        return earthMiles * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
