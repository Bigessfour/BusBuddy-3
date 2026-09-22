using System;
using System.Windows;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// District Map camera: Google Map Tiles 2D viewport is latitude, longitude, and integer zoom
/// (<see href="https://developers.google.com/maps/documentation/tile/2d-tiles-overview"/>).
/// Syncfusion <see cref="ImageryLayer.Center"/> is <c>Point(latitude, longitude)</c>
/// (docs: ImageryLayer.Center). Zoom is <see cref="SfMap.ZoomLevel"/> (clamped 1–19), not ZoomFactor.
/// Setting ZoomLevel calls ImageryLayer.ZoomMap, which pans LatLonPoint to zoomPointPosition.
/// That point is (0,0) for a toolbar zoom and stale on the wheel's first tick, so the district
/// leaves the viewport. The view holds MapCenter across that notification and writes it back.
/// Do not bind Center in XAML and do not use Radius fit — both fight this camera and throw or zoom-loop.
/// </summary>
public static class MapCameraHost
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapCameraHost));

    /// <summary>Pixels of mouse travel that count as a pan, not a pin click.</summary>
    public const double ClickDragThresholdPx = 6;

    /// <summary>Syncfusion ImageryLayer.Center: X = latitude, Y = longitude.</summary>
    public static (double Latitude, double Longitude) ToLatLon(Point center) => (center.X, center.Y);

    public static Point FromLatLon(double latitude, double longitude) => new(latitude, longitude);

    public static bool IsClickNotDrag(Point mouseDown, Point mouseUp) =>
        Math.Abs(mouseUp.X - mouseDown.X) <= ClickDragThresholdPx
        && Math.Abs(mouseUp.Y - mouseDown.Y) <= ClickDragThresholdPx;

    /// <summary>
    /// Interprets a Syncfusion geo <see cref="Point"/>. <c>GetLatLonFromPoint</c> is X=lon Y=lat;
    /// <see cref="ImageryLayer.Center"/> is X=lat Y=lon.
    /// </summary>
    public static bool TryInterpretLatLon(double first, double second, out double latitude, out double longitude) =>
        LocationCoordinate.TryInterpretLatLon(first, second, out latitude, out longitude);

    /// <summary>
    /// Click (not drag) on a pick map → validated lat/lng. Do not recenter the camera after this;
    /// pan/zoom stay with EnablePan/EnableZoom.
    /// </summary>
    public static bool TryReadClick(
        ImageryLayer? layer,
        Point mouseDown,
        Point mouseUp,
        out double latitude,
        out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (layer is null || !IsClickNotDrag(mouseDown, mouseUp))
        {
            return false;
        }

        Point geo;
        try
        {
            geo = layer.GetLatLonFromPoint(mouseUp);
        }
        catch (Exception)
        {
            return false;
        }

        return TryInterpretLatLon(geo.X, geo.Y, out latitude, out longitude);
    }

    /// <summary>
    /// Applies zoom then center once the layer can <c>TransformToVisual</c> the map.
    /// Returns false so the view can retry (same gate as pins).
    /// </summary>
    public static bool TryApply(SfMap? map, ImageryLayer? layer, Point centerLatLon, int zoomLevel)
    {
        if (!MapMarkerHost.CanHost(map, layer) || map is null || layer is null)
        {
            return false;
        }

        var zoom = MapDefaults.ClampZoom(zoomLevel);
        var zoomChanged = map.ZoomLevel != zoom;
        var centerChanged = layer.Center != centerLatLon;
        if (zoomChanged)
        {
            map.ZoomLevel = zoom;
        }

        if (centerChanged)
        {
            layer.Center = centerLatLon;
        }

        if (zoomChanged || centerChanged)
        {
            var (lat, lon) = ToLatLon(layer.Center);
            Logger.Information(
                "Map camera applied Lat={Lat:F4} Lon={Lon:F4} Zoom={Zoom}",
                lat,
                lon,
                map.ZoomLevel);
        }

        return true;
    }
}
