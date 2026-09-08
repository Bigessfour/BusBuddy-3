using System;
using BusBuddy.Core.Mapping;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion imagery layer for the District Map.
/// <para>
/// Google: resolves each tile through <see cref="GetUri"/> (the documented
/// <c>ImageryLayer</c> extension point) from the Map Tiles API session template.
/// OSM fail-open: <see cref="GetUri"/> returns empty and the built-in <see cref="LayerType.OSM"/> provider is used.
/// </para>
/// <para>
/// <see cref="ImageryLayer.UrlTemplate"/> is intentionally never set. That path downloads tiles with a bare
/// <c>HttpClient.GetByteArrayAsync</c> inside an <c>async void</c> generator; one 403/429 (expired session,
/// API not enabled, quota) throws past the layer and leaves its internal tile-generation flag stuck, after
/// which wheel zoom and every camera change are ignored. <see cref="GetUri"/> tiles go through the
/// guarded <c>BitmapImage</c> loader instead.
/// </para>
/// </summary>
public sealed class GoogleMapTilesImageryLayer : ImageryLayer
{
    private static readonly ILogger Logger = Log.ForContext<GoogleMapTilesImageryLayer>();
    private string? _googleUrlTemplate;

    public bool IsGoogleTilesActive => _googleUrlTemplate is not null;

    /// <summary>
    /// Applies an official Map Tiles API URL template with <c>{z}/{x}/{y}</c> placeholders and reloads tiles.
    /// </summary>
    public void UseGoogleTiles(string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        if (string.Equals(_googleUrlTemplate, urlTemplate, StringComparison.Ordinal))
        {
            return;
        }

        _googleUrlTemplate = urlTemplate;
        // Map Tiles API content is session-scoped; do not persist it in the LocalAppData tile cache.
        CanCacheTiles = false;
        ReloadTiles();
    }

    /// <summary>Drops the Google template and reloads with the built-in OpenStreetMap provider.</summary>
    public void UseOpenStreetMap()
    {
        var wasGoogle = _googleUrlTemplate is not null;
        _googleUrlTemplate = null;
        CanCacheTiles = true;
        if (LayerType != LayerType.OSM)
        {
            LayerType = LayerType.OSM;
            return;
        }

        if (wasGoogle)
        {
            ReloadTiles();
        }
    }

    /// <summary>
    /// Syncfusion calls this per tile before consulting <c>UrlTemplate</c> / <c>LayerType</c>.
    /// Empty string = fall through to the built-in provider.
    /// </summary>
    protected override string GetUri(int X, int Y, int Scale)
    {
        // Parameter casing matches the Syncfusion base signature (CA1725).
        var template = _googleUrlTemplate;
        TileRequested?.Invoke(this, new TileRequestedEventArgs(Scale, X, Y, template is not null));
        return template is null ? string.Empty : MapBasemap.ResolveTileUrl(template, Scale, X, Y);
    }

    /// <summary>
    /// Raised per tile the layer asks for (Google or OSM fall-through). Carries indices only — never the
    /// resolved URL, which embeds the session token and API key. Consumed by <see cref="MapInteractionDiagnostics"/>.
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

    /// <summary>
    /// Toggling <see cref="ImageryLayer.LayerType"/> is the only public call that clears the tile panel and
    /// tile list before regenerating. Bing with an empty key is a no-op load, so the round trip costs nothing.
    /// No-op until the layer is templated (the first load already goes through <see cref="GetUri"/>).
    /// </summary>
    private void ReloadTiles()
    {
        try
        {
            LayerType = LayerType.Bing;
            LayerType = LayerType.OSM;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Imagery tile reload failed — tiles refresh on next pan/zoom");
        }
    }
}
