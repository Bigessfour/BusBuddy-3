using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Applies Map Tiles API session to the district imagery layer (Path A) or OSM fail-open.
/// Keeps tile bootstrap out of MapView code-behind growth.
/// </summary>
public static class MapTileBootstrap
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapTileBootstrap));

    public static async Task<bool> TryApplyGoogleTilesAsync(
        GoogleMapTilesImageryLayer layer,
        Border? attributionBorder,
        TextBlock? attributionText,
        SfMap? mapControl,
        IServiceProvider? services)
    {
        ArgumentNullException.ThrowIfNull(layer);

        try
        {
            var tiles = services?.GetService<IGoogleMapTileSessionService>();
            if (tiles is null || !tiles.IsConfigured)
            {
                ApplyOsm(layer, attributionBorder, attributionText);
                return false;
            }

            var session = await tiles
                .GetSessionAsync(MapBasemap.MapTypeRoadmap)
                .ConfigureAwait(true);
            if (session is null || string.IsNullOrWhiteSpace(session.UrlTemplate))
            {
                ApplyOsm(layer, attributionBorder, attributionText);
                return false;
            }

            layer.UseGoogleTiles(session.UrlTemplate);
            SetAttribution(attributionBorder, attributionText, useGoogleMaps: true);
            NudgeZoom(mapControl);
            Logger.Information("District map using Google Map Tiles API roadmap");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed enabling Google Map Tiles — keeping OpenStreetMap");
            ApplyOsm(layer, attributionBorder, attributionText);
            return false;
        }
    }

    public static void ApplyOsm(
        GoogleMapTilesImageryLayer layer,
        Border? attributionBorder,
        TextBlock? attributionText)
    {
        layer.UseOpenStreetMap();
        SetAttribution(attributionBorder, attributionText, useGoogleMaps: false);
    }

    private static void SetAttribution(Border? overlay, TextBlock? text, bool useGoogleMaps)
    {
        if (overlay is not null)
        {
            overlay.Visibility = Visibility.Visible;
        }

        if (text is not null)
        {
            text.Text = useGoogleMaps ? "Google Maps" : "© OpenStreetMap contributors";
            text.FontSize = useGoogleMaps ? 13 : 12;
        }
    }

    private static void NudgeZoom(SfMap? mapControl)
    {
        if (mapControl is null)
        {
            return;
        }

        var zoom = mapControl.ZoomLevel;
        mapControl.ZoomLevel = Math.Max(1, zoom - 1);
        mapControl.ZoomLevel = zoom;
    }
}
