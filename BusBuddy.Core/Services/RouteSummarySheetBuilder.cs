using System.Globalization;
using System.Text.RegularExpressions;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Assembles a <see cref="RouteSummarySheet"/> from published stop clocks and the
/// keyed roster. Header times never use <see cref="Route.EstimatedDuration"/> or
/// UTC <c>Estimated*</c> DateTimes.
/// </summary>
public static class RouteSummarySheetBuilder
{
    public const string GenerateStopsOnlyMessage =
        "Generate created pickup stops only; no Student.AmRouteId / PmRouteId assignments on this route.";

    private static readonly Regex StudentIdNotes = new(
        @"StudentIds?\s*=\s*([0-9]+(?:\s*,\s*[0-9]+)*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static RouteSummarySheet Build(
        Route route,
        IEnumerable<RouteStop>? stops,
        IEnumerable<Student>? students,
        Bus? bus,
        Driver? driver,
        RouteTimeSlot timeSlot,
        string? districtName = null)
    {
        ArgumentNullException.ThrowIfNull(route);

        var ordered = (stops ?? Array.Empty<RouteStop>())
            .OrderBy(ArrivalClock)
            .ThenBy(s => s.StopOrder)
            .ThenBy(s => s.StopName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var stopRows = new List<RouteSummarySheet.StopRow>(ordered.Count);
        double? cumulative = null;
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
                milesText = FormatMiles(leg);
                cumulative = (cumulative ?? 0) + leg;
                anyLeg = true;
            }

            var cumText = cumulative is double cum ? FormatMiles(cum) : "—";
            stopRows.Add(new RouteSummarySheet.StopRow(
                stopRows.Count + 1,
                string.IsNullOrWhiteSpace(stop.StopName) ? "(unnamed stop)" : stop.StopName.Trim(),
                stop.StopAddress?.Trim() ?? string.Empty,
                FormatClock(ArrivalClock(stop)),
                FormatClock(DepartureClock(stop)),
                milesText,
                cumText,
                FormatRiders(CountStudentIds(stop.Notes))));
            previous = stop;
        }

        var roster = (students ?? Array.Empty<Student>()).ToList();
        var studentRows = roster
            .OrderBy(s => s.StudentName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new RouteSummarySheet.StudentRow(
                string.IsNullOrWhiteSpace(s.StudentName) ? "(unnamed)" : s.StudentName.Trim(),
                s.Grade?.Trim() ?? string.Empty,
                MatchStudentStop(s, ordered)))
            .ToList();

        var session = DisplaySession(route, timeSlot);
        var school = string.IsNullOrWhiteSpace(route.School) ? "—" : route.School.Trim();
        var district = FirstNonEmpty(route.School, districtName, "District");
        var generated = LooksGenerated(route.RouteName);

        string totalMiles;
        if (route.Distance is decimal dist && dist > 0)
        {
            totalMiles = FormatMiles((double)dist);
        }
        else if (anyLeg && cumulative is double total)
        {
            totalMiles = FormatMiles(total);
        }
        else
        {
            totalMiles = "—";
        }

        var busNumber = bus?.BusNumber ?? route.BusNumber;
        var driverName = driver?.DriverName ?? route.DriverName;
        var generateNote = roster.Count == 0 && ordered.Any(s => CountStudentIds(s.Notes) > 0)
            ? GenerateStopsOnlyMessage
            : null;

        return new RouteSummarySheet
        {
            Title = $"{district} {session} Route Sheet",
            DisplayName = DisplayNameFor(route),
            FullRouteName = string.IsNullOrWhiteSpace(route.RouteName) ? string.Empty : route.RouteName.Trim(),
            IsGeneratedName = generated,
            School = school,
            District = district,
            SessionLabel = session,
            ServiceDate = route.Date == default
                ? DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : route.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DriverLabel = string.IsNullOrWhiteSpace(driverName) ? "Unassigned" : driverName.Trim(),
            BusLabel = string.IsNullOrWhiteSpace(busNumber) ? "Unassigned" : $"Bus {busNumber.Trim()}",
            DepartureText = ordered.Count > 0 ? FormatClock(ArrivalClock(ordered[0])) : "—",
            ArrivalText = ordered.Count > 0 ? FormatClock(ArrivalClock(ordered[^1])) : "—",
            TotalMilesText = totalMiles,
            RosterCount = roster.Count,
            GenerateStopsOnlyNote = generateNote,
            Stops = stopRows,
            Students = studentRows
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

        var shortLabel = ShortGeneratedLabel(raw);
        if (!string.IsNullOrWhiteSpace(route.School))
        {
            return string.IsNullOrWhiteSpace(shortLabel)
                ? route.School.Trim()
                : $"{shortLabel} · {route.School.Trim()}";
        }

        return string.IsNullOrWhiteSpace(shortLabel) ? raw : shortLabel;
    }

    internal static bool LooksGenerated(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase)
            || name.Contains("_R0C0-", StringComparison.OrdinalIgnoreCase)
            || name.Contains("-R0C0-", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(name, @"-R\d+C\d+", RegexOptions.IgnoreCase);
    }

    internal static int CountStudentIds(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return 0;
        }

        var match = StudentIdNotes.Match(notes);
        if (!match.Success)
        {
            return 0;
        }

        return match.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
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

    private static string ShortGeneratedLabel(string raw)
    {
        var cleaned = raw;
        if (cleaned.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[6..];
        }

        cleaned = Regex.Replace(cleaned, @"_\d{8,}.*$", string.Empty);
        cleaned = Regex.Replace(cleaned, @"-R\d+C\d+(-\d+)?$", string.Empty, RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace('_', ' ').Trim();
        return cleaned;
    }

    private static TimeSpan ArrivalClock(RouteStop stop) =>
        stop.ScheduledArrival != default ? stop.ScheduledArrival : default;

    private static TimeSpan DepartureClock(RouteStop stop) =>
        stop.ScheduledDeparture != default ? stop.ScheduledDeparture : ArrivalClock(stop);

    private static string FormatClock(TimeSpan value)
    {
        if (value == default)
        {
            return "—";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours:00}:{value.Minutes:00}");
    }

    private static string FormatMiles(double miles) =>
        miles.ToString("0.0", CultureInfo.InvariantCulture);

    private static string FormatRiders(int count) =>
        count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "—";

    private static string DisplaySession(Route route, RouteTimeSlot timeSlot)
    {
        if (RouteSession.IsKnown(route.Session))
        {
            return route.Session;
        }

        if (timeSlot == RouteTimeSlot.PM)
        {
            return RouteSession.PM;
        }

        if (timeSlot == RouteTimeSlot.AM)
        {
            return RouteSession.AM;
        }

        return RouteSession.Infer(route);
    }

    private static string MatchStudentStop(Student student, IReadOnlyList<RouteStop> stops)
    {
        var pickup = student.PickupStop?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(pickup))
        {
            var byPickup = stops.FirstOrDefault(s =>
                string.Equals(s.StopName, pickup, StringComparison.OrdinalIgnoreCase));
            if (byPickup is not null)
            {
                return byPickup.StopName;
            }

            return pickup;
        }

        if (!string.IsNullOrWhiteSpace(student.HomeAddress))
        {
            var byAddress = stops.FirstOrDefault(s =>
                !string.IsNullOrWhiteSpace(s.StopAddress)
                && s.StopAddress.Contains(student.HomeAddress.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byAddress is not null)
            {
                return byAddress.StopName;
            }
        }

        if (!string.IsNullOrWhiteSpace(student.StudentName))
        {
            var byName = stops.FirstOrDefault(s =>
                string.Equals(s.StopName, student.StudentName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName.StopName;
            }
        }

        return "—";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "District";
    }
}
