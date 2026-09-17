using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Shared gate for assigning <see cref="ImageryLayer.Markers"/> after the visual tree can
/// <c>TransformToVisual</c>. Binding <c>Markers</c> or <c>MarkerTemplateSelector</c> in XAML
/// inflates Syncfusion <c>CustomDataSymbol</c> during first Measure (VM runtime-errors.log
/// 2026-09-17 District Map cascade).
/// </summary>
public static class MapMarkerHost
{
    public static bool CanHost(SfMap? map, ImageryLayer? layer)
    {
        if (map is null || layer is null)
        {
            return false;
        }

        if (map.ActualWidth <= 0 || map.ActualHeight <= 0)
        {
            return false;
        }

        if (PresentationSource.FromVisual(map) is null || PresentationSource.FromVisual(layer) is null)
        {
            return false;
        }

        try
        {
            _ = layer.TransformToVisual(map);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TryAssign(
        SfMap? map,
        ImageryLayer? layer,
        IEnumerable? markers,
        DataTemplateSelector? templateSelector = null)
    {
        if (!CanHost(map, layer) || layer is null)
        {
            return false;
        }

        if (templateSelector is not null)
        {
            layer.MarkerTemplateSelector = templateSelector;
            layer.MarkerTemplate = null;
        }

        layer.Markers = markers;
        return true;
    }
}
