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
/// Keeps tile bootstrap out of MapView code-behind growth. The layer owns its own reload;
/// this class never touches <c>SfMap.ZoomLevel</c> (a zoom "nudge" fires ZoomedIn/Out and two tile fetches).
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
                ApplyOsm(layer, attributionBorder, attributionText, mapControl);
                return false;
            }

            var session = await tiles
                .GetSessionAsync(MapBasemap.MapTypeRoadmap)
                .ConfigureAwait(true);
            if (session is null || string.IsNullOrWhiteSpace(session.UrlTemplate))
            {
                Logger.Warning("Map Tiles session unavailable — using OpenStreetMap");
                ApplyOsm(layer, attributionBorder, attributionText, mapControl);
                return false;
            }

            layer.UseGoogleTiles(session.UrlTemplate);
            SetAttribution(attributionBorder, attributionText, useGoogleMaps: true);
            Logger.Information("District map using Google Map Tiles API roadmap");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed enabling Google Map Tiles — keeping OpenStreetMap");
            ApplyOsm(layer, attributionBorder, attributionText, mapControl);
            return false;
        }
    }

    public static void ApplyOsm(
        GoogleMapTilesImageryLayer layer,
        Border? attributionBorder,
        TextBlock? attributionText,
        SfMap? mapControl = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        layer.UseOpenStreetMap();
        SetAttribution(attributionBorder, attributionText, useGoogleMaps: false);
    }

    /// <summary>
    /// Map Tiles API Policies require the viewport <c>copyright</c> string (e.g. "Map data ©2026 Google")
    /// to be shown for the tiles on screen. Call after the camera settles; no-op while OSM is active.
    /// Returns the text applied, or null when the static label was kept.
    /// </summary>
    public static async Task<string?> RefreshGoogleAttributionAsync(
        GoogleMapTilesImageryLayer layer,
        TextBlock? attributionText,
        SfMap? mapControl,
        IServiceProvider? services)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!layer.IsGoogleTilesActive || attributionText is null || mapControl is null)
        {
            return null;
        }

        try
        {
            var tiles = services?.GetService<IGoogleMapTileSessionService>();
            if (tiles is null || !tiles.IsConfigured)
            {
                return null;
            }

            var center = layer.Center;
            var zoom = MapDefaults.ClampZoom(mapControl.ZoomLevel);
            var bounds = MapDefaults.BoundsForViewport(
                center.Y,
                center.X,
                zoom,
                mapControl.ActualWidth,
                mapControl.ActualHeight);

            var copyright = await tiles
                .GetViewportCopyrightAsync(MapBasemap.MapTypeRoadmap, zoom, bounds)
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(copyright) || !layer.IsGoogleTilesActive)
            {
                return null;
            }

            attributionText.Text = copyright;
            return copyright;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Map Tiles viewport attribution refresh failed — keeping static label");
            return null;
        }
    }

    private static void SetAttribution(Border? overlay, TextBlock? text, bool useGoogleMaps)
    {
        if (overlay is not null)
        {
            overlay.Visibility = Visibility.Visible;
        }

        if (text is not null)
        {
            text.Text = useGoogleMaps ? MapBasemap.Attribution : "© OpenStreetMap contributors";
            text.FontSize = useGoogleMaps ? 13 : 12;
        }
    }
}
