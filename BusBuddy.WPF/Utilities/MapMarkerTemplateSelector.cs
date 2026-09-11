using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Map;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion <c>ImageryLayer.MarkerTemplateSelector</c>: school vs smaller home vs stop
/// (pickup, depot, waypoint, student). Docs:
/// https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Maps.MapLayer.html#Syncfusion_UI_Xaml_Maps_MapLayer_MarkerTemplateSelector
/// <para>
/// Syncfusion hands the selector its <see cref="CustomDataSymbol"/> wrapper, not the bound marker;
/// the marker is <see cref="CustomDataSymbol.Data"/>. Template DataContext stays the wrapper —
/// bind as <c>{Binding Data.Caption}</c> / <c>Data.MarkerSize</c> (not bare <c>Caption</c>).
/// </para>
/// </summary>
public sealed class MapMarkerTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SchoolTemplate { get; set; }

    public DataTemplate? HomeTemplate { get; set; }

    public DataTemplate? StopTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        var marker = Unwrap(item);
        if (marker is not null)
        {
            if (MapMarkerLabels.IsSchoolVisual(marker.Kind))
            {
                return SchoolTemplate ?? StopTemplate;
            }

            if (marker.Kind == MapMarkerLabels.Kind.Home)
            {
                return HomeTemplate ?? StopTemplate;
            }
        }

        return StopTemplate ?? SchoolTemplate;
    }

    /// <summary>Accepts either the raw marker or Syncfusion's <see cref="CustomDataSymbol"/> wrapper.</summary>
    internal static MapMarker? Unwrap(object? item) => item switch
    {
        MapMarker marker => marker,
        CustomDataSymbol symbol => symbol.Data as MapMarker,
        _ => null,
    };
}
