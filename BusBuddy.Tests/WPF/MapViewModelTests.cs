using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapViewModelTests
{
    [Test]
    public async Task SetMapView_ClampsZoomLevel()
    {
        var vm = await CreateSettledViewModelAsync();

        vm.SetMapView(38.1, -102.7, 99);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.MaxZoomLevel));
        Assert.That(vm.MapZoomLevel, Is.EqualTo(19), "SfMap ImageryLayer wheel zoom clamps at 19");

        vm.SetMapView(38.1, -102.7, 0);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.MinZoomLevel));

        vm.SetMapView(38.1535, -102.7195, MapDefaults.SchoolZoomLevel);
        Assert.That(vm.MapCenter.X, Is.EqualTo(38.1535).Within(0.0001));
        Assert.That(vm.MapCenter.Y, Is.EqualTo(-102.7195).Within(0.0001));
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.SchoolZoomLevel));
    }

    [Test]
    public async Task CenterOnMarkers_AveragesPointsAndFitsSpan()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.0, -102.0, null, "A");
        vm.PlotStop(38.2, -103.0, null, "B");

        vm.CenterOnMarkers();

        Assert.That(vm.MapCenter.X, Is.EqualTo(38.1).Within(0.0001));
        Assert.That(vm.MapCenter.Y, Is.EqualTo(-102.5).Within(0.0001));
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0)));
        Assert.That(vm.MapZoomLevel, Is.LessThan(MapDefaults.SchoolZoomLevel));
    }

    [Test]
    public async Task CenterOnMarkers_UsesReportedViewportSize()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.0, -102.0, null, "A");
        vm.PlotStop(38.2, -103.0, null, "B");

        vm.MapViewportSize = new System.Windows.Size(400, 300);
        vm.CenterOnMarkers();
        var small = vm.MapZoomLevel;

        vm.MapViewportSize = new System.Windows.Size(2400, 1400);
        vm.CenterOnMarkers();
        var large = vm.MapZoomLevel;

        Assert.That(large, Is.GreaterThan(small), "a wider viewport fits the same span at a deeper zoom");
        Assert.That(small, Is.EqualTo(MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0, 400, 300)));
        Assert.That(large, Is.EqualTo(MapDefaults.ZoomForBounds(38.0, 38.2, -103.0, -102.0, 2400, 1400)));
    }

    [Test]
    public async Task MapViewportSize_IgnoresEmptyOrInvalidSizes()
    {
        var vm = await CreateSettledViewModelAsync();
        var before = vm.MapViewportSize;

        vm.MapViewportSize = System.Windows.Size.Empty;
        vm.MapViewportSize = new System.Windows.Size(0, 400);
        vm.MapViewportSize = new System.Windows.Size(double.NaN, 400);

        Assert.That(vm.MapViewportSize, Is.EqualTo(before));
        Assert.That(before.Width, Is.EqualTo(MapDefaults.DefaultViewportWidth));
        Assert.That(before.Height, Is.EqualTo(MapDefaults.DefaultViewportHeight));
    }

    [Test]
    public async Task ZoomCommands_StepAroundCurrentCenterAndToggleDetailLabels()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.SetMapView(38.1535, -102.7195, MapDefaults.DetailLabelZoomLevel - 1);
        var center = vm.MapCenter;
        Assert.That(vm.ShowDetailLabels, Is.False);
        // Place pin (pickup) labels at the detail threshold; household pins wait for HomeLabelZoomLevel.
        vm.PlotStop(38.15, -102.72, null, MapMarkerLabels.ForPickup("Main & 5th"), MapMarkerLabels.Kind.Pickup);
        vm.PlotStop(38.16, -102.73, null, MapMarkerLabels.ForHome("Ada"), MapMarkerLabels.Kind.Home);
        var pickup = vm.MapMarkers[0];
        var home = vm.MapMarkers[1];
        Assert.That(pickup.ShowCaption, Is.False);
        Assert.That(home.ShowCaption, Is.False);
        var sizeAtOverview = pickup.MarkerSize;

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        var heldDuringZoom = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapViewModel.MapZoomLevel))
            {
                heldDuringZoom = vm.HoldCenterForZoom;
            }
        };
        vm.ZoomInCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.DetailLabelZoomLevel));
        Assert.That(heldDuringZoom, Is.True, "SfMap ZoomMap must not be allowed to replace the center");
        Assert.That(vm.ShowDetailLabels, Is.True);
        Assert.That(vm.MapCenter, Is.EqualTo(center), "zoom must not move the camera");
        Assert.That(raised, Does.Contain(nameof(MapViewModel.MapZoomLevel)));
        Assert.That(raised, Does.Contain(nameof(MapViewModel.ShowDetailLabels)));
        Assert.That(raised, Does.Not.Contain(nameof(MapViewModel.MapCenter)));
        Assert.That(pickup.ShowCaption, Is.True);
        Assert.That(pickup.Caption, Is.EqualTo("Main & 5th"));
        Assert.That(pickup.MarkerSize, Is.GreaterThan(sizeAtOverview));
        Assert.That(home.ShowCaption, Is.False, "household captions stay hidden until HomeLabelZoomLevel");

        vm.SetMapView(38.1535, -102.7195, MapDefaults.HomeLabelZoomLevel);
        Assert.That(home.ShowCaption, Is.True);
        Assert.That(home.Caption, Is.EqualTo("Ada"));

        vm.SetMapView(38.1535, -102.7195, MapDefaults.DetailLabelZoomLevel);
        vm.ZoomOutCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.DetailLabelZoomLevel - 1));
        Assert.That(vm.ShowDetailLabels, Is.False);
        Assert.That(pickup.ShowCaption, Is.False);
        Assert.That(home.ShowCaption, Is.False);

        vm.SetMapView(38.1535, -102.7195, MapDefaults.MaxZoomLevel);
        vm.ZoomInCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.MaxZoomLevel));
        Assert.That(vm.StatusMessage, Does.Contain("maximum zoom"));
    }

    [Test]
    public async Task MapMarker_LabelChangeRaisesPropertyChanged()
    {
        var vm = await CreateSettledViewModelAsync();
        var marker = vm.PlotStop(38.1, -102.7, new[] { "Ada" });
        var changed = 0;
        marker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapMarker.Label))
            {
                changed++;
            }
        };

        vm.PlotStop(38.1, -102.7, new[] { "Ben" });

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(marker.Label, Does.StartWith("2 students"));
    }

    [Test]
    public void LiveTrackingChrome_IsGoneFromViewModel()
    {
        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Not.Contain("DispatcherTimer"));
        Assert.That(vm, Does.Not.Contain("ReplaceLiveBusMarkers"));
        Assert.That(vm, Does.Not.Contain("ShowAllBuses"));
        Assert.That(vm, Does.Not.Contain("TrackSelectedBus"));
        Assert.That(vm, Does.Not.Contain("IsLiveTrackingEnabled"));
        Assert.That(vm, Does.Contain("PlotPickupStopsCommand"));
        Assert.That(vm, Does.Contain("TryPlotTrip"));
        Assert.That(vm, Does.Contain("LocationCoordinate.IsValidated"));
    }

    [Test]
    public async Task TryPlotTrip_SkipsUnvalidatedCoordinates()
    {
        var vm = await CreateSettledViewModelAsync();
        var trip = new TripEvent
        {
            DestinationName = "Unknown",
            DestinationLocation = new Destination
            {
                Name = "Unknown",
                Address = "x",
                City = "x",
                State = "CO",
                ZipCode = "81052",
                Latitude = 0m,
                Longitude = 0m,
                DestinationType = DestinationTypes.TripDestination
            }
        };

        Assert.That(vm.TryPlotTrip(trip), Is.EqualTo(0));
        Assert.That(vm.MapMarkers, Is.Empty);
        Assert.That(vm.StatusMessage, Does.Contain("not plotted"));
    }

    [Test]
    public async Task TryPlotTrip_PlotsValidatedDestination()
    {
        var vm = await CreateSettledViewModelAsync();
        var trip = new TripEvent
        {
            DestinationName = "Strasburg HS",
            DestinationLocation = new Destination
            {
                Name = "Strasburg HS",
                Address = "1 Main",
                City = "Strasburg",
                State = "CO",
                ZipCode = "80136",
                Latitude = 39.74m,
                Longitude = -104.32m,
                DestinationType = DestinationTypes.TripDestination
            }
        };

        Assert.That(vm.TryPlotTrip(trip), Is.EqualTo(1));
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TryPlotTrip_PlotsValidatedPathPolyline()
    {
        var routing = new Mock<IRoutingService>(MockBehavior.Strict);
        var drivePoints = new[]
        {
            (38.09, -102.62),
            (38.12, -102.70),
            (39.74, -104.32)
        };
        routing.Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default))
            .ReturnsAsync(new DrivePathResult
            {
                EncodedPolyline = EncodedPolylineCodec.Encode(drivePoints),
                Points = drivePoints,
                DistanceMeters = 16093
            });

        var vm = await CreateSettledViewModelAsync(routing: routing.Object);
        var trip = new TripEvent
        {
            OriginName = "Wiley HS",
            OriginLocation = new Destination
            {
                Name = "Wiley HS",
                Address = "1 School",
                City = "Wiley",
                State = "CO",
                ZipCode = "81092",
                Latitude = 38.09m,
                Longitude = -102.62m,
                DestinationType = DestinationTypes.School
            },
            DestinationName = "Strasburg HS",
            DestinationLocation = new Destination
            {
                Name = "Strasburg HS",
                Address = "1 Main",
                City = "Strasburg",
                State = "CO",
                ZipCode = "80136",
                Latitude = 39.74m,
                Longitude = -104.32m,
                DestinationType = DestinationTypes.TripDestination
            },
            PathMiles = 10m
        };

        await vm.TryPlotTripAsync(trip);
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(2));
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);
        Assert.That(vm.RouteLinePoints, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task PlotStop_RaisesMapMarkersChangedOnUpdate()
    {
        var vm = await CreateSettledViewModelAsync();
        var events = 0;
        vm.MapMarkersChanged += (_, _) => events++;

        vm.PlotStop(38.15, -102.72, null, "First");
        var afterAdd = events;
        Assert.That(afterAdd, Is.GreaterThan(0));
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));

        vm.PlotStop(38.15, -102.72, new[] { "Ada" }, "Updated");

        Assert.That(events, Is.GreaterThan(afterAdd));
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
        Assert.That(vm.MapMarkers[0].Label, Does.Contain("Ada").Or.EqualTo("Updated"));
    }

    [Test]
    public void MapViewModelSource_DoesNotInventASampleExportRoute()
    {
        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Contain("MapRouteExporter"));
        Assert.That(vm, Does.Contain("ExportSelectedAsync"));
        Assert.That(vm, Does.Not.Contain("Sample Export Route"));
        Assert.That(vm, Does.Not.Contain("Sample School"));

        var geo = CoreSourceFile.Read("Services/GeoDataService.cs");
        Assert.That(geo, Does.Not.Contain("SampleRoutes"));
        Assert.That(geo, Does.Not.Contain("using sample route"));
        Assert.That(geo, Does.Contain("PersistDerivedWaypointsAsync"));

        var trail = XamlViewFile.Read("Utilities/MapRouteTrail.cs");
        Assert.That(trail, Does.Contain("EnsureWaypointsAsync"));
        Assert.That(trail, Does.Contain("RebuildAndPersistAsync"));
    }

    [Test]
    public async Task ExportRouteDataCommand_WithoutSelection_IsEnabledAndPromptsForARoute()
    {
        // The button is never greyed out or gated by Settings: pressing it always answers the clerk.
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);

        var vm = await CreateSettledViewModelAsync(geo.Object);
        Assert.That(vm.SelectedRoute, Is.Null);

        Assert.That(vm.ExportRouteDataCommand.CanExecute(null), Is.True);
        await ((IAsyncRelayCommand)vm.ExportRouteDataCommand).ExecuteAsync(null);

        Assert.That(vm.StatusMessage, Does.Contain("Select a route"));
        geo.Verify(g => g.GetRouteGeoDataAsync(It.IsAny<int>()), Times.Never);

        var source = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(source, Does.Not.Contain("CanExportRouteData"));
        Assert.That(source, Does.Not.Contain("IUserSettingsService"));
    }

    [Test]
    public async Task RequestMapSnapshot_AsksTheBoundViewToCapture()
    {
        var vm = await CreateSettledViewModelAsync();
        var raised = 0;
        vm.CaptureSnapshotRequested += (_, _) =>
        {
            raised++;
            vm.LatestMapSnapshotPng = new byte[] { 0x89, 0x50 };
        };

        vm.RequestMapSnapshot();

        Assert.That(raised, Is.EqualTo(1));
        Assert.That(vm.LatestMapSnapshotPng, Is.EqualTo(new byte[] { 0x89, 0x50 }));
    }

    [Test]
    public async Task RouteStopOnExistingPin_TagsThatPinInsteadOfStackingASecondCaption()
    {
        // Screenshot 2026-09-15: "Lamar Stop 7 chool" = school caption + WP caption on one spot.
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.0872, -102.6208, null, MapMarkerLabels.ForSchool("Lamar High School"));
        vm.PlotStop(38.0900, -102.6300, null, MapMarkerLabels.ForHome("Ada"));

        var tagged = vm.PlotStop(38.0872, -102.6208, null, "WP Stop 7", MapMarkerLabels.Kind.Waypoint);
        var standalone = vm.PlotStop(38.1000, -102.6400, null, "WP Stop 8", MapMarkerLabels.Kind.Waypoint);

        Assert.That(vm.MapMarkers.Count, Is.EqualTo(3), "no second pin on the school spot");
        Assert.That(tagged.Kind, Is.EqualTo(MapMarkerLabels.Kind.School));
        Assert.That(tagged.RouteStopLabel, Is.EqualTo("Stop 7"));
        Assert.That(tagged.DisplayCaption, Is.EqualTo("Lamar High School (Stop 7)"));
        Assert.That(tagged.StrokeBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.WaypointFillHex)));
        Assert.That(standalone.Kind, Is.EqualTo(MapMarkerLabels.Kind.Waypoint));
        Assert.That(standalone.DisplayCaption, Is.EqualTo("Stop 8"));

        // Route stop named after the pin it sits on adds nothing to the caption (Plot Pickup Stops on a home).
        var home = vm.PlotStop(38.0900, -102.6300, null, MapMarkerLabels.ForRouteStop("Ada"));
        Assert.That(home.Kind, Is.EqualTo(MapMarkerLabels.Kind.Home));
        Assert.That(home.DisplayCaption, Is.EqualTo("Ada"));
        Assert.That(home.RouteStopLabel, Is.EqualTo("Ada"));

        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() => vm.StatusMessage.Contains("Resetting", StringComparison.Ordinal)
            || vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Waypoint));

        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.True,
            "Home recenters and keeps stop pins");
        Assert.That(tagged.RouteStopLabel, Is.EqualTo("Stop 7"));
        Assert.That(tagged.DisplayCaption, Is.EqualTo("Lamar High School (Stop 7)"));
    }

    [Test]
    public void MapMarker_ColoursFollowKind_SchoolsBlack()
    {
        var school = MapMarker.FromDegrees(38.1, -102.7, MapMarkerLabels.ForSchool("Wiley"));
        var pickup = MapMarker.FromDegrees(38.1, -102.7, MapMarkerLabels.ForPickup("Oak"));
        var home = MapMarker.FromDegrees(38.1, -102.7, MapMarkerLabels.ForHome("Ada"));
        var depot = MapMarker.FromDegrees(38.1, -102.7, MapMarkerLabels.ForDepot("Barn"));
        var route = MapMarker.FromDegrees(38.1, -102.7, "WP Start");
        var student = MapMarker.FromDegrees(38.1, -102.7, "Ada");

        Assert.That(MapMarkerLabels.SchoolFillHex, Is.EqualTo("#000000"));
        Assert.That(school.FillBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.SchoolFillHex)));
        Assert.That(school.StrokeBrush.Color.ToString(), Is.EqualTo(BrushHex("#FFFFFF")), "white ring on the black pin");
        Assert.That(pickup.FillBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.PickupFillHex)));
        Assert.That(home.FillBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.HomeFillHex)));
        Assert.That(depot.FillBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.DepotFillHex)));
        Assert.That(route.FillBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.WaypointFillHex)));
        Assert.That(student.FillBrush.Color.ToString(), Is.EqualTo(BrushHex(MapMarkerLabels.StudentFillHex)));

        var fills = new[] { school, pickup, home, depot, route, student }.Select(m => m.FillBrush.Color).Distinct().Count();
        Assert.That(fills, Is.EqualTo(6), "every kind has its own colour");
        Assert.That(school.FillBrush.IsFrozen, Is.True);

        var legendKinds = MapMarkerLabels.Legend.Select(l => l.Kind).ToList();
        Assert.That(legendKinds, Is.EquivalentTo(Enum.GetValues<MapMarkerLabels.Kind>()));
        Assert.That(MapMarkerLabels.Legend.All(l => l.FillHex == MapMarkerLabels.FillHex(l.Kind)), Is.True);
    }

    [Test]
    public async Task ShowSchools_LeavesOnlySchoolPins()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        SetupDistrictMap(geo, WileyAndOak());
        var vm = await CreateSettledViewModelAsync(geo.Object);
        Assert.That(vm.MapMarkers.Select(m => m.Kind).Distinct().Count(), Is.GreaterThan(1), "district overlay is mixed");

        await ((IAsyncRelayCommand)vm.ShowSchoolsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
        Assert.That(vm.MapMarkers.All(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);
        Assert.That(vm.StatusMessage, Does.Contain("Schools only"));

        await ((IAsyncRelayCommand)vm.RefreshMapCommand).ExecuteAsync(null);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True, "Refresh restores the district overlay");
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Home), Is.False,
            "student homes stay off the map until a route is selected (specs/maps.md)");
        Assert.That(vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.School), Is.EqualTo(1), "same-kind pins merge, no duplicates");
    }

    [Test]
    public async Task ShowSchools_ClearsRoutePolyline()
    {
        var route = new Route
        {
            RouteId = 9,
            RouteName = "AM-North",
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
        };
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        SetupDistrictMap(geo, SchoolsOnly());
        var vm = await CreateSettledViewModelAsync(geo.Object);
        vm.SelectedRoute = route;
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);

        await ((IAsyncRelayCommand)vm.ShowSchoolsCommand).ExecuteAsync(null);

        Assert.That(vm.RouteLinePoints, Is.Empty, "schools-only view hides the route polyline");
    }

    [Test]
    public async Task Refresh_WithoutSelectedRoute_DoesNotAutoPickRouteOrTrail()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>
        {
            new()
            {
                RouteId = 1,
                RouteName = "AM-Auto",
                WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
            }
        });
        var vm = await CreateSettledViewModelAsync(geo.Object);
        Assert.That(vm.SelectedRoute, Is.Null);

        await ((IAsyncRelayCommand)vm.RefreshMapCommand).ExecuteAsync(null);

        Assert.That(vm.SelectedRoute, Is.Null);
        Assert.That(vm.RouteLinePoints, Is.Empty);
        Assert.That(vm.StatusMessage, Does.Contain("select a route").IgnoreCase);
    }

    [Test]
    public async Task ShowRoutes_WithSelection_RefreshesThatRoute_NotFirstInList()
    {
        var first = new Route
        {
            RouteId = 1,
            RouteName = "First-With-Waypoints",
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.10, -102.70), (38.11, -102.71)])
        };
        var second = new Route
        {
            RouteId = 2,
            RouteName = "Second-With-Waypoints",
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
        };
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route> { first, second });

        var vm = await CreateSettledViewModelAsync(geo.Object);
        vm.SelectedRoute = second;
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);

        await ((IAsyncRelayCommand)vm.ShowRoutesCommand).ExecuteAsync(null);
        await WaitUntilAsync(() => vm.SelectedRoute!.RouteId == 2);

        Assert.That(vm.SelectedRoute!.RouteId, Is.EqualTo(2));
        Assert.That(vm.SelectedRoute.RouteName, Is.EqualTo("Second-With-Waypoints"));
        Assert.That(vm.RouteLinePoints.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task ShowRoutes_WithoutSelection_DoesNotGuessARoute()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>
        {
            new()
            {
                RouteId = 1,
                RouteName = "First-With-Waypoints",
                WaypointsJson = RouteWaypointSerializer.FromPairs([(38.10, -102.70), (38.11, -102.71)])
            }
        });
        var vm = await CreateSettledViewModelAsync(geo.Object);

        await ((IAsyncRelayCommand)vm.ShowRoutesCommand).ExecuteAsync(null);

        Assert.That(vm.SelectedRoute, Is.Null);
        Assert.That(vm.RouteLinePoints, Is.Empty);
        Assert.That(vm.StatusMessage, Does.Contain("Select a route"));
    }

    [Test]
    public async Task SelectedRoute_ShowsBusNumberLabel_NotAMovingPin()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.SelectedRoute = new Route
        {
            RouteId = 8,
            RouteName = "AM Special Needs Bus 5",
            BusNumber = "5",
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
        };
        await WaitUntilAsync(() => vm.StatusMessage.StartsWith("Bus 5.", StringComparison.Ordinal));

        Assert.That(vm.SelectedRouteBusLabel, Is.EqualTo("Bus 5"));
        Assert.That(vm.StatusMessage, Does.Contain("Bus 5"));
        Assert.That(vm.MapMarkers.Any(m => m.Label != null && m.Label.Contains("GPS", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public async Task InitializeMapData_ListsSchoolsAndStopsThatNeedValidation()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        SetupDistrictMap(geo, new DistrictMapSnapshot
        {
            Schools =
            [
                new DistrictMapPlace { Id = 1, Name = "Wiley School", Latitude = 38.1535, Longitude = -102.7195 }
            ],
            NeedsValidation = ["School: Unvalidated School", "Pickup: No GPS Stop"]
        });

        var vm = await CreateSettledViewModelAsync(geo.Object);

        Assert.That(vm.NeedsValidation, Does.Contain("School: Unvalidated School"));
        Assert.That(vm.NeedsValidation, Does.Contain("Pickup: No GPS Stop"));
        Assert.That(vm.NeedsValidation.Any(n => n.Contains("Wiley School", StringComparison.Ordinal)), Is.False);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForSchool("Wiley School")), Is.True);
    }

    [Test]
    public async Task ShowSchools_WhenDatabaseIsDown_DoesNotClaimSchoolsNeedValidation()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetDistrictMapAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Failed to connect to 192.168.64.1:5432"));
        var vm = await CreateSettledViewModelAsync(geo.Object);
        vm.PlotStop(38.14, -102.73, null, MapMarkerLabels.ForHome("Bea"));

        await ((IAsyncRelayCommand)vm.ShowSchoolsCommand).ExecuteAsync(null);

        Assert.That(vm.StatusMessage, Does.Contain("Database is unavailable"));
        Assert.That(vm.StatusMessage, Does.Not.Contain("validated"));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Home), Is.True,
            "a failed school query must not wipe pins already on the map");
    }

    [Test]
    public async Task PlotPickupStops_WhenDatabaseIsDown_DoesNotClaimStopsNeedValidation()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetDistrictMapAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Failed to connect to 192.168.64.1:5432"));
        var vm = await CreateSettledViewModelAsync(geo.Object);

        await ((IAsyncRelayCommand)vm.PlotPickupStopsCommand).ExecuteAsync(null);

        Assert.That(vm.StatusMessage, Does.Contain("Database is unavailable"));
        Assert.That(vm.StatusMessage, Does.Not.Contain("No pickup"));
    }

    [Test]
    public async Task PlotPickupStops_UsesPublishedRouteStopsWhenCatalogIsEmpty()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>
        {
            new() { RouteId = 3, RouteName = "AM-3", WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)]) }
        });
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);
        SetupDistrictMap(geo, DistrictMapSnapshot.Empty);

        var routeService = new Mock<IRouteService>();
        routeService.Setup(r => r.GetRouteStopsAsync(3)).ReturnsAsync(BusBuddy.Core.Utilities.Result.SuccessResult<IEnumerable<RouteStop>>(
        [
            new RouteStop { RouteStopId = 1, RouteId = 3, StopName = "Oak & 4th", StopOrder = 1, Latitude = 38.15m, Longitude = -102.72m },
            new RouteStop { RouteStopId = 2, RouteId = 3, StopName = "Elm & 9th", StopOrder = 2, Latitude = 38.16m, Longitude = -102.71m },
            new RouteStop { RouteStopId = 3, RouteId = 3, StopName = "unvalidated", StopOrder = 3, Latitude = 0m, Longitude = 0m }
        ]));
        var services = new ServiceCollection();
        services.AddSingleton(routeService.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(geo.Object, scopes: scopes);
        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() => vm.MapMarkers.Count == 0);

        await ((IAsyncRelayCommand)vm.PlotPickupStopsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(2), "two validated route stops; the 0,0 one is skipped");
        Assert.That(vm.MapMarkers.All(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.True);
        Assert.That(vm.MapMarkers.Select(m => m.DisplayCaption), Is.EquivalentTo(new[] { "Oak & 4th", "Elm & 9th" }));
        Assert.That(vm.StatusMessage, Does.Contain("2 pickup stop(s)"));
        Assert.That(vm.StatusMessage, Does.Contain("0 catalog"));
        Assert.That(vm.StatusMessage, Does.Contain("2 on published routes"));
    }

    [Test]
    public async Task PlotStop_AttachesStudentIdsForClerkOverride()
    {
        var vm = await CreateSettledViewModelAsync();
        var marker = vm.PlotStop(
            38.1,
            -102.7,
            new[] { "Ada" },
            MapMarkerLabels.ForHome("Ada"),
            MapMarkerLabels.Kind.Home,
            studentIds: new[] { 7 });

        Assert.That(marker.StudentIds, Is.EqualTo(new[] { 7 }));
        vm.SelectMapMarker(marker);
        Assert.That(vm.SelectedMarker, Is.SameAs(marker));
        Assert.That(((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).CanExecute(null), Is.False,
            "needs a destination route");
    }

    [Test]
    public async Task ApplyClerkOverride_MovesSelectedStudentOntoSelectedPmRoute()
    {
        var planner = new Mock<IRouteDeterminationService>();
        planner.Setup(p => p.ApplyClerkOverrideAsync(
                7, 11, 22, RouteTimeSlotKind.PM, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClerkOverrideResult { Success = true });

        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetStudentByIdAsync(7)).ReturnsAsync(new Student
        {
            StudentId = 7,
            AmRouteId = 10,
            PmRouteId = 11
        });

        var services = new ServiceCollection();
        services.AddSingleton(planner.Object);
        services.AddSingleton(students.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(scopes: scopes);
        var marker = vm.PlotStop(
            38.1,
            -102.7,
            new[] { "Ada" },
            MapMarkerLabels.ForHome("Ada"),
            MapMarkerLabels.Kind.Home,
            studentIds: new[] { 7 });
        vm.SelectMapMarker(marker);
        vm.SelectedRoute = new Route { RouteId = 22, RouteName = "Draft-School-1-PM", Session = RouteSession.PM };

        Assert.That(((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).CanExecute(null), Is.True);
        await ((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).ExecuteAsync(null);

        planner.Verify(
            p => p.ApplyClerkOverrideAsync(7, 11, 22, RouteTimeSlotKind.PM, "District Map", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.That(vm.StatusMessage, Does.Contain("Moved 1 rider"));
        Assert.That(vm.StatusMessage, Does.Contain("Draft-School-1-PM"));
    }

    [Test]
    public async Task ApplyClerkOverride_SchoolPinWithoutStudentId_DoesNotCallPlanner()
    {
        var planner = new Mock<IRouteDeterminationService>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton(planner.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(scopes: scopes);
        var school = vm.PlotStop(38.15, -102.72, null, MapMarkerLabels.ForSchool("Wiley"), MapMarkerLabels.Kind.School);
        vm.SelectMapMarker(school);
        vm.SelectedRoute = new Route { RouteId = 22, RouteName = "Draft-AM", Session = RouteSession.AM };

        Assert.That(((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).CanExecute(null), Is.False);
        await ((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).ExecuteAsync(null);
        planner.VerifyNoOtherCalls();
    }

    private static string BrushHex(string hex) =>
        ((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)).ToString();

    [Test]
    public async Task SelectingRoute_WithoutStoredJson_LoadsDerivedGeoAndDraws()
    {
        var derived = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)]);
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetRouteGeoDataAsync(12)).ReturnsAsync(new Route
        {
            RouteId = 12,
            RouteName = "AM-Derived",
            WaypointsJson = derived
        });

        var vm = await CreateSettledViewModelAsync(geo.Object);
        vm.SelectedRoute = new Route { RouteId = 12, RouteName = "AM-Derived" };
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);

        Assert.That(vm.RouteLinePoints, Has.Count.EqualTo(2));
        Assert.That(vm.SelectedRoute!.WaypointsJson, Is.EqualTo(derived));
        geo.Verify(g => g.GetRouteGeoDataAsync(12), Times.AtLeastOnce);
    }

    [Test]
    public async Task SelectingRoute_WithCompactWaypoints_DrawsLineAndStopMarkersWithoutCallingRoutes()
    {
        var route = new Route
        {
            RouteId = 9,
            RouteName = "AM-North",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71)
            })
        };
        var routing = new Mock<IRoutingService>(MockBehavior.Strict);
        var vm = await CreateSettledViewModelAsync(routing: routing.Object);

        vm.SelectedRoute = route;
        await WaitUntilAsync(() =>
            vm.RouteLinePoints.Count >= 2
            && vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.Waypoint) >= 2);

        Assert.That(vm.RouteLinePoints, Has.Count.EqualTo(2));
        Assert.That(vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.EqualTo(2));
        Assert.That(vm.MapMarkers.Count(m => m.Caption is "Start" or "End"), Is.EqualTo(2));
        routing.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SelectingRoute_WithEncodedPolyline_DoesNotPlotEveryVertex()
    {
        const string encoded = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
        var route = new Route
        {
            RouteId = 10,
            RouteName = "AM-Road",
            WaypointsJson = RouteWaypointSerializer.FromEncodedPolyline(
                encoded,
                new[] { (38.15, -102.72), (38.16, -102.71) })
        };
        var vm = await CreateSettledViewModelAsync();
        vm.SelectedRoute = route;
        await WaitUntilAsync(() =>
            vm.RouteLinePoints.Count >= 2
            && vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.Waypoint) >= 2);

        var decoded = EncodedPolylineCodec.Decode(encoded);
        Assert.That(vm.RouteLinePoints.Count, Is.EqualTo(decoded.Count));
        Assert.That(decoded.Count, Is.GreaterThan(2));
        Assert.That(
            vm.MapMarkers.Count(m => m.Kind == MapMarkerLabels.Kind.Waypoint),
            Is.EqualTo(2));
        Assert.That(vm.MapMarkers.Count(m => m.Caption is "Start" or "End"), Is.EqualTo(2));
    }

    [Test]
    public async Task ClearingSelectedRoute_ClearsTheLine()
    {
        var route = new Route
        {
            RouteId = 11,
            RouteName = "AM-1",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71)
            })
        };
        var vm = await CreateSettledViewModelAsync();
        vm.SelectedRoute = route;
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);

        vm.SelectedRoute = null;
        await WaitUntilAsync(() => vm.RouteLinePoints.Count == 0);

        Assert.That(vm.RouteLinePoints, Is.Empty);
    }

    [Test]
    public async Task ResetView_KeepsTrailAndStopsWhenRouteSelected()
    {
        var route = new Route
        {
            RouteId = 12,
            RouteName = "AM-1",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71)
            })
        };
        var vm = await CreateSettledViewModelAsync();
        vm.SelectedRoute = route;
        await WaitUntilAsync(() =>
            vm.RouteLinePoints.Count >= 2
            && RouteStopVisualCount(vm) >= 2);

        Assert.That(RouteStopVisualCount(vm), Is.EqualTo(2),
            "standalone WP pins or gold tags on overlapping district pins");

        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() =>
            vm.RouteLinePoints.Count >= 2
            && RouteStopVisualCount(vm) >= 2);

        Assert.That(vm.RouteLinePoints, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(RouteStopVisualCount(vm), Is.EqualTo(2));
        Assert.That(vm.SelectedRoute, Is.SameAs(route));
    }

    [Test]
    public async Task ResetView_RecentersToConfiguredDepot_NotUsCentroid()
    {
        var district = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings
        {
            DepotName = "Reset Barn",
            DepotLatitude = 38.1541,
            DepotLongitude = -102.7201
        }));
        var vm = await CreateSettledViewModelAsync(districtSettings: district);
        vm.SetMapView(MapDefaults.UnconfiguredLatitude, MapDefaults.UnconfiguredLongitude, MapDefaults.UnconfiguredZoomLevel);

        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() =>
            Math.Abs(vm.MapCenter.X - 38.1541) < 0.001
            && Math.Abs(vm.MapCenter.Y - (-102.7201)) < 0.001);

        Assert.That(vm.MapCenter.X, Is.EqualTo(38.1541).Within(0.0001));
        Assert.That(vm.MapCenter.Y, Is.EqualTo(-102.7201).Within(0.0001));
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.DistrictZoomLevel));
        Assert.That(vm.MapZoomLevel, Is.Not.EqualTo(MapDefaults.UnconfiguredZoomLevel));
    }

    [Test]
    public async Task CenterOnFleetCommand_FitsMarkersLikeCenterOnStops()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.10, -102.80, null, MapMarkerLabels.ForHome("A"), MapMarkerLabels.Kind.Home);
        vm.PlotStop(38.20, -102.60, null, MapMarkerLabels.ForPickup("B"), MapMarkerLabels.Kind.Pickup);
        vm.MapViewportSize = new System.Windows.Size(800, 600);

        vm.CenterOnStopsCommand.Execute(null);

        Assert.That(vm.MapCenter.X, Is.EqualTo(38.15).Within(0.01));
        Assert.That(vm.MapCenter.Y, Is.EqualTo(-102.70).Within(0.01));
        Assert.That(vm.MapZoomLevel, Is.InRange(MapDefaults.MinFitZoomLevel, MapDefaults.MaxFitZoomLevel));
        Assert.That(vm.MapZoomLevel, Is.Not.EqualTo(MapDefaults.UnconfiguredZoomLevel));
        Assert.That(vm.ShowDetailLabels, Is.EqualTo(vm.MapZoomLevel >= MapDefaults.DetailLabelZoomLevel));
    }

    [Test]
    public async Task PlotStop_DoesNotMergeSchoolAndPickupAtSameCoords()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.15, -102.72, null, MapMarkerLabels.ForSchool("Wiley"), MapMarkerLabels.Kind.School);
        vm.PlotStop(38.15, -102.72, null, MapMarkerLabels.ForPickup("Oak"), MapMarkerLabels.Kind.Pickup);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(2));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForSchool("Wiley")), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForPickup("Oak")), Is.True);
    }

    [Test]
    public async Task PlotStop_MergesSameKindPickupAndKeepsPkLabel()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.16, -102.71, null, MapMarkerLabels.ForPickup("Oak"), MapMarkerLabels.Kind.Pickup);
        vm.PlotStop(38.16, -102.71, new[] { "Ada" }, MapMarkerLabels.ForPickup("Oak"), MapMarkerLabels.Kind.Pickup);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
        Assert.That(vm.MapMarkers[0].Kind, Is.EqualTo(MapMarkerLabels.Kind.Pickup));
        Assert.That(vm.MapMarkers[0].Label, Is.EqualTo(MapMarkerLabels.ForPickup("Oak")));
        Assert.That(vm.MapMarkers[0].StudentNames, Does.Contain("Ada"));
    }

    [Test]
    public async Task PlotStop_DoesNotMergeStudentOntoPickupAcrossKinds()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.16, -102.71, new[] { "Ada" }, "Ada", MapMarkerLabels.Kind.Student);
        vm.PlotStop(38.16, -102.71, null, MapMarkerLabels.ForPickup("Oak"), MapMarkerLabels.Kind.Pickup);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(2));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Student && m.Label == "Ada"), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True);
    }

    [Test]
    public void MapMarkerLabels_UsesSchPkHomeWpDepotPrefixesAndKindOnlyMerge()
    {
        Assert.That(MapMarkerLabels.SchoolPrefix, Is.EqualTo("SCH "));
        Assert.That(MapMarkerLabels.PickupPrefix, Is.EqualTo("PK "));
        Assert.That(MapMarkerLabels.HomePrefix, Is.EqualTo("HOME "));
        Assert.That(MapMarkerLabels.DepotPrefix, Is.EqualTo("DEPOT "));
        Assert.That(MapMarkerLabels.WaypointPrefix, Is.EqualTo("WP "));

        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForSchool("Wiley")), Is.EqualTo(MapMarkerLabels.Kind.School));
        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForHome("Ada")), Is.EqualTo(MapMarkerLabels.Kind.Home));
        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForPickup("Oak")), Is.EqualTo(MapMarkerLabels.Kind.Pickup));
        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForDepot("Barn")), Is.EqualTo(MapMarkerLabels.Kind.Depot));
        Assert.That(MapMarkerLabels.GetKind("WP Start"), Is.EqualTo(MapMarkerLabels.Kind.Waypoint));
        Assert.That(MapMarkerLabels.IsSchoolVisual(MapMarkerLabels.Kind.School), Is.True);
        Assert.That(MapMarkerLabels.IsSchoolVisual(MapMarkerLabels.Kind.Pickup), Is.False);

        Assert.That(MapMarkerLabels.CaptionFrom(MapMarkerLabels.ForSchool("Wiley")), Is.EqualTo("Wiley"));
        Assert.That(MapMarkerLabels.CaptionFrom(MapMarkerLabels.ForHome("Ada")), Is.EqualTo("Ada"));
        Assert.That(MapMarkerLabels.CaptionFrom(MapMarkerLabels.ForPickup("Oak")), Is.EqualTo("Oak"));
        Assert.That(MapMarkerLabels.ZoomScale(MapDefaults.DetailLabelZoomLevel), Is.EqualTo(1.0).Within(0.01));
        Assert.That(
            MapMarkerLabels.ScaledMarkerSize(MapMarkerLabels.Kind.Home, MapDefaults.DistrictZoomLevel),
            Is.LessThan(MapMarkerLabels.ScaledMarkerSize(MapMarkerLabels.Kind.Pickup, MapDefaults.DistrictZoomLevel)));
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.School, 8), Is.False);
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.School, MapDefaults.DetailLabelZoomLevel), Is.True);
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.Home, 8), Is.False);
        // Per-household captions wait two zoom steps longer than place captions (town view stays legible).
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.Home, MapDefaults.DetailLabelZoomLevel), Is.False);
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.Student, MapDefaults.DetailLabelZoomLevel), Is.False);
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.Home, MapDefaults.HomeLabelZoomLevel), Is.True);
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.Pickup, MapDefaults.DetailLabelZoomLevel), Is.True);
        Assert.That(MapMarkerLabels.ShowsCaption(MapMarkerLabels.Kind.Waypoint, MapDefaults.DetailLabelZoomLevel), Is.True);
        Assert.That(MapDefaults.HomeLabelZoomLevel, Is.GreaterThan(MapDefaults.DetailLabelZoomLevel));
        Assert.That(
            MapMarkerLabels.ZoomScale(MapDefaults.DistrictZoomLevel),
            Is.LessThan(MapMarkerLabels.ZoomScale(MapDefaults.DetailLabelZoomLevel)));

        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.Pickup, MapMarkerLabels.Kind.Student), Is.False);
        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.Home, MapMarkerLabels.Kind.Pickup), Is.False);
        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.School, MapMarkerLabels.Kind.Pickup), Is.False);
        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.Pickup, MapMarkerLabels.Kind.Pickup), Is.True);

        Assert.That(
            MapMarkerLabels.ShouldReplaceLabel("Ada", MapMarkerLabels.ForPickup("Oak"), MapMarkerLabels.Kind.Student, MapMarkerLabels.Kind.Pickup),
            Is.False);
        Assert.That(
            MapMarkerLabels.ShouldReplaceLabel(MapMarkerLabels.ForPickup("Oak"), "Ada", MapMarkerLabels.Kind.Pickup, MapMarkerLabels.Kind.Student),
            Is.False);
        Assert.That(StudentPlotLocation.SameSpot(38.15, -102.72, 38.15, -102.72), Is.True);
        Assert.That(StudentPlotLocation.SameSpot(38.15, -102.72, 38.16, -102.72), Is.False);
    }

    [Test]
    public void MapMarkerLabels_GetKind_DistinguishesSchoolHomeAndPickup()
    {
        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForSchool("Wiley")), Is.EqualTo(MapMarkerLabels.Kind.School));
        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForHome("Ada")), Is.EqualTo(MapMarkerLabels.Kind.Home));
        Assert.That(MapMarkerLabels.GetKind(MapMarkerLabels.ForPickup("Oak")), Is.EqualTo(MapMarkerLabels.Kind.Pickup));
        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.School, MapMarkerLabels.Kind.Pickup), Is.False);
        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.Home, MapMarkerLabels.Kind.Pickup), Is.False);
        Assert.That(MapMarkerLabels.CanMerge(MapMarkerLabels.Kind.Home, MapMarkerLabels.Kind.School), Is.False);
    }

    [Test]
    public async Task InitializeMapData_AutoPlotsSchoolsAndPickups_NotAllStudentHomes()
    {
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        SetupDistrictMap(geo, WileyAndOak());

        var vm = await CreateSettledViewModelAsync(geo.Object);

        Assert.That(vm.StatusMessage, Does.StartWith("Map ready"));
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForSchool("Wiley School")), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForPickup("Oak & 4th")), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Home), Is.False,
            "student homes wait for route selection per specs/maps.md");
        Assert.That(
            vm.MapMarkers.Select(m => m.Kind).Distinct().ToList(),
            Is.SupersetOf(new[]
            {
                MapMarkerLabels.Kind.School,
                MapMarkerLabels.Kind.Pickup,
            }));
        var pickupMarker = vm.MapMarkers.Single(m => m.Label == MapMarkerLabels.ForPickup("Oak & 4th"));
        Assert.That(pickupMarker.Kind, Is.EqualTo(MapMarkerLabels.Kind.Pickup));
        Assert.That(pickupMarker.LatitudeDegrees, Is.EqualTo(38.16).Within(0.0001));
        Assert.That(pickupMarker.StudentNames, Is.Empty);
        Assert.That(pickupMarker.Caption, Is.EqualTo("Oak & 4th"));
    }

    [Test]
    public async Task SelectingRoute_PlotsOnlyAssignedStudents()
    {
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetRouteStopsAsync(5))
            .ReturnsAsync(Result.SuccessResult<IEnumerable<RouteStop>>(Array.Empty<RouteStop>()));
        routes.Setup(r => r.GetStudentsForRouteAsync(5, RouteTimeSlot.AM))
            .ReturnsAsync(Result.SuccessResult<List<Student>>(
            [
                new Student
                {
                    StudentId = 1,
                    StudentName = "Ada",
                    Latitude = 38.14m,
                    Longitude = -102.73m
                }
            ]));

        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync(
        [
            new Student
            {
                StudentId = 1,
                StudentName = "Ada",
                Latitude = 38.14m,
                Longitude = -102.73m
            },
            new Student
            {
                StudentId = 2,
                StudentName = "Bea",
                Latitude = 38.15m,
                Longitude = -102.72m
            }
        ]);

        var services = new ServiceCollection();
        services.AddSingleton(routes.Object);
        services.AddSingleton(students.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        SetupDistrictMap(
            geo,
            DistrictMapSnapshot.Empty,
            new Dictionary<int, DistrictMapSnapshot>
            {
                [5] = new DistrictMapSnapshot
                {
                    SelectedRoute = new DistrictMapRoute
                    {
                        RouteId = 5,
                        RouteName = "Special Needs Route",
                        Homes =
                        [
                            new DistrictMapHome
                            {
                                StudentId = 1,
                                StudentName = "Ada",
                                Pins = [new StudentPlotPoint(38.14, -102.73, AtPickup: false, PickupName: null)]
                            }
                        ]
                    }
                }
            });

        var vm = await CreateSettledViewModelAsync(geo.Object, students: students.Object, scopes: scopes);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForHome("Bea")), Is.False);

        vm.SelectedRoute = new Route
        {
            RouteId = 5,
            RouteName = "Special Needs Route",
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
        };
        await WaitUntilAsync(() => vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForHome("Ada")));

        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForHome("Bea")), Is.False);
        geo.Verify(g => g.GetDistrictMapAsync(5, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        routes.Verify(r => r.GetStudentsForRouteAsync(5, RouteTimeSlot.AM), Times.Never);
    }

    [Test]
    public async Task InitializeMapData_WithRouteWaypoints_DoesNotAutoSelectOrRefreshTrail()
    {
        var stored = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)]);
        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(
        [
            new Route { RouteId = 21, RouteName = "AM-Trail", WaypointsJson = stored, IsActive = true }
        ]);
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);
        SetupDistrictMap(geo, SchoolsOnly());

        var routing = new Mock<IRoutingService>(MockBehavior.Strict);

        var vm = await CreateSettledViewModelAsync(
            geoData: geo.Object,
            routing: routing.Object);

        Assert.That(vm.SelectedRoute, Is.Null);
        Assert.That(vm.RouteLinePoints, Is.Empty);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.False);
        routing.Verify(
            r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default),
            Times.Never);
    }

    [Test]
    public async Task BulkPlot_UsesPickupStopInsteadOfHomeAndSkipsGeocode()
    {
        const int routeId = 40;
        var roster = new List<Student>
        {
            new()
            {
                StudentId = 3,
                StudentName = "Cara",
                PickupStopId = 9,
                HomeAddress = "1 Home St",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        };
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetStudentsForRouteAsync(routeId, RouteTimeSlot.AM))
            .ReturnsAsync(Result.SuccessResult(roster));
        routes.Setup(r => r.GetRouteStopsAsync(routeId))
            .ReturnsAsync(Result.SuccessResult<IEnumerable<RouteStop>>(Array.Empty<RouteStop>()));
        var services = new ServiceCollection();
        services.AddSingleton(routes.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var geo = NewGeo();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        SetupDistrictMap(
            geo,
            DistrictMapSnapshot.Empty,
            new Dictionary<int, DistrictMapSnapshot>
            {
                [routeId] = new DistrictMapSnapshot
                {
                    SelectedRoute = new DistrictMapRoute
                    {
                        RouteId = routeId,
                        Homes =
                        [
                            new DistrictMapHome
                            {
                                StudentId = 3,
                                StudentName = "Cara",
                                Pins = [new StudentPlotPoint(38.2, -102.5, AtPickup: true, PickupName: "Main & Elm")]
                            }
                        ]
                    }
                }
            });

        var vm = await CreateSettledViewModelAsync(geo.Object, scopes: scopes);
        vm.SelectedRoute = new Route { RouteId = routeId, RouteName = "AM-Plot" };
        await WaitUntilAsync(() =>
            vm.MapMarkers.Count(m => m.Label == MapMarkerLabels.ForPickup("Main & Elm")) == 1);

        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers.Count(m => m.Label == MapMarkerLabels.ForPickup("Main & Elm")), Is.EqualTo(1));
        Assert.That(
            vm.MapMarkers.Single(m => m.Label == MapMarkerLabels.ForPickup("Main & Elm")).LatitudeDegrees,
            Is.EqualTo(38.2).Within(0.0001));
    }

    [Test]
    public async Task BulkPlot_SkipsHomeWhenPickupAndStoredCoordsAreMissing()
    {
        const int routeId = 41;
        var roster = new List<Student>
        {
            new()
            {
                StudentId = 4,
                StudentName = "Dee",
                HomeAddress = "2 Home St",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        };
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetStudentsForRouteAsync(routeId, RouteTimeSlot.AM))
            .ReturnsAsync(Result.SuccessResult(roster));
        var services = new ServiceCollection();
        services.AddSingleton(routes.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(scopes: scopes);
        vm.SelectedRoute = new Route { RouteId = routeId, RouteName = "AM-Empty" };
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers.Any(m => m.Kind is MapMarkerLabels.Kind.Home or MapMarkerLabels.Kind.Student), Is.False);
    }

    [Test]
    public async Task BulkPlot_SkipsHomeWhenAssignedPickupHasNoCoords()
    {
        const int routeId = 42;
        var roster = new List<Student>
        {
            new()
            {
                StudentId = 8,
                StudentName = "Fay",
                PickupStopId = 9,
                HomeAddress = "3 Home St",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        };
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetStudentsForRouteAsync(routeId, RouteTimeSlot.AM))
            .ReturnsAsync(Result.SuccessResult(roster));
        var services = new ServiceCollection();
        services.AddSingleton(routes.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(scopes: scopes);
        vm.SelectedRoute = new Route { RouteId = routeId, RouteName = "AM-NoGps" };
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers.Any(m => m.Kind is MapMarkerLabels.Kind.Home or MapMarkerLabels.Kind.Student), Is.False);
    }

    [Test]
    public async Task InitializeMapData_SkipsPickupWithoutGps()
    {
        var vm = await CreateSettledViewModelAsync();

        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.False);
    }

    [Test]
    public async Task InitializeMapData_PlotsConfiguredDepot()
    {
        var district = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings
        {
            DepotName = "Lamar Barn",
            DepotLatitude = 38.0866,
            DepotLongitude = -102.6201
        }));
        var vm = await CreateSettledViewModelAsync(districtSettings: district);

        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForDepot("Lamar Barn")), Is.True);
        Assert.That(
            vm.MapMarkers.Single(m => m.Label == MapMarkerLabels.ForDepot("Lamar Barn")).LatitudeDegrees,
            Is.EqualTo(38.0866).Within(0.0001));
    }

    [Test]
    public async Task ApplyDistrictSettings_ReplotsDepotAndRecentersAwayFromUsCentroid()
    {
        var district = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings()));
        var vm = await CreateSettledViewModelAsync(districtSettings: district);

        district.Replace(new RoutingDistrictSettings
        {
            DepotName = "Settings Barn",
            DepotLatitude = 38.1541,
            DepotLongitude = -102.7201,
            BoundingBoxMinLat = 38.05,
            BoundingBoxMaxLat = 38.25,
            BoundingBoxMinLon = -102.80,
            BoundingBoxMaxLon = -102.40
        });

        await vm.ApplyDistrictSettingsAsync();

        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForDepot("Settings Barn")), Is.True);
        Assert.That(vm.MapCenter.X, Is.EqualTo(38.1541).Within(0.0001));
        Assert.That(vm.MapCenter.Y, Is.EqualTo(-102.7201).Within(0.0001));
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.DistrictZoomLevel));
        Assert.That(vm.MapZoomLevel, Is.Not.EqualTo(MapDefaults.UnconfiguredZoomLevel));
        Assert.That(Math.Abs(vm.MapCenter.X - MapDefaults.UnconfiguredLatitude), Is.GreaterThan(0.5));
        // Must not be the Lamar/Wiley US-fail-open remapping alone.
        Assert.That(Math.Abs(vm.MapCenter.X - 38.0872), Is.GreaterThan(0.01));
    }

    [Test]
    public async Task PlotStop_DoesNotMergeHomeAndPickupAtSameCoords()
    {
        var vm = await CreateSettledViewModelAsync();
        vm.PlotStop(38.16, -102.71, new[] { "Ada" }, MapMarkerLabels.ForHome("Ada"), MapMarkerLabels.Kind.Home);
        vm.PlotStop(38.16, -102.71, null, MapMarkerLabels.ForPickup("Oak"), MapMarkerLabels.Kind.Pickup);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(2));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Home), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True);
        Assert.That(
            vm.MapMarkers.Single(m => m.Kind == MapMarkerLabels.Kind.Home).MarkerSize,
            Is.LessThan(vm.MapMarkers.Single(m => m.Kind == MapMarkerLabels.Kind.Pickup).MarkerSize));
        Assert.That(
            vm.MapMarkers.Single(m => m.Kind == MapMarkerLabels.Kind.Home).MarkerSize,
            Is.EqualTo(MapMarkerLabels.ScaledMarkerSize(MapMarkerLabels.Kind.Home, vm.MapZoomLevel)));
    }

    [Test]
    public async Task BulkPlot_NoStoredCoords_DoesNotGeocodeOrPersist()
    {
        const int routeId = 43;
        var roster = new List<Student>
        {
            new()
            {
                StudentId = 5,
                StudentName = "Eve",
                HomeAddress = "9 Nowhere",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        };
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetStudentsForRouteAsync(routeId, RouteTimeSlot.AM))
            .ReturnsAsync(Result.SuccessResult(roster));
        var services = new ServiceCollection();
        services.AddSingleton(routes.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(scopes: scopes);
        vm.SelectedRoute = new Route { RouteId = routeId, RouteName = "AM-Eve" };
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers.Any(m => m.Kind is MapMarkerLabels.Kind.Home or MapMarkerLabels.Kind.Student), Is.False);
    }

    [Test]
    public async Task BulkPlot_WithoutGeocodingService_WritesZeroFakeCoords()
    {
        const int routeId = 44;
        var roster = new List<Student>
        {
            new()
            {
                StudentId = 6,
                StudentName = "Fay",
                HomeAddress = "3 Main",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            },
            new()
            {
                StudentId = 7,
                StudentName = "Gus",
                HomeAddress = "4 Main",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        };
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.GetStudentsForRouteAsync(routeId, RouteTimeSlot.AM))
            .ReturnsAsync(Result.SuccessResult(roster));
        var services = new ServiceCollection();
        services.AddSingleton(routes.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = await CreateSettledViewModelAsync(scopes: scopes);
        vm.SelectedRoute = new Route { RouteId = routeId, RouteName = "AM-NoGeo" };
        await WaitUntilAsync(() => vm.StatusMessage.Contains("no waypoints", StringComparison.OrdinalIgnoreCase));
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers.Any(m => m.Kind is MapMarkerLabels.Kind.Home or MapMarkerLabels.Kind.Student), Is.False);
        Assert.That(vm.StatusMessage, Does.Contain("No assigned students with stored locations").IgnoreCase);
    }

    [Test]
    public async Task BulkPlot_WithoutSelectedRoute_IsDisabled()
    {
        var vm = await CreateSettledViewModelAsync();
        Assert.That(((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).CanExecute(null), Is.False);
    }

    [Test]
    public void MapViewModelSource_ExposesCameraAndMarkerChangeContract()
    {
        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Contain("public Point MapCenter"));
        Assert.That(vm, Does.Contain("public int MapZoomLevel"));
        Assert.That(vm, Does.Contain("public void SetMapView"));
        Assert.That(vm, Does.Contain("event EventHandler? MapMarkersChanged"));
    }

    [Test]
    public void MapViewModel_RequiresMapsGeoDistrictDepotAndDrivePathRefresherInCore()
    {
        var vm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(vm, Does.Contain("ResetCameraToDistrictAsync"));
        Assert.That(vm, Does.Contain("DistrictCameraUi.ResolveHomeAsync"));
        Assert.That(vm, Does.Contain("DistrictDepot.TryGetCoordinates"));
        Assert.That(vm, Does.Contain("PlotPickupStopsCommand"));
        Assert.That(vm, Does.Contain("MapMarkerLabels"));
        Assert.That(vm, Does.Contain("MapDistrictLayers"));
        Assert.That(vm, Does.Contain("MapDefaults.ZoomForBounds"));
        Assert.That(vm, Does.Contain("ResolveDepotMarker"));
        Assert.That(vm, Does.Contain("PlotDepotPins()"));
        Assert.That(vm, Does.Contain("PlotSchoolsAsync()"));
        Assert.That(vm, Does.Contain("PlotPickupsAsync()"));
        Assert.That(vm, Does.Not.Contain("PlotStoredStudentsAsync()"));
        Assert.That(vm, Does.Contain("PlotAssignedStudentsForRouteAsync"));
        Assert.That(vm, Does.Contain("ApplyNeedsValidation"));
        Assert.That(vm, Does.Not.Contain("UpdateMapForRouteAsync(routeWithTrail, refreshDrivePath: true)"));
        Assert.That(vm, Does.Contain("no auto trail"));
        Assert.That(vm, Does.Not.Contain("GenerateEligibilityRoutePdf"));
        Assert.That(vm, Does.Not.Contain("AddMarkerCommand"));
        Assert.That(vm, Does.Not.Contain("PdfReports"));
        Assert.That(vm, Does.Not.Contain("IGeocodingService"));
        Assert.That(vm, Does.Not.Contain("38.0872"));

        var layers = XamlViewFile.Read("Utilities/MapDistrictLayers.cs");
        Assert.That(layers, Does.Contain("GetDistrictMapAsync"));
        Assert.That(layers, Does.Contain("HasValidatedCoordinates"));
        Assert.That(layers, Does.Contain("MapStudentPlot.Draw"));
        Assert.That(layers, Does.Contain("LoadDistrictBaseLayersAsync"));
        Assert.That(layers, Does.Not.Contain("LoadDistrictLayersAsync"));
        Assert.That(layers, Does.Not.Contain("BulkPlotStudentsAsync"));
        Assert.That(layers, Does.Not.Contain("IGeocodingService"));
        Assert.That(layers, Does.Not.Contain("IMapsGeoService"));
        Assert.That(layers, Does.Contain("PlotDepot"));
        Assert.That(layers, Does.Not.Contain("SeedAsync"));

        var geoQuery = CoreSourceFile.Read("Services/GeoDataService.cs");
        Assert.That(geoQuery, Does.Contain("StudentPlotLocation.PinsFromStored"));
        Assert.That(geoQuery, Does.Contain("GetDistrictMapAsync"));

        var plotPolicy = CoreSourceFile.Read("Mapping/StudentPlotLocation.cs");
        Assert.That(plotPolicy, Does.Contain("PinsFromStored"));
        Assert.That(plotPolicy, Does.Contain("HasValidatedHomeCoordinates"));

        var trail = XamlViewFile.Read("Utilities/MapRouteTrail.cs");
        Assert.That(trail, Does.Contain("RouteDrivePathRefresher.TryRefreshAsync"));
        Assert.That(trail, Does.Contain("MarkerStops"));

        var mapsGeo = CoreSourceFile.Read("Services/GoogleMaps/IMapsGeoService.cs");
        Assert.That(mapsGeo, Does.Contain("interface IMapsGeoService"));
        Assert.That(mapsGeo, Does.Contain("IsConfigured"));
        Assert.That(mapsGeo, Does.Contain("GeocodeAsync"));
        Assert.That(mapsGeo, Does.Contain("ReverseGeocodeAsync"));

        var depot = CoreSourceFile.Read("Mapping/DistrictDepot.cs");
        Assert.That(depot, Does.Contain("static class DistrictDepot"));
        Assert.That(depot, Does.Contain("TryGetCoordinates"));

        var refresher = CoreSourceFile.Read("Services/GoogleMaps/RouteDrivePathRefresher.cs");
        Assert.That(refresher, Does.Contain("static class RouteDrivePathRefresher"));
        Assert.That(refresher, Does.Contain("TryRefreshAsync"));
        Assert.That(refresher, Does.Contain("TryReadStepsAsync"));
        Assert.That(vm, Does.Not.Contain("DirectionsForPrintAsync"));
    }

    [Test]
    public void MapViewCodeBehind_SubscribesToMapMarkersChangedWithoutLayerSelectionHandler()
    {
        var codeBehind = XamlViewFile.Read("Views/Map/MapView.xaml.cs");
        Assert.That(codeBehind, Does.Contain("vm.MapMarkersChanged +="));
        Assert.That(codeBehind, Does.Contain("nameof(MapViewModel.MapCenter)"));
        Assert.That(codeBehind, Does.Contain("GeoMap_SizeChanged"));
        Assert.That(codeBehind, Does.Contain("MapCameraHost.TryApply"));
        Assert.That(codeBehind, Does.Contain("TryApplyCameraThenMarkers"));
        Assert.That(codeBehind, Does.Contain("vm.MapViewportSize = size"));
        // Camera is Center + ZoomLevel; no Radius fit, no zoom nudge, no dead camera helpers.
        Assert.That(codeBehind, Does.Not.Contain("MapFitRadiusKm"));
        Assert.That(codeBehind, Does.Not.Contain("imagery.Radius"));
        Assert.That(codeBehind, Does.Not.Contain("DistanceType.KiloMeter"));
        Assert.That(codeBehind, Does.Not.Contain("CaptureVisualMapState"));
        Assert.That(codeBehind, Does.Not.Contain("ResolveClerkCamera"));
        Assert.That(codeBehind, Does.Not.Contain("SchoolZoomLevel"));
        var bootstrap = XamlViewFile.Read("Utilities/MapTileBootstrap.cs");
        Assert.That(bootstrap, Does.Not.Contain("NudgeZoom"));
        Assert.That(bootstrap, Does.Not.Contain("ZoomLevel ="));
        // Map Tiles API Policies: viewport copyright shown for the tiles on screen, debounced per settled camera.
        Assert.That(bootstrap, Does.Contain("RefreshGoogleAttributionAsync"));
        Assert.That(bootstrap, Does.Contain("GetViewportCopyrightAsync"));
        Assert.That(bootstrap, Does.Contain("MapCameraHost.ToLatLon"));
        Assert.That(codeBehind, Does.Contain("ScheduleAttributionRefresh"));
        Assert.That(codeBehind, Does.Contain("_attributionTimer"));
        Assert.That(codeBehind, Does.Not.Contain("MapInteractionDiagnostics"));
        Assert.That(codeBehind, Does.Contain("LatestMapSnapshotPng"));
        Assert.That(codeBehind, Does.Contain("ReplayRouteLineFromViewModel"));
        Assert.That(codeBehind, Does.Contain("MapRouteTrailLayer.Apply"));
        Assert.That(codeBehind, Does.Contain("ApplyMarkerTemplates"));
        Assert.That(codeBehind, Does.Contain("DistrictMarkerTemplateSelector"));
        Assert.That(codeBehind, Does.Contain("OnImageryMarkerSelected"));
        Assert.That(codeBehind, Does.Contain("SelectMapMarker"));
        Assert.That(codeBehind, Does.Not.Contain("CheckBackendConnectivityAsync"));
        Assert.That(codeBehind, Does.Not.Contain("MapControl.Layers.Add"));
        Assert.That(codeBehind, Does.Not.Contain("MapLayerComboBox_SelectionChanged"));
    }

    [Test]
    public void PrintBrief_IncludesStoredMilesAndMinutes()
    {
        var brief = MapPrintBrief.Create(
            "AM-5",
            "Bus 5",
            Array.Empty<MapMarker>(),
            directions: null,
            distanceMiles: 12.4m,
            durationMinutes: 36);

        Assert.That(brief.Lines[0], Is.EqualTo("12.4 mi · 36 min"));
        Assert.That(brief.Subtitle, Does.Contain("not live tracking"));
    }

    private static DistrictMapSnapshot SchoolsOnly() => new()
    {
        Schools =
        [
            new DistrictMapPlace { Id = 1, Name = "Wiley School", Latitude = 38.1535, Longitude = -102.7195 }
        ]
    };

    private static DistrictMapSnapshot WileyAndOak() => new()
    {
        Schools = SchoolsOnly().Schools,
        CatalogStops =
        [
            new DistrictMapPlace { Id = 7, Name = "Oak & 4th", Latitude = 38.16, Longitude = -102.71 }
        ]
    };

    private static Mock<IGeoDataService> NewGeo()
    {
        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);
        geo.Setup(g => g.GetDistrictMapAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DistrictMapSnapshot.Empty);
        return geo;
    }

    private static void SetupDistrictMap(
        Mock<IGeoDataService> geo,
        DistrictMapSnapshot overlay,
        IReadOnlyDictionary<int, DistrictMapSnapshot>? byRoute = null)
    {
        geo.Setup(g => g.GetDistrictMapAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int? id, CancellationToken _) =>
                id is int routeId && byRoute is not null && byRoute.TryGetValue(routeId, out var selected)
                    ? selected
                    : overlay);
    }

    private static MapViewModel CreateViewModel(
        IGeoDataService? geoData = null,
        IRoutingService? routing = null,
        IStudentService? students = null,
        IDistrictSettingsAccessor? districtSettings = null,
        IServiceScopeFactory? scopes = null)
    {
        if (geoData is null)
        {
            geoData = NewGeo().Object;
        }

        return new MapViewModel(
            geoData,
            studentService: students,
            scopeFactory: scopes,
            routingService: routing,
            districtSettings: districtSettings);
    }

    private static async Task<MapViewModel> CreateSettledViewModelAsync(
        IGeoDataService? geoData = null,
        IRoutingService? routing = null,
        IStudentService? students = null,
        IDistrictSettingsAccessor? districtSettings = null,
        IServiceScopeFactory? scopes = null)
    {
        var vm = CreateViewModel(geoData, routing, students, districtSettings, scopes);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (!vm.IsMapLoading &&
                (vm.StatusMessage.StartsWith("Map ready", StringComparison.Ordinal) ||
                 vm.StatusMessage.StartsWith("Error", StringComparison.Ordinal) ||
                 vm.StatusMessage.StartsWith("Map data", StringComparison.Ordinal)))
            {
                await Task.Delay(30);
                return vm;
            }

            await Task.Delay(10);
        }

        return vm;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition())
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                // ObservableCollection mutated while the predicate enumerated it.
            }

            await Task.Delay(20);
        }
    }

    private static int RouteStopVisualCount(MapViewModel vm) =>
        vm.MapMarkers.ToList().Count(m =>
            m.Kind == MapMarkerLabels.Kind.Waypoint || m.RouteStopLabel is not null);
}
