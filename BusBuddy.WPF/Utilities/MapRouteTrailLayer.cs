using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Paints the gold route line on the UI thread.
/// Syncfusion redraws only when <c>Points</c> is replaced
/// (<c>OnShapePointsPropertyChanged</c> / <c>MapPolyline.OnPointsChanged</c>).
/// Mutating an existing collection does not rebuild the shape.
/// For <c>ShapeType.Polyline</c>, <c>ShapeFill</c> is the line color
/// (https://help.syncfusion.com/wpf/maps/shapetype).
/// </summary>
internal static class MapRouteTrailLayer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapRouteTrailLayer));

    public static void Apply(MapPolyline? polyline, SubShapeFileLayer? layer, IReadOnlyList<Point> points)
    {
        if (polyline is null && layer is null)
        {
            Logger.Warning("RouteTrail layer not found in view");
            return;
        }

        try
        {
            var draw = points.Count >= 2;

            if (polyline is not null)
            {
                polyline.Stroke = Brushes.Gold;
                polyline.StrokeThickness = 4;
                // New collection: MapPolyline.OnPointsChanged does not see Add/Clear.
                polyline.Points = CopyPoints(points, draw);
            }

            if (layer is not null)
            {
                layer.ShapeType = ShapeType.Polyline;
                layer.ShapeSettings ??= new ShapeSetting();
                // Polyline sample sets ShapeFill, not a transparent fill with a stroke.
                layer.ShapeSettings.ShapeFill = Brushes.Gold;
                layer.ShapeSettings.ShapeStroke = Brushes.Gold;
                layer.ShapeSettings.ShapeStrokeThickness = 4;
                layer.Points = CopyPoints(points, draw);
                ReseatPolyline(layer, polyline);
            }

            if (draw)
            {
                Logger.Information("Route trail polyline updated with {Count} point(s)", points.Count);
            }
            else
            {
                Logger.Information("Route trail polyline cleared ({Count} point(s))", points.Count);
            }

            layer?.Refresh();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed updating route polyline");
        }
    }

    private static ObservableCollection<Point> CopyPoints(IReadOnlyList<Point> points, bool draw)
    {
        var geometry = new ObservableCollection<Point>();
        if (!draw)
        {
            return geometry;
        }

        foreach (var point in points)
        {
            // SfMap Point.X is latitude, Point.Y is longitude.
            geometry.Add(point);
        }

        return geometry;
    }

    /// <summary>
    /// Map element shapes render from the collection change, not from edits inside an element.
    /// </summary>
    private static void ReseatPolyline(SubShapeFileLayer layer, MapPolyline? polyline)
    {
        if (polyline is null || layer.MapElements is null)
        {
            return;
        }

        try
        {
            var seated = false;
            foreach (var element in layer.MapElements)
            {
                if (ReferenceEquals(element, polyline))
                {
                    seated = true;
                    break;
                }
            }

            if (!seated)
            {
                layer.MapElements.Add(polyline);
                return;
            }

            layer.MapElements.Remove(polyline);
            layer.MapElements.Add(polyline);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "MapPolyline reseat skipped");
        }
    }
}
