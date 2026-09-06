namespace BusBuddy.WPF.Utilities;

/// <summary>
/// District-map marker captions. Prefixes keep school, pickup, waypoint, and student pins
/// from collapsing into one label when coordinates are close.
/// </summary>
internal static class MapMarkerLabels
{
    public const string SchoolPrefix = "School ";
    public const string PickupPrefix = "PK ";
    public const string WaypointPrefix = MapRouteTrail.WaypointPrefix;

    public enum Kind
    {
        School,
        Pickup,
        Waypoint,
        Student
    }

    public static string ForSchool(string? name) =>
        SchoolPrefix + DisplayName(name, "School");

    public static string ForPickup(string? name) =>
        PickupPrefix + DisplayName(name, "Stop");

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

        if (label.StartsWith(SchoolPrefix, StringComparison.Ordinal))
        {
            return Kind.School;
        }

        if (label.StartsWith(PickupPrefix, StringComparison.Ordinal))
        {
            return Kind.Pickup;
        }

        return Kind.Student;
    }

    public static bool CanMerge(Kind existing, Kind incoming) =>
        existing == incoming
        || (existing == Kind.Pickup && incoming == Kind.Student)
        || (existing == Kind.Student && incoming == Kind.Pickup);

    private static string DisplayName(string? name, string fallback) =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
}
