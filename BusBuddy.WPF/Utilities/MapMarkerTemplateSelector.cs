using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Map;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion <c>ImageryLayer.MarkerTemplateSelector</c>: school vs smaller home vs stop
/// (pickup, depot, waypoint, student). Docs:
/// https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Maps.MapLayer.html#Syncfusion_UI_Xaml_Maps_MapLayer_MarkerTemplateSelector
/// </summary>
public sealed class MapMarkerTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SchoolTemplate { get; set; }

    public DataTemplate? HomeTemplate { get; set; }

    public DataTemplate? StopTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is MapViewModel.MapMarker marker)
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
}
