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
        string? districtName = null,
        IReadOnlySet<int>? notRidingStudentIds = null)
    {
        ArgumentNullException.ThrowIfNull(route);

        var roster = (students ?? Array.Empty<Student>()).ToList();
        var ordered = AssignedRouteStops.ForRouting(stops, roster)
            .OrderBy(ArrivalClock)
            .ThenBy(s => s.StopOrder)
            .ThenBy(s => s.StopName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var claimed = new HashSet<int>();
        var ridersByStop = ordered
            .Select(stop => ResolveRiders(stop, roster, claimed))
            .ToList();

        var stopRows = new List<RouteSummarySheet.StopRow>(ordered.Count);
        double? cumulative = null;
        var anyLeg = false;
        RouteStop? previous = null;
        for (var i = 0; i < ordered.Count; i++)
        {
            var stop = ordered[i];
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
            var riders = ridersByStop[i];
            stopRows.Add(new RouteSummarySheet.StopRow(
                stopRows.Count + 1,
                DisplayStopName(stop, riders),
                stop.StopAddress?.Trim() ?? string.Empty,
                FormatClock(ArrivalClock(stop)),
                FormatClock(DepartureClock(stop)),
                milesText,
                cumText,
                FormatRiders(riders.Count > 0 ? riders.Count : CountStudentIds(stop.Notes))));
            previous = stop;
        }

        var studentRows = roster
            .OrderBy(s => s.StudentName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new RouteSummarySheet.StudentRow(
                string.IsNullOrWhiteSpace(s.StudentName) ? "(unnamed)" : s.StudentName.Trim(),
                s.Grade?.Trim() ?? string.Empty,
                MatchStudentStop(s, ordered, ridersByStop),
                notRidingStudentIds is not null && notRidingStudentIds.Contains(s.StudentId)
                    ? "Not riding today"
                    : string.Empty))
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

    internal static int CountStudentIds(string? notes) => ParseStudentIds(notes).Count;

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
            return ClerkSessionLabel(route.Session);
        }

        if (timeSlot == RouteTimeSlot.PM)
        {
            return RouteSession.PM;
        }

        if (timeSlot == RouteTimeSlot.AM)
        {
            return RouteSession.AM;
        }

        return ClerkSessionLabel(RouteSession.Infer(route));
    }

    internal static string ClerkSessionLabel(string session) =>
        string.Equals(session, RouteSession.SpecialNeeds, StringComparison.OrdinalIgnoreCase)
            ? "Special Needs"
            : session;

    private static string DisplayStopName(RouteStop stop, IReadOnlyList<Student> riders)
    {
        var names = riders
            .Select(s => s.StudentName?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count > 0)
        {
            return string.Join(", ", names);
        }

        var stored = stop.StopName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stored) || IsGenericHomeStopName(stored))
        {
            return "(unnamed stop)";
        }

        return stored;
    }

    internal static bool IsGenericHomeStopName(string name)
    {
        var n = name.Trim();
        return n.Equals("Home", StringComparison.OrdinalIgnoreCase)
            || n.Equals("StudentHome", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Student home", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Home pickup", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Student> ResolveRiders(
        RouteStop stop,
        IReadOnlyList<Student> roster,
        HashSet<int> claimed)
    {
        if (roster.Count == 0)
        {
            return Array.Empty<Student>();
        }

        var fromNotes = ParseStudentIds(stop.Notes)
            .Select(id => roster.FirstOrDefault(s => s.StudentId == id))
            .Where(s => s is not null)
            .Cast<Student>()
            .ToList();
        if (fromNotes.Count > 0)
        {
            foreach (var rider in fromNotes)
            {
                claimed.Add(rider.StudentId);
            }

            return fromNotes;
        }

        var matched = new List<Student>();
        foreach (var student in roster)
        {
            if (claimed.Contains(student.StudentId))
            {
                continue;
            }

            if (!StudentMatchesStop(student, stop))
            {
                continue;
            }

            claimed.Add(student.StudentId);
            matched.Add(student);
        }

        return matched;
    }

    internal static IReadOnlyList<int> ParseStudentIds(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return Array.Empty<int>();
        }

        var match = StudentIdNotes.Match(notes);
        if (!match.Success)
        {
            return Array.Empty<int>();
        }

        return match.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    private static bool StudentMatchesStop(Student student, RouteStop stop)
    {
        if (!string.IsNullOrWhiteSpace(student.StudentName)
            && string.Equals(stop.StopName?.Trim(), student.StudentName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var pickup = student.PickupStop?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(pickup)
            && string.Equals(stop.StopName?.Trim(), pickup, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AddressesOverlap(stop.StopAddress, student.HomeAddress);
    }

    private static bool AddressesOverlap(string? stopAddress, string? homeAddress)
    {
        var stop = NormalizeAddress(stopAddress);
        var home = NormalizeAddress(homeAddress);
        if (stop.Length < 6 || home.Length < 6)
        {
            return false;
        }

        return stop.Contains(home, StringComparison.OrdinalIgnoreCase)
            || home.Contains(stop, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string MatchStudentStop(
        Student student,
        IReadOnlyList<RouteStop> stops,
        IReadOnlyList<IReadOnlyList<Student>> ridersByStop)
    {
        for (var i = 0; i < stops.Count; i++)
        {
            if (ridersByStop[i].Any(s => s.StudentId == student.StudentId))
            {
                return DisplayStopName(stops[i], ridersByStop[i]);
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
