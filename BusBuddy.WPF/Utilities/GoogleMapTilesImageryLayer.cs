using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion imagery layer for Google Map Tiles API only (no OpenStreetMap).
/// </summary>
/// <remarks>
/// Syncfusion has no <c>LayerType.Google</c>. Custom XYZ tiles use <see cref="ImageryLayer.UrlTemplate"/>
/// (<c>{z}/{x}/{y}</c>) per map-providers. Until a session URL is applied, <see cref="LayerType.Bing"/>
/// with an empty Bing key loads nothing (avoids Syncfusion's built-in HTTP OSM fetch).
/// Docs: https://help.syncfusion.com/wpf/maps/map-providers
/// </remarks>
public sealed class GoogleMapTilesImageryLayer : ImageryLayer
{
    private static readonly ILogger Logger = Log.ForContext<GoogleMapTilesImageryLayer>();

    private static readonly FieldInfo? TileGenerationInProgressField =
        typeof(ImageryLayer).GetField(
            "isTileGenerationInProgress",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? ImageryPanelField =
        typeof(ImageryLayer).GetField(
            "imageryPanel",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private string? _googleUrlTemplate;

    public bool IsGoogleTilesActive => _googleUrlTemplate is not null;

    /// <summary>
    /// Applies an official Map Tiles API URL template with <c>{z}/{x}/{y}</c> placeholders.
    /// </summary>
    public void UseGoogleTiles(string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        if (string.Equals(_googleUrlTemplate, urlTemplate, StringComparison.Ordinal)
            && string.Equals(UrlTemplate, urlTemplate, StringComparison.Ordinal))
        {
            return;
        }

        _googleUrlTemplate = urlTemplate;
        CanCacheTiles = true;
        ResetTileGenerationGate();
        UrlTemplate = urlTemplate;
        Logger.Information("Google Map Tiles UrlTemplate applied (Syncfusion custom imagery path)");
    }

    /// <summary>
    /// Clears any basemap URL. Uses <see cref="LayerType.Bing"/> without a key so Syncfusion
    /// does not call built-in OpenStreetMap HTTP tile hosts.
    /// </summary>
    public void ClearBasemap()
    {
        _googleUrlTemplate = null;
        CanCacheTiles = true;
        ResetTileGenerationGate();
        if (!string.IsNullOrEmpty(UrlTemplate))
        {
            UrlTemplate = string.Empty;
        }

        if (LayerType != LayerType.Bing)
        {
            LayerType = LayerType.Bing;
        }

        Logger.Debug("District imagery basemap cleared (Google-only; no OSM fail-open)");
    }

    /// <summary>
    /// Returns empty so Syncfusion uses <c>UrlTemplate</c>. Still raises <see cref="TileRequested"/>.
    /// </summary>
    protected override string GetUri(int X, int Y, int Scale)
    {
        TileRequested?.Invoke(
            this,
            new TileRequestedEventArgs(Scale, X, Y, isGoogle: _googleUrlTemplate is not null));
        return string.Empty;
    }

    /// <summary>
    /// Raised when Syncfusion asks for a tile URI. Indices only — never the resolved URL.
    /// </summary>
    public event EventHandler<TileRequestedEventArgs>? TileRequested;

    public sealed class TileRequestedEventArgs : EventArgs
    {
        public TileRequestedEventArgs(int zoom, int x, int y, bool isGoogle)
        {
            Zoom = zoom;
            X = x;
            Y = y;
            IsGoogle = isGoogle;
        }

        public int Zoom { get; }

        public int X { get; }

        public int Y { get; }

        public bool IsGoogle { get; }
    }

    /// <summary>Serilog snapshot of Syncfusion tile panel health (no URLs / secrets).</summary>
    public void LogTileHealth(string reason)
    {
        try
        {
            var panel = ImageryPanelField?.GetValue(this) as Panel;
            var children = panel?.Children.Count ?? -1;
            var withSource = 0;
            if (panel is not null)
            {
                foreach (UIElement child in panel.Children)
                {
                    if (child is Tile { TileImageSource: not null })
                    {
                        withSource++;
                    }
                }
            }

            var gate = TileGenerationInProgressField?.GetValue(this);
            var urlSet = !string.IsNullOrEmpty(UrlTemplate);
            if (IsGoogleTilesActive && urlSet && withSource == 0 && children > 0)
            {
                Logger.Warning(
                    "Imagery tile health Reason={Reason} Children={Children} WithSource={WithSource} Gate={Gate} Google={Google} UrlTemplateSet={UrlSet} — Google UrlTemplate set but no painted tiles",
                    reason,
                    children,
                    withSource,
                    gate,
                    IsGoogleTilesActive,
                    urlSet);
            }
            else
            {
                Logger.Information(
                    "Imagery tile health Reason={Reason} Children={Children} WithSource={WithSource} Gate={Gate} Google={Google} UrlTemplateSet={UrlSet}",
                    reason,
                    children,
                    withSource,
                    gate,
                    IsGoogleTilesActive,
                    urlSet);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Imagery tile health inspect failed");
        }
    }

    private void ResetTileGenerationGate()
    {
        if (TileGenerationInProgressField is null)
        {
            return;
        }

        try
        {
            TileGenerationInProgressField.SetValue(this, false);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Could not reset isTileGenerationInProgress");
        }
    }
}
