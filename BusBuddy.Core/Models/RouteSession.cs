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

    public static bool IsKnown(string? session) =>
        !string.IsNullOrWhiteSpace(session)
        && All.Any(s => string.Equals(s, session, StringComparison.OrdinalIgnoreCase));

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

    private static bool ContainsToken(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
