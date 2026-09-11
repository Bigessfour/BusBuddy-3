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
/// Applies Map Tiles API session to district / pick-map imagery (Google only; no OSM).
/// </summary>
public static class MapTileBootstrap
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapTileBootstrap));

    public static async Task<bool> TryApplyGoogleTilesAsync(
        GoogleMapTilesImageryLayer layer,
        Border? attributionBorder,
        TextBlock? attributionText,
        SfMap? mapControl,
        IServiceProvider? services,
        System.Windows.Controls.Image? googleLogo = null,
        string host = "DistrictMap")
    {
        ArgumentNullException.ThrowIfNull(layer);

        try
        {
            var tiles = services?.GetService<IGoogleMapTileSessionService>();
            if (tiles is null)
            {
                Logger.Warning(
                    "MapTileBootstrap Host={Host} Outcome=no-service — IGoogleMapTileSessionService not registered",
                    host);
                ApplyUnavailable(layer, attributionBorder, attributionText, googleLogo, "Maps key not configured");
                return false;
            }

            if (!tiles.IsConfigured)
            {
                Logger.Warning(
                    "MapTileBootstrap Host={Host} Outcome=no-key — GOOGLE_MAPS_API_KEY not configured",
                    host);
                ApplyUnavailable(layer, attributionBorder, attributionText, googleLogo, "Maps key not configured");
                return false;
            }

            var session = await tiles
                .GetSessionAsync(MapBasemap.MapTypeRoadmap)
                .ConfigureAwait(true);
            if (session is null || string.IsNullOrWhiteSpace(session.UrlTemplate))
            {
                Logger.Warning(
                    "MapTileBootstrap Host={Host} Outcome=session-null — basemap empty (Google-only)",
                    host);
                ApplyUnavailable(layer, attributionBorder, attributionText, googleLogo, "Map tiles unavailable");
                return false;
            }

            layer.UseGoogleTiles(session.UrlTemplate);
            SetAttribution(attributionBorder, attributionText, googleLogo, useGoogleMaps: true);
            Logger.Information(
                "MapTileBootstrap Host={Host} Outcome=ok — Google Map Tiles roadmap",
                host);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(
                ex,
                "MapTileBootstrap Host={Host} Outcome=exception — basemap empty (Google-only)",
                host);
            ApplyUnavailable(layer, attributionBorder, attributionText, googleLogo, "Map tiles unavailable");
            return false;
        }
    }

    /// <summary>Clear basemap and show a non-OSM status line when Google tiles cannot load.</summary>
    public static void ApplyUnavailable(
        GoogleMapTilesImageryLayer layer,
        Border? attributionBorder,
        TextBlock? attributionText,
        System.Windows.Controls.Image? googleLogo = null,
        string? statusMessage = null)
    {
        ArgumentNullException.ThrowIfNull(layer);
        layer.ClearBasemap();
        if (googleLogo is not null)
        {
            googleLogo.Visibility = Visibility.Collapsed;
        }

        if (attributionBorder is not null)
        {
            attributionBorder.Visibility = Visibility.Visible;
        }

        if (attributionText is not null)
        {
            attributionText.Text = statusMessage ?? "Map tiles unavailable";
            attributionText.FontSize = 12;
        }
    }

    /// <summary>
    /// Map Tiles API Policies require the viewport <c>copyright</c> string when Google tiles are active.
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

    private static void SetAttribution(
        Border? overlay,
        TextBlock? text,
        System.Windows.Controls.Image? googleLogo,
        bool useGoogleMaps)
    {
        if (overlay is not null)
        {
            overlay.Visibility = Visibility.Visible;
        }

        if (googleLogo is not null)
        {
            googleLogo.Visibility = useGoogleMaps ? Visibility.Visible : Visibility.Collapsed;
        }

        if (text is not null)
        {
            text.Text = useGoogleMaps ? MapBasemap.Attribution : "Map tiles unavailable";
            text.FontSize = useGoogleMaps ? 13 : 12;
        }
    }
}
