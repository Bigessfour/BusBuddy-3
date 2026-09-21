using BusBuddy.Core.Models;

namespace BusBuddy.WPF.Utilities;

/// <summary>District Map route combo captions — disambiguate similar names (spec maps toolbar).</summary>
internal static class RouteDistrictMapLabels
{
    public static string SessionLabel(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return string.IsNullOrWhiteSpace(route.Session)
            ? RouteSession.Infer(route)
            : route.Session;
    }

    public static string ListCaption(Route route) =>
        $"{route.RouteName ?? "Route"} · {SessionLabel(route)}";

    public static string ListDetail(Route route) =>
        $"Route id {route.RouteId} — pick this row for stops, roster homes, and trail";
}
