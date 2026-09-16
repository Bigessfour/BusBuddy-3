using BusBuddy.Core.Mapping;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// District-map marker captions and kinds. Prefixes are for kind detection / merge only;
/// UI templates bind <c>Caption</c> (clean name). Syncfusion MarkerTemplate uses screen pixels —
/// sizes refresh with zoom via <see cref="ScaledMarkerSize"/>.
/// </summary>
public static class MapMarkerLabels
{
    public const string SchoolPrefix = "SCH ";
    public const string PickupPrefix = "PK ";
    public const string HomePrefix = "HOME ";
    public const string DepotPrefix = "DEPOT ";
    public const string WaypointPrefix = MapRouteTrail.WaypointPrefix;

    /// <summary>Base pin diameter (DIP) at <see cref="MapDefaults.DetailLabelZoomLevel"/>.</summary>
    public const double PrimaryMarkerSize = 10;

    public const double HomeMarkerSize = 6;

    public const double PrimaryLabelFontSize = 10;

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

    /// <summary>One legend row: clerk-facing name plus the pin colour for that kind.</summary>
    public readonly record struct LegendEntry(Kind Kind, string Name, string FillHex);

    // Pin colours — one per kind so a mixed district plot reads without a tooltip.
    // Schools are black per clerk request; the rest are distinct hues on both Fluent themes.
    public const string SchoolFillHex = "#000000";
    public const string PickupFillHex = "#F28C28";   // orange — catalog / published boarding point
    public const string HomeFillHex = "#5B8DEF";     // blue — student home
    public const string StudentFillHex = "#2E9E5B";  // green — student at their plotted pickup
    public const string DepotFillHex = "#7B3FE4";    // purple — bus barn
    public const string WaypointFillHex = "#D4A017"; // gold — matches the route polyline

    /// <summary>Pin fill by kind (hex, so Core/tests stay free of System.Windows.Media).</summary>
    public static string FillHex(Kind kind) => kind switch
    {
        Kind.School => SchoolFillHex,
        Kind.Pickup => PickupFillHex,
        Kind.Home => HomeFillHex,
        Kind.Depot => DepotFillHex,
        Kind.Waypoint => WaypointFillHex,
        _ => StudentFillHex,
    };

    /// <summary>Pin outline: white ring on the black school pin, otherwise dark for contrast on tiles.</summary>
    public static string StrokeHex(Kind kind) => kind == Kind.School ? "#FFFFFF" : "#1F1F1F";

    /// <summary>Legend rows in display order (schools first, per-household kinds last).</summary>
    public static IReadOnlyList<LegendEntry> Legend { get; } =
    [
        new(Kind.School, "School", SchoolFillHex),
        new(Kind.Pickup, "Pickup stop", PickupFillHex),
        new(Kind.Waypoint, "Route stop", WaypointFillHex),
        new(Kind.Depot, "Bus barn", DepotFillHex),
        new(Kind.Student, "Student at stop", StudentFillHex),
        new(Kind.Home, "Student home", HomeFillHex),
    ];

    public static string ForSchool(string? name) =>
        SchoolPrefix + DisplayName(name, "School");

    public static string ForPickup(string? name) =>
        PickupPrefix + DisplayName(name, "Stop");

    public static string ForHome(string? name) =>
        HomePrefix + DisplayName(name, "Student");

    public static string ForDepot(string? name) =>
        DepotPrefix + DisplayName(name, "District Bus Barn");

    /// <summary>Published route stop by name (Kind.Waypoint) — sequence labels come from <see cref="MapRouteTrail.MarkerLabel"/>.</summary>
    public static string ForRouteStop(string? name) =>
        WaypointPrefix + DisplayName(name, "Stop");

    /// <summary>Screen caption without SCH/PK/HOME/… prefixes (Syncfusion MarkerTemplate text).</summary>
    public static string CaptionFrom(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        var text = label.Trim();
        foreach (var prefix in new[]
                 {
                     SchoolPrefix, PickupPrefix, HomePrefix, DepotPrefix, WaypointPrefix, "School "
                 })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].TrimStart();
                break;
            }
        }

        return text;
    }

    /// <summary>
    /// "Lamar High School (Stop 7)" — one caption per spot instead of two pins stacked on each other.
    /// A route stop named after the pin it sits on ("Ada" on Ada's home) adds nothing, so the caption stays as is.
    /// </summary>
    public static string DisplayCaption(string caption, string? routeStopLabel)
    {
        if (string.IsNullOrWhiteSpace(routeStopLabel))
        {
            return caption;
        }

        var tag = routeStopLabel.Trim();
        if (string.IsNullOrWhiteSpace(caption))
        {
            return tag;
        }

        return string.Equals(caption.Trim(), tag, StringComparison.OrdinalIgnoreCase)
            ? caption
            : $"{caption} ({tag})";
    }

    public static double MarkerSize(Kind kind) =>
        kind == Kind.Home ? HomeMarkerSize : PrimaryMarkerSize;

    public static double LabelFontSize(Kind kind) =>
        kind == Kind.Home ? HomeLabelFontSize : PrimaryLabelFontSize;

    /// <summary>
    /// Syncfusion marker templates are fixed screen pixels (not geographic). Scale with zoom so
    /// county overview stays compact and street zoom stays readable.
    /// </summary>
    public static double ZoomScale(int zoomLevel)
    {
        var z = MapDefaults.ClampZoom(zoomLevel);
        // Screen-pixel markers do not shrink with the basemap. Steeper curve so county overview
        // stays compact (zoom ~8–11) and street zoom stays readable (zoom ≥12).
        // 0.40 at z=6 → 1.0 at DetailLabelZoomLevel (12) → 1.45 at z=16+.
        return Math.Clamp(0.40 + ((z - 6) * 0.10), 0.40, 1.45);
    }

    public static double ScaledMarkerSize(Kind kind, int zoomLevel) =>
        Math.Round(MarkerSize(kind) * ZoomScale(zoomLevel), 1);

    public static double ScaledLabelFontSize(Kind kind, int zoomLevel) =>
        Math.Round(LabelFontSize(kind) * ZoomScale(zoomLevel), 1);

    /// <summary>
    /// Captions from <see cref="MapDefaults.DetailLabelZoomLevel"/> up for place kinds (school, stop,
    /// depot, route stop). Per-household kinds (home, student) wait for
    /// <see cref="MapDefaults.HomeLabelZoomLevel"/> so a town view does not stack names on top of each other.
    /// </summary>
    public static bool ShowsCaption(Kind kind, int zoomLevel) =>
        IsPerHousehold(kind)
            ? MapDefaults.ShowsHomeLabels(zoomLevel)
            : MapDefaults.ShowsDetailLabels(zoomLevel);

    public static bool IsPerHousehold(Kind kind) => kind is Kind.Home or Kind.Student;

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
