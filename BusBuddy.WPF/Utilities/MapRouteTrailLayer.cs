using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Mutates the XAML-hosted gold <see cref="MapPolyline"/> on the UI thread.
/// </summary>
internal static class MapRouteTrailLayer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapRouteTrailLayer));

    public static void Apply(MapPolyline? polyline, SubShapeFileLayer? layer, IReadOnlyList<Point> points)
    {
        if (polyline is null)
        {
            Logger.Warning("RouteTrail MapPolyline not found in view");
            return;
        }

        try
        {
            polyline.Stroke ??= Brushes.Gold;
            if (polyline.StrokeThickness <= 0)
            {
                polyline.StrokeThickness = 3;
            }

            polyline.Points ??= new ObservableCollection<Point>();
            polyline.Points.Clear();
            if (points.Count >= 2)
            {
                foreach (var point in points)
                {
                    polyline.Points.Add(point);
                }
            }

            layer?.Refresh();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed updating route polyline");
        }
    }
}
