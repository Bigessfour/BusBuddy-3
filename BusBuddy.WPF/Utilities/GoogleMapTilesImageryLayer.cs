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
    private bool _loggedVisualTreeSkip;

    public bool IsGoogleTilesActive => _googleUrlTemplate is not null;

    /// <summary>
    /// True after the latest measure/arrange pass finished (success or swallowed skip).
    /// Reset by <see cref="BeginMarkerHostCheck"/> before a forced layout.
    /// </summary>
    internal bool LastMeasureCompleted { get; private set; }

    /// <summary>
    /// True when the latest layout pass swallowed a not-parented <c>TransformToVisual</c>.
    /// </summary>
    internal bool LastLayoutSkippedVisualTree { get; private set; }

    internal void BeginMarkerHostCheck()
    {
        LastMeasureCompleted = false;
        LastLayoutSkippedVisualTree = false;
    }

    /// <summary>
    /// Syncfusion <c>CustomDataSymbol.ApplyTemplate</c> calls <c>TransformToVisual</c> before the
    /// marker is parented (VM runtime-errors.log 2026-09-17). Swallow that layout throw so Window
    /// measure can finish; pin assignment is retried from the view after <see cref="MapMarkerHost"/>.
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        try
        {
            var size = base.MeasureOverride(constraint);
            LastLayoutSkippedVisualTree = false;
            LastMeasureCompleted = true;
            return size;
        }
        catch (Exception ex) when (IsVisualTreeNotReady(ex))
        {
            LastLayoutSkippedVisualTree = true;
            LastMeasureCompleted = true;
            LogVisualTreeSkipOnce(ex);
            return SafeLayoutSize(constraint);
        }
    }

    /// <inheritdoc cref="MeasureOverride"/>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        try
        {
            var size = base.ArrangeOverride(arrangeBounds);
            LastMeasureCompleted = true;
            return size;
        }
        catch (Exception ex) when (IsVisualTreeNotReady(ex))
        {
            LastLayoutSkippedVisualTree = true;
            LastMeasureCompleted = true;
            LogVisualTreeSkipOnce(ex);
            return arrangeBounds;
        }
    }

    internal static bool IsVisualTreeNotReady(Exception ex)
    {
        if (ex is NullReferenceException &&
            ex.StackTrace?.Contains("Syncfusion.UI.Xaml.Maps", StringComparison.Ordinal) == true)
        {
            return true;
        }

        var text = ex.Message ?? string.Empty;
        if (ex.InnerException is not null)
        {
            text += " " + ex.InnerException.Message;
        }

        return text.Contains("do not share a common ancestor", StringComparison.OrdinalIgnoreCase);
    }

    private void LogVisualTreeSkipOnce(Exception ex)
    {
        if (_loggedVisualTreeSkip)
        {
            return;
        }

        _loggedVisualTreeSkip = true;
        Logger.Debug(ex, "Imagery layer layout skipped — marker visual tree not parented yet");
    }

    private Size SafeLayoutSize(Size constraint)
    {
        if (DesiredSize.Width > 0 && DesiredSize.Height > 0)
        {
            return DesiredSize;
        }

        var width = double.IsNaN(constraint.Width) || double.IsInfinity(constraint.Width) ? 0 : constraint.Width;
        var height = double.IsNaN(constraint.Height) || double.IsInfinity(constraint.Height) ? 0 : constraint.Height;
        return new Size(width, height);
    }

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
