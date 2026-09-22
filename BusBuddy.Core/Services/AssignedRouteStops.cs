using System.Globalization;
using System.Text.RegularExpressions;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.RouteDetermination;

namespace BusBuddy.Core.Services;

/// <summary>
/// A student stop is on the drive path, the map, and the printed sheet only when that
/// student is assigned to the route. School, depot, and catalog stops stay.
/// Generated stops that still carry <c>StudentId</c> notes stay when the route has no roster yet.
/// </summary>
public static class AssignedRouteStops
{
    /// <summary>Home GPS within this distance of the stop counts as the same pickup.</summary>
    public const double MatchMeters = 100;

    private static readonly Regex StudentToken = new(
        @"(^|[^A-Za-z])students?([^A-Za-z]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<RouteStop> ForRouting(
        IEnumerable<RouteStop>? stops,
        IEnumerable<Student>? assignedStudents)
    {
        var roster = (assignedStudents ?? Array.Empty<Student>()).ToList();
        return (stops ?? Array.Empty<RouteStop>()).Where(stop => IsRoutable(stop, roster)).ToList();
    }

    /// <summary>
    /// Keeps an optimized visit order and appends stops the optimizer did not visit,
    /// so a full-list reorder still has one id per saved stop.
    /// </summary>
    public static List<int> OrderPreservingUnroutable(
        IReadOnlyList<RouteStop> allStops,
        IReadOnlyList<int> routableOrder)
    {
        ArgumentNullException.ThrowIfNull(allStops);
        ArgumentNullException.ThrowIfNull(routableOrder);
        var placed = routableOrder.ToHashSet();
        var held = allStops
            .OrderBy(s => s.StopOrder)
            .ThenBy(s => s.RouteStopId)
            .Select(s => s.RouteStopId)
            .Where(id => id > 0 && !placed.Contains(id));
        return routableOrder.Concat(held).ToList();
    }

    public static bool IsRoutable(RouteStop stop, IReadOnlyList<Student> assignedStudents)
    {
        ArgumentNullException.ThrowIfNull(stop);
        assignedStudents ??= Array.Empty<Student>();
        if (!IsStudentStop(stop))
        {
            return true;
        }

        var ids = RouteSummarySheetBuilder.ParseStudentIds(stop.Notes);
        if (ids.Count > 0)
        {
            // Generate writes StudentId notes before AmRouteId / PmRouteId exist.
            if (assignedStudents.Count == 0)
            {
                return true;
            }

            return ids.Any(id => assignedStudents.Any(s => s.StudentId == id));
        }

        return assignedStudents.Any(student => Matches(student, stop));
    }

    /// <summary>
    /// Notes name a student, the label is a home pickup, or the name itself says "student"
    /// (seed rows such as <c>TEST_STUDENT_SN_01</c>).
    /// </summary>
    public static bool IsStudentStop(RouteStop stop)
    {
        ArgumentNullException.ThrowIfNull(stop);
        if (RouteSummarySheetBuilder.ParseStudentIds(stop.Notes).Count > 0)
        {
            return true;
        }

        if (RouteSummarySheetBuilder.IsGenericHomeStopName(stop.StopName ?? string.Empty))
        {
            return true;
        }

        return HasStudentToken(stop.StopName);
    }

    internal static bool HasStudentToken(string? name) =>
        !string.IsNullOrWhiteSpace(name) && StudentToken.IsMatch(name);

    private static bool Matches(Student student, RouteStop stop)
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

        if (AddressesOverlap(stop.StopAddress, student.HomeAddress))
        {
            return true;
        }

        return CoordinatesMatch(student, stop);
    }

    private static bool CoordinatesMatch(Student student, RouteStop stop)
    {
        if (!student.HasValidatedHomeCoordinates || !stop.HasValidatedCoordinates)
        {
            return false;
        }

        var miles = RoutePacker.HaversineMiles(
            (double)student.Latitude!.Value,
            (double)student.Longitude!.Value,
            (double)stop.Latitude!.Value,
            (double)stop.Longitude!.Value);
        return miles * 1609.344 <= MatchMeters;
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
}
