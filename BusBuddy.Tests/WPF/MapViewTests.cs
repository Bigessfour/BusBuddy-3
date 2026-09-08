using System;
using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapViewTests
{
    [Test]
    public void MapViewXaml_UsesSfMapAndPlotsStudentsInSystem()
    {
        var xaml = XamlViewFile.Read("Views/Map/MapView.xaml");
        Assert.That(xaml, Does.Contain("x:Class=\"BusBuddy.WPF.Views.Map.MapView\""));
        Assert.That(xaml, Does.Contain("maps:SfMap"));
        Assert.That(xaml, Does.Contain("Command=\"{Binding BulkPlotEligibleStudentsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ShowSchoolsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PlotPickupStopsCommand}\""));
        Assert.That(xaml, Does.Contain("Label=\"Plot Pickup Stops\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ExportRouteDataCommand}\""));
        Assert.That(xaml, Does.Contain("Label=\"Export Route\""));
        Assert.That(xaml, Does.Contain("utils:GoogleMapTilesImageryLayer"));
        Assert.That(xaml, Does.Contain("x:Name=\"MapAttribution\""));
        Assert.That(xaml, Does.Contain("Google Maps"));
        var tileLayer = XamlViewFile.Read("Utilities/GoogleMapTilesImageryLayer.cs");
        // Tiles resolve through the GetUri extension point; UrlTemplate's HttpClient path can wedge the layer.
        Assert.That(tileLayer, Does.Contain("protected override string GetUri"));
        Assert.That(tileLayer, Does.Contain("MapBasemap.ResolveTileUrl"));
        // The ImageryLayer.UrlTemplate property must stay unset (its HttpClient path wedges the layer on 403/429).
        Assert.That(tileLayer, Does.Not.Match(@"(?<![\w.])UrlTemplate\s*="));
        Assert.That(tileLayer, Does.Not.Contain("this.UrlTemplate"));
        Assert.That(tileLayer, Does.Not.Contain("base.UrlTemplate"));
        Assert.That(tileLayer, Does.Contain("LayerType.OSM"));
        Assert.That(tileLayer, Does.Contain("CanCacheTiles = false"));
        Assert.That(xaml, Does.Not.Contain("MapLayerComboBox"));
        Assert.That(xaml, Does.Not.Contain("SelectedMapLayer"));
        Assert.That(xaml, Does.Not.Contain("GoogleEarth"));
        Assert.That(xaml, Does.Not.Contain("Wiley"));
        Assert.That(xaml, Does.Not.Contain("Add Stop (Demo)"));
        Assert.That(xaml, Does.Not.Contain("Demo Stop"));
        Assert.That(xaml, Does.Not.Contain("MVP"));
        Assert.That(xaml, Does.Not.Contain("MappingName=\"CurrentLocation\""));
        Assert.That(xaml, Does.Contain("Content=\"Zoom In\""));
        Assert.That(xaml, Does.Contain("Content=\"Zoom Out\""));
        Assert.That(xaml, Does.Contain("EnableZoom=\"True\""));
        Assert.That(xaml, Does.Contain("EnablePan=\"True\""));
        Assert.That(xaml, Does.Contain("IsHitTestVisible=\"True\""));
        Assert.That(xaml, Does.Contain("Center=\"{Binding MapCenter, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Markers=\"{Binding MapMarkers}\""));
        Assert.That(xaml, Does.Contain("MarkerTemplateSelector=\"{StaticResource DistrictMarkerTemplateSelector}\""));
        Assert.That(xaml, Does.Contain("x:Key=\"SchoolMarkerTemplate\""));
        Assert.That(xaml, Does.Contain("x:Key=\"StopMarkerTemplate\""));
        Assert.That(xaml, Does.Contain("x:Key=\"HomeMarkerTemplate\""));
        Assert.That(xaml, Does.Contain("HomeTemplate=\"{StaticResource HomeMarkerTemplate}\""));
        Assert.That(xaml, Does.Contain("MapMarkerTemplateSelector"));
        Assert.That(xaml, Does.Not.Contain("StudentMarkerTemplate"));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedRoute, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Routes}\""));
        Assert.That(xaml, Does.Contain("ZoomLevel=\"{Binding MapZoomLevel, Mode=TwoWay}\""));
        // Camera is Center + ZoomLevel; ImageryLayer.Radius doubles the bounds and re-fits on every resize.
        Assert.That(xaml, Does.Not.Contain("Radius=\"{Binding"));
        Assert.That(xaml, Does.Not.Contain("MapFitRadiusKm"));
        Assert.That(xaml, Does.Contain("MaxZoom=\"19\""));
        Assert.That(xaml, Does.Contain("SizeChanged=\"GeoMap_SizeChanged\""));
        Assert.That(xaml, Does.Contain("DataContext.ShowDetailLabels"));
        Assert.That(xaml, Does.Contain("AncestorType={x:Type local:MapView}"));
        Assert.That(xaml, Does.Contain("x:Name=\"RouteTrail\""));
        Assert.That(xaml, Does.Contain("x:Name=\"RouteTrailLayer\""));
        Assert.That(xaml, Does.Contain("SubShapeFileLayers"));
        Assert.That(xaml, Does.Contain("maps:MapPolyline"));
        Assert.That(XamlViewFile.Read("Utilities/MapRouteTrailLayer.cs"), Does.Contain("polyline.Points.Clear"));
        Assert.That(xaml, Does.Not.Contain("MapLayerComboBox_SelectionChanged"));
        Assert.That(xaml, Does.Not.Contain("ZoomLevel=\"13\""));
        Assert.That(xaml, Does.Contain("Live fleet GPS tracking is deferred"));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Fleet GPS status\""));
        Assert.That(xaml, Does.Not.Contain("Show All Buses"));
        Assert.That(xaml, Does.Not.Contain("Track Selected"));
        Assert.That(xaml, Does.Not.Contain("IsLiveTrackingEnabled"));
        Assert.That(xaml, Does.Not.Contain("TrackingIntervalIndex"));
        Assert.That(xaml, Does.Not.Contain("FluentDarkTheme.xaml"));
        Assert.That(xaml, Does.Not.Contain("#AA2B2B2B"));
    }

    [Test]
    public void MarkerTemplateSelector_UnwrapsSyncfusionCustomDataSymbol()
    {
        // Syncfusion passes CustomDataSymbol (Data = bound marker) to SelectTemplate, never the marker itself.
        var source = XamlViewFile.Read("Utilities/MapMarkerTemplateSelector.cs");
        Assert.That(source, Does.Contain("CustomDataSymbol symbol => symbol.Data as MapViewModel.MapMarker"));
        Assert.That(source, Does.Contain("Unwrap(item)"));
    }

    [Test]
    public void RetiredLeafletWebViewAndMapWinGis_AreGone()
    {
        Assert.That(XamlViewFile.Exists("Views/Map/MapView.xaml"), Is.True);
        Assert.That(XamlViewFile.Exists("Assets/Map/map.html"), Is.False);
        Assert.That(XamlViewFile.Exists("Controls/MapWinGISMapControl.cs"), Is.False);
        Assert.That(XamlViewFile.Read("BusBuddy.WPF.csproj"), Does.Not.Contain("Microsoft.Web.WebView2"));
        Assert.That(CoreSourceFile.Exists("Services/GeoDataService.cs"), Is.True);
        Assert.That(CoreSourceFile.Exists("Services/OfflineGeocodingService.cs"), Is.False);
        Assert.That(CoreSourceFile.Exists("Models/GeoAnalysisResults.cs"), Is.False);
    }

    [Test]
    public void MappingService_IsAutoMapperObjectMappingNotGeo()
    {
        var mapping = XamlViewFile.Read("Services/MappingService.cs");
        Assert.That(mapping, Does.Contain("AutoMapper"));
        Assert.That(mapping, Does.Contain("object mapping"));
        Assert.That(mapping, Does.Contain("not geospatial"));
        Assert.That(mapping, Does.Not.Contain("IGeoDataService"));
        Assert.That(mapping, Does.Not.Contain("SfMap"));
    }

    [Test]
    public void MapViewLauncher_PrefersActiveWindowSoModalStudentsCannotHideTheMap()
    {
        var launcher = XamlViewFile.Read("Utilities/MapViewLauncher.cs");
        Assert.That(launcher, Does.Contain("ResolveOwner"));
        Assert.That(launcher, Does.Contain("DialogOwner.Resolve"));
        Assert.That(launcher, Does.Contain("BringToFront"));
        Assert.That(launcher, Does.Contain("ShowActivated = true"));
        var owner = XamlViewFile.Read("Utilities/DialogOwner.cs");
        Assert.That(owner, Does.Contain("IsActive"));
    }

    [Test]
    public void SchoolAndPickupPickMaps_DoNotHardcodeATown()
    {
        var schoolXaml = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml");
        Assert.That(schoolXaml, Does.Not.Contain("Lamar"));
        Assert.That(schoolXaml, Does.Not.Contain("Wiley"));

        var schoolVm = XamlViewFile.Read("ViewModels/Student/SchoolDestinationFormViewModel.cs");
        Assert.That(schoolVm, Does.Contain("DistrictCameraUi.Resolve"));
        Assert.That(schoolVm, Does.Not.Contain("38.0872"));
        Assert.That(schoolVm, Does.Not.Contain("\"Lamar\""));

        var pickupVm = XamlViewFile.Read("ViewModels/Student/PickupStopFormViewModel.cs");
        Assert.That(pickupVm, Does.Contain("DistrictCameraUi.Resolve"));
        Assert.That(pickupVm, Does.Not.Contain("38.0872"));
    }

    [Test]
    public void StudentsViewMapCommands_OpenDistrictMapWindow()
    {
        var xaml = XamlViewFile.Read("Views/Student/StudentsView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding ViewMapCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DataContext.ViewOnMapCommand"));

        // Folder-scoped: the map wiring must exist in the Students view-model layer, but which
        // collaborator holds it is a structural choice this test must not freeze.
        var vm = XamlViewFile.ReadFolder("ViewModels/Student");
        Assert.That(vm, Does.Contain("MapViewLauncher.Show"));
        Assert.That(vm, Does.Contain("BulkPlotEligibleStudentsCommand"));
        Assert.That(vm, Does.Contain("District Map opened"));
        Assert.That(vm, Does.Contain("PickupStopId"));
        Assert.That(vm, Does.Contain("StudentPlotLocation.PinsFromStored"));
        Assert.That(vm, Does.Contain("MapStudentPlot.Draw"));
    }

    [Test]
    public void StudentFormViewOnMap_UsesPickupThenHomePlotRule()
    {
        var form = XamlViewFile.ReadFolder("ViewModels/Student");
        Assert.That(form, Does.Contain("StudentPlotLocation.PinsFromStored"));
        Assert.That(form, Does.Contain("MapStudentPlot.Draw"));
        Assert.That(form, Does.Contain("ResolvePickupCatalogForPlotAsync"));
    }

    [Test]
    public void TripBoardView_IsNotTheDailyRouteEditor()
    {
        var xaml = XamlViewFile.Read("Views/Activity/ActivityManagementView.xaml");
        Assert.That(xaml, Does.Contain("Trip Board"));
        Assert.That(xaml, Does.Contain("Import CSV"));
        Assert.That(xaml, Does.Contain("MappingName=\"ExternalTicketNo\""));
        Assert.That(xaml, Does.Contain("MappingName=\"PlannedHeadcount\""));
        Assert.That(xaml, Does.Not.Contain("Regular Route"));

        var dialog = XamlViewFile.Read("Views/Activity/ActivityScheduleEditDialog.xaml.cs");
        Assert.That(dialog, Does.Not.Contain("Regular Route"));
        Assert.That(dialog, Does.Contain("MissingInfo"));

        var mapVm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(mapVm, Does.Contain("TryPlotTrip"));
        Assert.That(mapVm, Does.Not.Contain("IsLiveTrackingEnabled"));
    }
}
