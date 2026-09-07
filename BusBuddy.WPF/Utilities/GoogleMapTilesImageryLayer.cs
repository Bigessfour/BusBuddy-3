using System;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion imagery layer for District Map.
/// Google: sets <see cref="ImageryLayer.UrlTemplate"/> (Map Tiles API).
/// OSM fail-open: clears UrlTemplate and uses built-in <see cref="LayerType.OSM"/>.
/// </summary>
public sealed class GoogleMapTilesImageryLayer : ImageryLayer
{
    public bool IsGoogleTilesActive { get; private set; }

    /// <summary>
    /// Applies an official Map Tiles API URL template with <c>{z}/{x}/{y}</c> placeholders.
    /// Syncfusion prefers <see cref="ImageryLayer.UrlTemplate"/> over <see cref="LayerType"/>.
    /// </summary>
    public void UseGoogleTiles(string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        IsGoogleTilesActive = true;
        // UrlTemplate takes precedence and ignores LayerType (Syncfusion Maps docs).
        UrlTemplate = urlTemplate;
    }

    public void UseOpenStreetMap()
    {
        IsGoogleTilesActive = false;
        // Clear custom template so built-in OSM provider is used (User-Agent / tile host handled by SfMaps).
        UrlTemplate = null;
        LayerType = LayerType.OSM;
    }
}
