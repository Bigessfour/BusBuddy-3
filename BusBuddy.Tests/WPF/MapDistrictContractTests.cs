using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Map;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Tough contract checks for <c>specs/maps.md</c> — file locks, not runtime SfMap.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapDistrictContractTests
{
    [Test]
    public void MapViewModel_DoesNotCallBulkPlotStudentsOnInit()
    {
        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Contain("LoadDistrictBaseLayersAsync"));
        Assert.That(vm, Does.Not.Contain("PlotStoredStudentsAsync()"));
        Assert.That(vm, Does.Not.Contain("BulkPlotStudentsAsync()"));
    }

    [Test]
    public void MapViewModel_RouteDraw_SinglePipeline()
    {
        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Contain("UpdateMapForRouteAsync"));
        Assert.That(vm, Does.Contain("MapRouteTrail.Build"));
        Assert.That(vm, Does.Contain("RouteLineUpdated"));
        Assert.That(vm, Does.Not.Contain("ImageryLayer.Markers"));
    }

    [Test]
    public void MapView_CodeBehind_DoesNotBuildPolylines()
    {
        var view = XamlViewFile.Read("Views/Map/MapView.xaml.cs");
        Assert.That(view, Does.Contain("MapRouteTrailLayer.Apply"));
        Assert.That(view, Does.Not.Contain("RouteDrivePathRefresher"));
        Assert.That(view, Does.Not.Contain("ComputeDrivePathAsync"));
    }

    [Test]
    public void MapDistrictLayers_AssignedRosterEntryPoint()
    {
        var layers = XamlViewFile.Read("Utilities/MapDistrictLayers.cs");
        Assert.That(layers, Does.Contain("PlotAssignedStudentsForRouteAsync"));
        Assert.That(layers, Does.Contain("GetStudentsForRouteAsync"));
    }

    [Test]
    public void StudentsMapCoordinator_DoesNotGeocodeOnPlot()
    {
        var coord = XamlViewFile.Read("ViewModels/Student/StudentsMapCoordinator.cs");
        Assert.That(coord, Does.Contain("PinsFromStored"));
        Assert.That(coord, Does.Not.Contain("GeocodeAsync"));
    }

    [Test]
    public void ToolbarSpec_CommandsExistInXaml()
    {
        var xaml = XamlViewFile.Read("Views/Map/MapView.xaml");
        Assert.That(xaml, Does.Contain("CenterOnStopsCommand"));
        Assert.That(xaml, Does.Contain("ShowRoutesCommand"));
        Assert.That(xaml, Does.Contain("BulkPlotEligibleStudentsCommand"));
        Assert.That(xaml, Does.Contain("ApplyClerkOverrideCommand"));
        Assert.That(xaml, Does.Not.Contain("LayerType=\"OSM\""));
    }

    [Test]
    public void RouteDistrictMapLabels_DisambiguatesRows()
    {
        var route = new Route { RouteId = 4, RouteName = "Special Needs Route", Session = RouteSession.SpecialNeeds };
        Assert.That(RouteDistrictMapLabels.ListCaption(route), Does.Contain("Special Needs Route"));
        Assert.That(RouteDistrictMapLabels.ListCaption(route), Does.Contain("SpecialNeeds"));
    }
}
