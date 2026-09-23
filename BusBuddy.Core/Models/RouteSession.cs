namespace BusBuddy.Core.Models;

/// <summary>
/// Published daily-route session. Core already keys AM and PM as two route rows
/// (for example <c>Draft-School-cell-1</c> and <c>Draft-School-cell-1-PM</c>).
/// Do not add a second session structure alongside this name.
/// </summary>
public static class RouteSession
{
    public const string AM = "AM";
    public const string PM = "PM";
    public const string Transfer = "Transfer";
    public const string SpecialNeeds = "SpecialNeeds";

    public static readonly string[] All = { AM, PM, Transfer, SpecialNeeds };

    public static string[] GetAll() => All;

    public static bool IsKnown(string? session) => Canonical(session) is not null;

    /// <summary>
    /// Canonical spelling from <see cref="All"/>, or null when the value is not a session.
    /// One pass so a known-check cannot succeed and then fail to find a row.
    /// </summary>
    public static string? Canonical(string? session)
    {
        if (string.IsNullOrWhiteSpace(session))
        {
            return null;
        }

        foreach (var known in All)
        {
            if (string.Equals(known, session, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return null;
    }

    /// <summary>
    /// Names the session from existing two-row keying plus special-needs / transfer flags.
    /// Does not invent a parallel AM/PM stop list.
    /// </summary>
    public static string Infer(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return Infer(route.RouteName, route.IsSpecialNeedsRoute, route.Description);
    }

    public static string Infer(string? routeName, bool isSpecialNeedsRoute, string? description)
    {
        if (isSpecialNeedsRoute
            || (!string.IsNullOrWhiteSpace(routeName)
                && routeName.Contains("special needs", StringComparison.OrdinalIgnoreCase)))
        {
            return SpecialNeeds;
        }

        if (ContainsToken(description, "Transfer") || ContainsToken(routeName, "Transfer"))
        {
            return Transfer;
        }

        if (!string.IsNullOrWhiteSpace(routeName)
            && routeName.EndsWith("-PM", StringComparison.OrdinalIgnoreCase))
        {
            return PM;
        }

        return AM;
    }

    /// <summary>
    /// Student / pairing slot for this row. Special-needs and transfer still use AM or PM
    /// keys; a <c>-PM</c> suffix or <see cref="PM"/> session is the PM row.
    /// </summary>
    public static RouteTimeSlot ToAssignmentSlot(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (string.Equals(route.Session, PM, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(route.RouteName)
                && route.RouteName.EndsWith("-PM", StringComparison.OrdinalIgnoreCase)))
        {
            return RouteTimeSlot.PM;
        }

        return RouteTimeSlot.AM;
    }

    private static bool ContainsToken(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
