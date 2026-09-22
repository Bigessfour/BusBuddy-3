using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using BusBuddy.Core.Mapping;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.ViewModels.Map;

/// <summary>
/// Syncfusion ImageryLayer marker model. Template DataContext is <c>CustomDataSymbol</c>;
/// bind UI as <c>{Binding Data.DisplayCaption}</c> / <c>Data.MarkerSize</c> / <c>Data.FillBrush</c>.
/// </summary>
public sealed class MapMarker : INotifyPropertyChanged
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    private string? _label;
    private string? _routeStopLabel;
    private MapMarkerLabels.Kind _kind = MapMarkerLabels.Kind.Student;
    private double _markerSize = MapMarkerLabels.PrimaryMarkerSize;
    private double _labelFontSize = MapMarkerLabels.PrimaryLabelFontSize;
    private bool _showCaption = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Internal caption with kind prefix (merge / diagnostics). UI binds <see cref="DisplayCaption"/>.</summary>
    public string? Label
    {
        get => _label;
        set
        {
            if (!string.Equals(_label, value, StringComparison.Ordinal))
            {
                _label = value;
                Raise(nameof(Label));
                Raise(nameof(Caption));
                Raise(nameof(DisplayCaption));
            }
        }
    }

    /// <summary>Clean name for Syncfusion MarkerTemplate (no SCH/PK/HOME prefixes).</summary>
    public string Caption => MapMarkerLabels.CaptionFrom(Label);

    /// <summary>
    /// Sequence tag ("Start", "Stop 7", "End") when the selected route stops at this pin. Set instead of
    /// stacking a second waypoint marker on the same spot — that is what overprints the first caption.
    /// </summary>
    public string? RouteStopLabel
    {
        get => _routeStopLabel;
        set
        {
            var clean = string.IsNullOrWhiteSpace(value) ? null : MapMarkerLabels.CaptionFrom(value);
            if (!string.Equals(_routeStopLabel, clean, StringComparison.Ordinal))
            {
                _routeStopLabel = clean;
                Raise(nameof(RouteStopLabel));
                Raise(nameof(DisplayCaption));
                Raise(nameof(StrokeBrush));
            }
        }
    }

    /// <summary>Caption as drawn: "School name (Stop 7)" when the route stops here, else <see cref="Caption"/>.</summary>
    public string DisplayCaption => MapMarkerLabels.DisplayCaption(Caption, RouteStopLabel);

    /// <summary>Pin fill by kind (schools black, stops orange, homes blue, …). Frozen, shared per colour.</summary>
    public SolidColorBrush FillBrush => BrushFor(MapMarkerLabels.FillHex(Kind));

    /// <summary>Pin outline; switches to route gold when the selected route stops at this pin.</summary>
    public SolidColorBrush StrokeBrush =>
        BrushFor(RouteStopLabel is null ? MapMarkerLabels.StrokeHex(Kind) : MapMarkerLabels.WaypointFillHex);

    public double MarkerSize
    {
        get => _markerSize;
        set
        {
            if (Math.Abs(_markerSize - value) > 0.01)
            {
                _markerSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerSize)));
            }
        }
    }

    public double LabelFontSize
    {
        get => _labelFontSize;
        set
        {
            if (Math.Abs(_labelFontSize - value) > 0.01)
            {
                _labelFontSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LabelFontSize)));
            }
        }
    }

    /// <summary>When false, template shows pin only (tooltip still has full label).</summary>
    public bool ShowCaption
    {
        get => _showCaption;
        set
        {
            if (_showCaption != value)
            {
                _showCaption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCaption)));
            }
        }
    }

    public MapMarkerLabels.Kind Kind
    {
        get => _kind;
        set
        {
            if (_kind != value)
            {
                _kind = value;
                Raise(nameof(Kind));
                Raise(nameof(FillBrush));
                Raise(nameof(StrokeBrush));
            }
        }
    }

    /// <summary>Syncfusion ImageryLayer marker latitude (official N/S string).</summary>
    public string Latitude { get; set; } = "0.0000N";

    /// <summary>Syncfusion ImageryLayer marker longitude (official E/W string).</summary>
    public string Longitude { get; set; } = "0.0000E";

    public double LatitudeDegrees { get; set; }

    public double LongitudeDegrees { get; set; }

    public List<string> StudentNames { get; } = new();

    /// <summary>Roster keys at this pin so the clerk override can call Core without matching names.</summary>
    public List<int> StudentIds { get; } = new();

    public void ApplyZoomVisuals(int zoomLevel)
    {
        MarkerSize = MapMarkerLabels.ScaledMarkerSize(Kind, zoomLevel);
        LabelFontSize = MapMarkerLabels.ScaledLabelFontSize(Kind, zoomLevel);
        ShowCaption = MapMarkerLabels.ShowsCaption(Kind, zoomLevel)
            && !string.IsNullOrWhiteSpace(DisplayCaption);
    }

    private void Raise(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static SolidColorBrush BrushFor(string hex) =>
        BrushCache.GetOrAdd(hex, static h =>
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));
            brush.Freeze();
            return brush;
        });

    public static MapMarker FromDegrees(
        double latitude,
        double longitude,
        string? label = null,
        MapMarkerLabels.Kind? kind = null,
        int zoomLevel = MapDefaults.DistrictZoomLevel)
    {
        var resolved = kind ?? MapMarkerLabels.GetKind(label);
        var marker = new MapMarker
        {
            Label = label,
            Kind = resolved,
            LatitudeDegrees = latitude,
            LongitudeDegrees = longitude,
            Latitude = MapCoordinateFormatter.FormatLatitude(latitude),
            Longitude = MapCoordinateFormatter.FormatLongitude(longitude)
        };
        marker.ApplyZoomVisuals(zoomLevel);
        return marker;
    }

    /// <summary>
    /// Adds a student name to this marker. Only unlabeled <see cref="MapMarkerLabels.Kind.Student"/>
    /// markers rewrite <see cref="Label"/> for aggregation; typed kinds keep their prefix label.
    /// </summary>
    public void AddStudent(string name, int? studentId = null)
    {
        if (studentId is > 0 && !StudentIds.Contains(studentId.Value))
        {
            StudentIds.Add(studentId.Value);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!StudentNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            StudentNames.Add(name);
        }

        if (Kind != MapMarkerLabels.Kind.Student)
        {
            return;
        }

        if (StudentNames.Count == 1)
        {
            Label = StudentNames[0];
            return;
        }

        var preview = string.Join(", ", StudentNames.Take(3));
        Label = StudentNames.Count > 3
            ? $"{StudentNames.Count} students: {preview} +{StudentNames.Count - 3} more"
            : $"{StudentNames.Count} students: {preview}";
    }
}
