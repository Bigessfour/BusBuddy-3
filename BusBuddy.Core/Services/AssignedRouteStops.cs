using System.Globalization;
using System.Text.RegularExpressions;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.RouteDetermination;

namespace BusBuddy.Core.Services;

/// <summary>
/// A student stop is on the drive path, the map, and the printed sheet only when that
/// student is assigned to the route. School, depot, and catalog stops stay when the
/// place is still an active catalog location. A stop named for a school that is not
/// an active destination is omitted. Generated stops that still carry <c>StudentId</c>
/// notes stay when the route has no roster yet.
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
        IEnumerable<Student>? assignedStudents,
        IEnumerable<Destination>? schools = null)
    {
        var roster = (assignedStudents ?? Array.Empty<Student>()).ToList();
        var catalog = schools?.ToList();
        return (stops ?? Array.Empty<RouteStop>()).Where(stop => IsRoutable(stop, roster, catalog)).ToList();
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

    public static bool IsRoutable(RouteStop stop, IReadOnlyList<Student> assignedStudents) =>
        IsRoutable(stop, assignedStudents, null);

    public static bool IsRoutable(
        RouteStop stop,
        IReadOnlyList<Student> assignedStudents,
        IReadOnlyList<Destination>? schools)
    {
        ArgumentNullException.ThrowIfNull(stop);
        assignedStudents ??= Array.Empty<Student>();
        if (IsUnlistedSchoolStop(stop, assignedStudents, schools))
        {
            return false;
        }

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

    /// <summary>Stop name matches a school row in the destination catalog, active or not.</summary>
    public static bool NamesCatalogSchool(RouteStop stop, IEnumerable<Destination>? schools)
    {
        ArgumentNullException.ThrowIfNull(stop);
        var name = stop.StopName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || schools is null)
        {
            return false;
        }

        return schools.Any(school =>
            DestinationTypes.IsSchool(school.DestinationType)
            && string.Equals(school.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when the stop names a school that is inactive or deleted, or a school
    /// none of the assigned riders attend when the roster names its schools.
    /// </summary>
    public static bool IsUnlistedSchoolStop(
        RouteStop stop,
        IReadOnlyList<Student> assignedStudents,
        IReadOnlyList<Destination>? schools)
    {
        if (schools is null || schools.Count == 0 || !NamesCatalogSchool(stop, schools))
        {
            return false;
        }

        var name = stop.StopName?.Trim();
        var active = schools.Where(school =>
            DestinationTypes.IsSchool(school.DestinationType)
            && school.IsActive
            && !school.IsDeleted
            && school.HasValidatedCoordinates
            && string.Equals(school.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (active.Count == 0)
        {
            return true;
        }

        if (!assignedStudents.Any(s => s.DestinationId.HasValue || !string.IsNullOrWhiteSpace(s.School)))
        {
            return false;
        }

        return !active.Any(school => Attends(assignedStudents, school));
    }

    private static bool Attends(IReadOnlyList<Student> students, Destination school) =>
        students.Any(student =>
            (student.DestinationId.HasValue && student.DestinationId.Value == school.DestinationId)
            || string.Equals(student.School?.Trim(), school.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

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
