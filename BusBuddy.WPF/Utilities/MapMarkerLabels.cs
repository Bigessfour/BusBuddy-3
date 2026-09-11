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

    public static string ForSchool(string? name) =>
        SchoolPrefix + DisplayName(name, "School");

    public static string ForPickup(string? name) =>
        PickupPrefix + DisplayName(name, "Stop");

    public static string ForHome(string? name) =>
        HomePrefix + DisplayName(name, "Student");

    public static string ForDepot(string? name) =>
        DepotPrefix + DisplayName(name, "District Bus Barn");

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
    /// Captions only from <see cref="MapDefaults.DetailLabelZoomLevel"/> up for all kinds.
    /// Schools/depots still use larger pins; names at county overview dominate the screen.
    /// </summary>
    public static bool ShowsCaption(Kind kind, int zoomLevel) =>
        MapDefaults.ShowsDetailLabels(zoomLevel);

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
