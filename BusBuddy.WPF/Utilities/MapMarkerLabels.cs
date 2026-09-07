using BusBuddy.Core.Mapping;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// District-map marker captions and kinds. Prefixes are display-only; merge policy
/// uses <see cref="Kind"/> on <c>MapMarker</c>, never cross-kind collapse.
/// Syncfusion: choose visuals with <c>MarkerTemplateSelector</c> (school vs stop templates).
/// </summary>
public static class MapMarkerLabels
{
    public const string SchoolPrefix = "SCH ";
    public const string PickupPrefix = "PK ";
    public const string HomePrefix = "HOME ";
    public const string DepotPrefix = "DEPOT ";
    public const string WaypointPrefix = MapRouteTrail.WaypointPrefix;

    public const double PrimaryMarkerSize = 12;
    public const double HomeMarkerSize = 8;
    public const double PrimaryLabelFontSize = 11;
    public const double HomeLabelFontSize = 9;

    public enum Kind
    {
        School,
        Pickup,
        Home,
        Depot,
        Waypoint,
        Student
    }

    public static string ForSchool(string? name) =>
        SchoolPrefix + DisplayName(name, "School");

    public static string ForPickup(string? name) =>
        PickupPrefix + DisplayName(name, "Stop");

    public static string ForHome(string? name) =>
        HomePrefix + DisplayName(name, "Student");

    public static string ForDepot(string? name) =>
        DepotPrefix + DisplayName(name, "District Bus Barn");

    public static double MarkerSize(Kind kind) =>
        kind == Kind.Home ? HomeMarkerSize : PrimaryMarkerSize;

    public static double LabelFontSize(Kind kind) =>
        kind == Kind.Home ? HomeLabelFontSize : PrimaryLabelFontSize;

    public static Kind GetKind(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return Kind.Student;
        }

        if (label.StartsWith(WaypointPrefix, StringComparison.Ordinal))
        {
            return Kind.Waypoint;
        }

        if (label.StartsWith(SchoolPrefix, StringComparison.Ordinal)
            || label.StartsWith("School ", StringComparison.Ordinal))
        {
            return Kind.School;
        }

        if (label.StartsWith(PickupPrefix, StringComparison.Ordinal))
        {
            return Kind.Pickup;
        }

        if (label.StartsWith(HomePrefix, StringComparison.Ordinal))
        {
            return Kind.Home;
        }

        if (label.StartsWith(DepotPrefix, StringComparison.Ordinal))
        {
            return Kind.Depot;
        }

        return Kind.Student;
    }

    /// <summary>Same kind only — never merge school↔pickup↔home↔depot↔waypoint.</summary>
    public static bool CanMerge(Kind existing, Kind incoming) => existing == incoming;

    public static bool IsSchoolVisual(Kind kind) => kind == Kind.School;

    public static bool ShouldReplaceLabel(string? existing, string? incoming, Kind current, Kind next)
    {
        if (string.IsNullOrWhiteSpace(incoming) || current != next)
        {
            return false;
        }

        return current is Kind.Student or Kind.Waypoint || string.IsNullOrWhiteSpace(existing);
    }

    private static string DisplayName(string? name, string fallback) =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
}
