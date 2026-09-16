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
        Assert.That(xaml, Does.Contain("utils:DistrictSfMap"));
        Assert.That(xaml, Does.Not.Contain("maps:SfMap"));
        Assert.That(xaml, Does.Contain("Command=\"{Binding BulkPlotEligibleStudentsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ShowRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ShowSchoolsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PlotPickupStopsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding BulkPlotEligibleStudentsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CenterOnFleetCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshMapCommand}\""));
        // Sidebar ButtonAdv: Label + Command only — no local Background (stomps Fluent pressed chrome).
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding ShowSchoolsCommand}\"\n                              Background="));
        var mapVm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(mapVm, Does.Contain("LoadAllRoutesOnMapAsync"));
        Assert.That(mapVm, Does.Contain("UpdateMapForRouteAsync(withWaypoints, refreshDrivePath: true)"));
        Assert.That(mapVm, Does.Contain("IRoutingService"));
        Assert.That(xaml, Does.Contain("Label=\"Export Route\""));
        Assert.That(xaml, Does.Contain("utils:GoogleMapTilesImageryLayer"));
        Assert.That(xaml, Does.Contain("x:Name=\"MapAttribution\""));
        Assert.That(xaml, Does.Contain("x:Name=\"GoogleMapsLogo\""));
        Assert.That(xaml, Does.Contain("Assets/Maps/google_maps_on_non_white.png"));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Google Maps\""));
        Assert.That(xaml, Does.Contain("Google Maps"));
        var bootstrap = XamlViewFile.Read("Utilities/MapTileBootstrap.cs");
        Assert.That(bootstrap, Does.Contain("googleLogo.Visibility"));
        Assert.That(bootstrap, Does.Contain("useGoogleMaps ? Visibility.Visible : Visibility.Collapsed"));
        var tileLayer = XamlViewFile.Read("Utilities/GoogleMapTilesImageryLayer.cs");
        // Syncfusion map-providers: custom XYZ tiles via UrlTemplate; Google-only (no OSM fail-open).
        Assert.That(tileLayer, Does.Contain("UrlTemplate = urlTemplate"));
        Assert.That(tileLayer, Does.Contain("ClearBasemap"));
        Assert.That(tileLayer, Does.Contain("LayerType.Bing"));
        Assert.That(tileLayer, Does.Not.Contain("OpenStreetMapHttpsTemplate"));
        Assert.That(tileLayer, Does.Not.Contain("UseOpenStreetMap"));
        Assert.That(tileLayer, Does.Contain("protected override string GetUri"));
        Assert.That(tileLayer, Does.Contain("return string.Empty"));
        Assert.That(tileLayer, Does.Contain("LogTileHealth"));
        Assert.That(tileLayer, Does.Contain("isTileGenerationInProgress"));
        Assert.That(bootstrap, Does.Contain("ApplyUnavailable"));
        Assert.That(bootstrap, Does.Not.Contain("ApplyOsm"));
        Assert.That(bootstrap, Does.Not.Contain("ForceReloadTiles"));
        Assert.That(xaml, Does.Contain("LayerType=\"Bing\""));
        Assert.That(xaml, Does.Not.Contain("LayerType=\"OSM\""));
        Assert.That(xaml, Does.Not.Contain("OpenStreetMap"));
        Assert.That(xaml, Does.Not.Contain("MapLayerComboBox"));
        Assert.That(xaml, Does.Not.Contain("SelectedMapLayer"));
        Assert.That(xaml, Does.Not.Contain("GoogleEarth"));
        Assert.That(xaml, Does.Not.Contain("Wiley"));
        Assert.That(xaml, Does.Not.Contain("Add Stop (Demo)"));
        Assert.That(xaml, Does.Not.Contain("Demo Stop"));
        Assert.That(xaml, Does.Not.Contain("MVP"));
        Assert.That(xaml, Does.Not.Contain("MappingName=\"CurrentLocation\""));
        Assert.That(xaml, Does.Contain("Label=\"Zoom In\""));
        Assert.That(xaml, Does.Contain("Label=\"Zoom Out\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ZoomInCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ZoomOutCommand}\""));
        Assert.That(xaml, Does.Contain("Style=\"{StaticResource MapOverlayButtonStyle}\""));
        Assert.That(xaml, Does.Not.Contain("MapOverlayWpfButtonStyle"));
        Assert.That(xaml, Does.Not.Contain("Content=\"Zoom In\""));
        Assert.That(xaml, Does.Contain("EnableZoom=\"True\""));
        Assert.That(xaml, Does.Contain("EnablePan=\"True\""));
        Assert.That(xaml, Does.Contain("IsHitTestVisible=\"True\""));
        Assert.That(xaml, Does.Contain("Center=\"{Binding MapCenter, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Not.Contain("Markers=\"{Binding MapMarkers}\""));
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
        Assert.That(xaml, Does.Contain("ShowCaption"));
        Assert.That(xaml, Does.Contain("Text=\"{Binding Data.DisplayCaption}\""));
        Assert.That(xaml, Does.Not.Contain("Text=\"{Binding Data.Caption}\""), "one caption per spot: DisplayCaption folds the route-stop tag in");
        Assert.That(xaml, Does.Contain("Fill=\"{Binding Data.FillBrush}\""));
        Assert.That(xaml, Does.Contain("Stroke=\"{Binding Data.StrokeBrush}\""));
        Assert.That(xaml, Does.Not.Contain("Fill=\"#E85D4C\""), "pin colours come from MapMarkerLabels, not per-template literals");
        Assert.That(xaml, Does.Not.Contain("Fill=\"#5B8DEF\""));
        Assert.That(xaml, Does.Contain("Width=\"{Binding Data.MarkerSize}\""));
        Assert.That(xaml, Does.Contain("FontSize=\"{Binding Data.LabelFontSize}\""));
        Assert.That(xaml, Does.Contain("Data.ShowCaption"));
        Assert.That(xaml, Does.Not.Contain("DataContext.ShowDetailLabels"));
        Assert.That(XamlViewFile.Read("Utilities/MapMarkerLabels.cs"), Does.Contain("ScaledMarkerSize"));
        Assert.That(XamlViewFile.Read("Utilities/MapMarkerLabels.cs"), Does.Contain("CaptionFrom"));
        Assert.That(XamlViewFile.Read("Utilities/MapMarkerTemplateSelector.cs"), Does.Contain("Binding Data.DisplayCaption"));
        Assert.That(XamlViewFile.Read("Utilities/DistrictSfMap.cs"), Does.Contain("OnMouseMove"));
        Assert.That(XamlViewFile.Read("Utilities/DistrictSfMap.cs"), Does.Contain("NullReferenceException"));
        Assert.That(bootstrap, Does.Contain("Host={Host} Outcome="));
        Assert.That(bootstrap, Does.Contain("Outcome=no-key"));
        Assert.That(bootstrap, Does.Contain("Outcome=ok"));
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
        Assert.That(source, Does.Contain("CustomDataSymbol symbol => symbol.Data as MapMarker"));
        Assert.That(source, Does.Contain("Unwrap(item)"));
        Assert.That(source, Does.Contain("Binding Data.DisplayCaption"));
    }

    [Test]
    public void MapView_OverlayToolbarOwnsItsTemplate_AndActiveBusesCardIsGone()
    {
        var xaml = XamlViewFile.Read("Views/Map/MapView.xaml");

        // Zoom In / Zoom Out vanished after a click because the Fluent ButtonAdv theme repainted the focused/pressed
        // chrome over the tiles. The overlay style now carries its own ControlTemplate and literal brushes.
        var overlayStyle = xaml[xaml.IndexOf("x:Key=\"MapOverlayButtonStyle\"", StringComparison.Ordinal)..];
        overlayStyle = overlayStyle[..overlayStyle.IndexOf("</Style>", StringComparison.Ordinal)];
        Assert.That(overlayStyle, Does.Contain("<ControlTemplate TargetType=\"syncfusion:ButtonAdv\">"));
        Assert.That(overlayStyle, Does.Contain("Text=\"{TemplateBinding Label}\""));
        Assert.That(overlayStyle, Does.Contain("Background=\"{TemplateBinding Background}\""));
        Assert.That(overlayStyle, Does.Not.Contain("DynamicResource ButtonBackgroundBrush"), "that key never resolved");
        Assert.That(overlayStyle, Does.Not.Contain("DynamicResource ButtonForegroundBrush"));
        Assert.That(overlayStyle, Does.Contain("<Trigger Property=\"IsPressed\" Value=\"True\">"));
        Assert.That(xaml, Does.Contain("Panel.ZIndex=\"10\""));
        Assert.That(xaml, Does.Contain("ClipToBounds=\"True\""));

        // Active Buses grid told the clerk nothing (fleet GPS is deferred) — replaced by the pin legend.
        Assert.That(xaml, Does.Not.Contain("Active Buses"));
        Assert.That(xaml, Does.Not.Contain("BusListGrid"));
        Assert.That(xaml, Does.Not.Contain("ActiveBuses"));
        Assert.That(xaml, Does.Not.Contain("SelectedBus"));
        Assert.That(xaml, Does.Contain("Pin Legend"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding MarkerLegend}\""));
        Assert.That(xaml, Does.Contain("Fill=\"{Binding FillHex}\""));

        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Not.Contain("ActiveBuses"));
        Assert.That(vm, Does.Not.Contain("IBusService"));
        Assert.That(vm, Does.Contain("MarkerLegend"));
        Assert.That(vm, Does.Contain("ClearMarkersExcept(MapMarkerLabels.Kind.School)"));
        Assert.That(vm, Does.Contain("PlotRouteStopsAsync"));
        Assert.That(vm, Does.Contain("TryTagRouteStop"));

        // Tooltips describe the fixed behaviours (no "Enable in Settings").
        Assert.That(xaml, Does.Not.Contain("Enable in Settings"));
        Assert.That(xaml, Does.Contain("Schools only"));
    }

    [Test]
    public void MapView_RefreshesMarkerTemplatesOnZoomLevelChange()
    {
        // Nested Data.MarkerSize / ShowCaption changes do not remeasure CustomDataSymbol templates.
        var codeBehind = XamlViewFile.Read("Views/Map/MapView.xaml.cs");
        Assert.That(codeBehind, Does.Contain("nameof(MapViewModel.MapZoomLevel)"));
        Assert.That(codeBehind, Does.Contain("RefreshMarkersOnImageryLayer()"));
        Assert.That(codeBehind, Does.Contain("CanHostMarkers()"));
        Assert.That(codeBehind, Does.Contain("DispatcherPriority.Background"));
        var labels = XamlViewFile.Read("Utilities/MapMarkerLabels.cs");
        Assert.That(labels, Does.Contain("MapDefaults.ShowsDetailLabels(zoomLevel)"));
        Assert.That(labels, Does.Not.Contain("kind is Kind.School or Kind.Depot ||"));
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
    public void PickMapForms_UseGoogleTilesLayerNotOsm()
    {
        var school = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml");
        Assert.That(school, Does.Contain("utils:GoogleMapTilesImageryLayer"));
        Assert.That(school, Does.Contain("LayerType=\"Bing\""));
        Assert.That(school, Does.Not.Contain("LayerType=\"OSM\""));

        var stop = XamlViewFile.Read("Views/Student/PickupStopForm.xaml");
        Assert.That(stop, Does.Contain("utils:GoogleMapTilesImageryLayer"));
        Assert.That(stop, Does.Contain("LayerType=\"Bing\""));
        Assert.That(stop, Does.Not.Contain("LayerType=\"OSM\""));

        Assert.That(school, Does.Contain("utils:DistrictSfMap"));
        Assert.That(stop, Does.Contain("utils:DistrictSfMap"));
        Assert.That(school, Does.Not.Contain("maps:SfMap"));
        Assert.That(stop, Does.Not.Contain("maps:SfMap"));

        var schoolCs = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml.cs");
        var stopCs = XamlViewFile.Read("Views/Student/PickupStopForm.xaml.cs");
        Assert.That(schoolCs, Does.Contain("MapTileBootstrap.TryApplyGoogleTilesAsync"));
        Assert.That(stopCs, Does.Contain("MapTileBootstrap.TryApplyGoogleTilesAsync"));
        Assert.That(school, Does.Contain("SchoolPickAttribution"));
        Assert.That(school, Does.Contain("SchoolPickGoogleLogo"));
        Assert.That(stop, Does.Contain("StopPickAttribution"));
        Assert.That(stop, Does.Contain("StopPickGoogleLogo"));
        Assert.That(schoolCs, Does.Contain("RefreshGoogleAttributionAsync"));
        Assert.That(stopCs, Does.Contain("RefreshGoogleAttributionAsync"));
    }

    [Test]
    public void TripBoardView_IsNotTheDailyRouteEditor()
    {
        var xaml = XamlViewFile.Read("Views/Activity/ActivityManagementView.xaml");
        Assert.That(xaml, Does.Contain("Trip Board"));
        Assert.That(xaml, Does.Contain("Import CSV"));
        Assert.That(xaml, Does.Contain("Optimize Day"));
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
