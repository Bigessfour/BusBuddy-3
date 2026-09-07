using System;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion imagery layer that serves Map Tiles API roadmap tiles when a URL template is set,
/// otherwise OpenStreetMap tiles (fail-open without a Maps key / session).
/// </summary>
public sealed class GoogleMapTilesImageryLayer : ImageryLayer
{
    private string? _urlTemplate;
    private bool _useGoogleTiles;

    public bool IsGoogleTilesActive => _useGoogleTiles;

    /// <summary>
    /// Applies an official Map Tiles API URL template
    /// (<c>https://tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}?session=…&amp;key=…</c>).
    /// </summary>
    public void UseGoogleTiles(string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        _urlTemplate = urlTemplate;
        _useGoogleTiles = true;
    }

    public void UseOpenStreetMap()
    {
        _urlTemplate = null;
        _useGoogleTiles = false;
    }

    protected override string GetUri(int X, int Y, int Scale)
    {
        if (_useGoogleTiles && !string.IsNullOrWhiteSpace(_urlTemplate))
        {
            return _urlTemplate
                .Replace("{z}", Scale.ToString(), StringComparison.Ordinal)
                .Replace("{x}", X.ToString(), StringComparison.Ordinal)
                .Replace("{y}", Y.ToString(), StringComparison.Ordinal);
        }

        return $"https://tile.openstreetmap.org/{Scale}/{X}/{Y}.png";
    }
}
