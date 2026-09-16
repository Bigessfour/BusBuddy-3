using System.Linq.Expressions;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Single slot-assignment contract: identity key plus the denormalised name that still mirrors it.
/// Name-only fallback applies only when the key is null (rows the backfill could not attribute).
/// </summary>
public static class StudentRouteAssignment
{
    public static void SetSlot(Student student, RouteTimeSlot slot, Route? route)
    {
        ArgumentNullException.ThrowIfNull(student);
        if (slot == RouteTimeSlot.Both)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Specify AM or PM.");
        }

        if (slot == RouteTimeSlot.AM)
        {
            student.AMRoute = route?.RouteName;
            student.AmRouteId = route?.RouteId;
        }
        else
        {
            student.PMRoute = route?.RouteName;
            student.PmRouteId = route?.RouteId;
        }
    }

    public static bool Matches(Student student, Route route, RouteTimeSlot slot)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(route);
        return slot == RouteTimeSlot.AM
            ? MatchesAm(student, route.RouteId, route.RouteName)
            : MatchesPm(student, route.RouteId, route.RouteName);
    }

    public static bool MatchesAm(Student student, int routeId, string? routeName) =>
        student.AmRouteId == routeId
        || (student.AmRouteId is null && NamesEqual(student.AMRoute, routeName));

    public static bool MatchesPm(Student student, int routeId, string? routeName) =>
        student.PmRouteId == routeId
        || (student.PmRouteId is null && NamesEqual(student.PMRoute, routeName));

    public static bool IsUnassigned(Student student, RouteTimeSlot slot)
    {
        ArgumentNullException.ThrowIfNull(student);
        return slot switch
        {
            RouteTimeSlot.AM => IsUnassignedAm(student),
            RouteTimeSlot.PM => IsUnassignedPm(student),
            RouteTimeSlot.Both => IsUnassignedAm(student) && IsUnassignedPm(student),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown slot.")
        };
    }

    public static bool IsUnassignedAm(Student student) =>
        student.AmRouteId is null && string.IsNullOrWhiteSpace(student.AMRoute);

    public static bool IsUnassignedPm(Student student) =>
        student.PmRouteId is null && string.IsNullOrWhiteSpace(student.PMRoute);

    public static bool IsAssignedAny(Student student) =>
        !IsUnassignedAm(student) || !IsUnassignedPm(student);

    public static bool MatchesEither(Student student, Route route) =>
        Matches(student, route, RouteTimeSlot.AM) || Matches(student, route, RouteTimeSlot.PM);

    public static Expression<Func<Student, bool>> OnAmRoute(int routeId, string routeName) =>
        s => s.AmRouteId == routeId || (s.AmRouteId == null && s.AMRoute == routeName);

    public static Expression<Func<Student, bool>> OnPmRoute(int routeId, string routeName) =>
        s => s.PmRouteId == routeId || (s.PmRouteId == null && s.PMRoute == routeName);

    public static Expression<Func<Student, bool>> UnassignedAm() =>
        s => s.AmRouteId == null && (s.AMRoute == null || s.AMRoute == "");

    public static Expression<Func<Student, bool>> UnassignedPm() =>
        s => s.PmRouteId == null && (s.PMRoute == null || s.PMRoute == "");

    public static IQueryable<Student> WhereOnSlot(
        this IQueryable<Student> query,
        int routeId,
        string routeName,
        RouteTimeSlot slot) =>
        slot == RouteTimeSlot.AM
            ? query.Where(OnAmRoute(routeId, routeName))
            : query.Where(OnPmRoute(routeId, routeName));

    public static IQueryable<Student> WhereOnRoute(this IQueryable<Student> query, Route route)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(route);
        var routeId = route.RouteId;
        var routeName = route.RouteName;
        return query.Where(s =>
            s.AmRouteId == routeId || (s.AmRouteId == null && s.AMRoute == routeName)
            || s.PmRouteId == routeId || (s.PmRouteId == null && s.PMRoute == routeName));
    }

    /// <summary>
    /// Students on any route that currently uses <paramref name="routeName"/>. Keyed riders win;
    /// name match is only for rows the backfill could not attribute.
    /// </summary>
    public static Expression<Func<Student, bool>> OnNamedRoutes(
        List<int> routeIds,
        string routeName)
    {
        ArgumentNullException.ThrowIfNull(routeIds);
        return s =>
            (s.AmRouteId != null && routeIds.Contains(s.AmRouteId.Value))
            || (s.PmRouteId != null && routeIds.Contains(s.PmRouteId.Value))
            || (s.AmRouteId == null && s.AMRoute == routeName)
            || (s.PmRouteId == null && s.PMRoute == routeName);
    }

    private static bool NamesEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
