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
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedMapLayer, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("ZoomLevel=\"{Binding MapZoomLevel, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Radius=\"{Binding MapFitRadiusKm}\""));
        Assert.That(xaml, Does.Contain("DistanceType=\"KiloMeter\""));
        Assert.That(xaml, Does.Contain("x:Name=\"RouteTrail\""));
        Assert.That(xaml, Does.Contain("x:Name=\"RouteTrailLayer\""));
        Assert.That(xaml, Does.Contain("SubShapeFileLayers"));
        Assert.That(xaml, Does.Contain("maps:MapPolyline"));
        Assert.That(XamlViewFile.Read("Utilities/MapRouteTrailLayer.cs"), Does.Contain("polyline.Points.Clear"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding MapLayers}\""));
        Assert.That(xaml, Does.Not.Contain("MapLayerComboBox_SelectionChanged"));
        Assert.That(xaml, Does.Not.Contain("ZoomLevel=\"13\""));
        Assert.That(xaml, Does.Contain("Fleet GPS tracking is not enabled yet"));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Fleet GPS status\""));
        Assert.That(xaml, Does.Not.Contain("Show All Buses"));
        Assert.That(xaml, Does.Not.Contain("Track Selected"));
        Assert.That(xaml, Does.Not.Contain("IsLiveTrackingEnabled"));
        Assert.That(xaml, Does.Not.Contain("TrackingIntervalIndex"));
        Assert.That(xaml, Does.Not.Contain("FluentDarkTheme.xaml"));
        Assert.That(xaml, Does.Not.Contain("#AA2B2B2B"));
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

        var vm = XamlViewFile.Read("ViewModels/Student/StudentsViewModel.cs");
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
        var form = XamlViewFile.Read("ViewModels/Student/StudentFormViewModel.cs");
        Assert.That(form, Does.Contain("StudentPlotLocation.PinsFromStored"));
        Assert.That(form, Does.Contain("MapStudentPlot.Draw"));
        Assert.That(form, Does.Contain("ResolvePickupCatalogForPlotAsync"));
    }
}
