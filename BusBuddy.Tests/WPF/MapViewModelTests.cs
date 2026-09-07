using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Input;
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
        Assert.That(vm.MapZoomLevel, Is.EqualTo(18));

        vm.SetMapView(38.1, -102.7, 0);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(1));

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
        Assert.That(vm.MapFitRadiusKm, Is.EqualTo(MapDefaults.RadiusKilometers(38.0, 38.2, -103.0, -102.0)).Within(0.01));
        Assert.That(vm.MapFitRadiusKm, Is.GreaterThan(20));
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

        var mapVm = XamlViewFile.Read("ViewModels/Map/MapViewModel.cs");
        Assert.That(mapVm, Does.Contain("EnsureRouteWaypointsAsync"));
        Assert.That(mapVm, Does.Contain("RebuildAndPersistAsync"));
    }

    [Test]
    public async Task ExportRouteDataCommand_WhenDisabledInSettings_DoesNotCallGeoService()
    {
        var settings = new Mock<IUserSettingsService>();
        settings.SetupGet(s => s.EnableRouteGeoExport).Returns(false);
        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);

        var vm = await CreateSettledViewModelAsync(geo.Object, settings.Object);
        vm.SelectedRoute = new Route
        {
            RouteId = 7,
            RouteName = "AM-1",
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
        };

        Assert.That(vm.ExportRouteDataCommand.CanExecute(null), Is.True);
        await ((IAsyncRelayCommand)vm.ExportRouteDataCommand).ExecuteAsync(null);

        Assert.That(vm.StatusMessage, Does.Contain("Settings"));
        geo.Verify(g => g.GetRouteGeoDataAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task SelectingRoute_WithoutStoredJson_LoadsDerivedGeoAndDraws()
    {
        var derived = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)]);
        var geo = new Mock<IGeoDataService>();
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
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);

        Assert.That(vm.RouteLinePoints, Has.Count.EqualTo(2));
        Assert.That(vm.MapMarkers.Count(m => m.Label?.StartsWith("WP ", StringComparison.Ordinal) == true), Is.EqualTo(2));
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
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);

        var decoded = EncodedPolylineCodec.Decode(encoded);
        Assert.That(vm.RouteLinePoints.Count, Is.EqualTo(decoded.Count));
        Assert.That(decoded.Count, Is.GreaterThan(2));
        Assert.That(
            vm.MapMarkers.Count(m => m.Label?.StartsWith("WP ", StringComparison.Ordinal) == true),
            Is.EqualTo(2));
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
    public async Task ResetView_ClearsTrailAndWaypointMarkers()
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
        await WaitUntilAsync(() => vm.RouteLinePoints.Count >= 2);
        Assert.That(vm.MapMarkers.Count(m => m.Label?.StartsWith("WP ", StringComparison.Ordinal) == true), Is.EqualTo(2));

        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() => vm.RouteLinePoints.Count == 0);

        Assert.That(vm.RouteLinePoints, Is.Empty);
        Assert.That(vm.MapMarkers.Count(m => m.Label?.StartsWith("WP ", StringComparison.Ordinal) == true), Is.EqualTo(0));
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
    public async Task InitializeMapData_AutoPlotsSchoolsPickupsAndStudentsWithCoords()
    {
        var dest = new Mock<IDestinationService>();
        dest.Setup(d => d.GetActiveSchoolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Destination
                {
                    Name = "Wiley School",
                    Latitude = 38.1535m,
                    Longitude = -102.7195m
                }
            });

        var pickups = new Mock<IPickupStopService>();
        pickups.Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PickupStop
                {
                    PickupStopId = 7,
                    Name = "Oak & 4th",
                    Latitude = 38.16m,
                    Longitude = -102.71m
                }
            });

        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync(
        [
            new Student
            {
                StudentId = 1,
                StudentName = "Ada",
                PickupStopId = 7,
                Latitude = 38.0m,
                Longitude = -102.0m
            },
            new Student
            {
                StudentId = 2,
                StudentName = "Bea",
                Latitude = 38.14m,
                Longitude = -102.73m
            }
        ]);

        var geocode = new Mock<IGeocodingService>(MockBehavior.Strict);
        var vm = await CreateSettledViewModelAsync(
            destinations: dest.Object,
            pickupStops: pickups.Object,
            students: students.Object,
            geocoding: geocode.Object);

        Assert.That(vm.StatusMessage, Does.StartWith("Map ready"));
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForSchool("Wiley School")), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForPickup("Oak & 4th")), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Label == MapMarkerLabels.ForHome("Bea")), Is.True);
        Assert.That(
            vm.MapMarkers.Select(m => m.Kind).Distinct().ToList(),
            Is.SupersetOf(new[]
            {
                MapMarkerLabels.Kind.School,
                MapMarkerLabels.Kind.Pickup,
                MapMarkerLabels.Kind.Home
            }));
        var pickupMarker = vm.MapMarkers.Single(m => m.Label == MapMarkerLabels.ForPickup("Oak & 4th"));
        Assert.That(pickupMarker.Kind, Is.EqualTo(MapMarkerLabels.Kind.Pickup));
        Assert.That(pickupMarker.LatitudeDegrees, Is.EqualTo(38.16).Within(0.0001));
        Assert.That(pickupMarker.StudentNames, Does.Contain("Ada"));
        var adaHome = vm.MapMarkers.Single(m => m.Label == MapMarkerLabels.ForHome("Ada"));
        Assert.That(adaHome.Kind, Is.EqualTo(MapMarkerLabels.Kind.Home));
        Assert.That(adaHome.LatitudeDegrees, Is.EqualTo(38.0).Within(0.0001));
        Assert.That(adaHome.MarkerSize, Is.EqualTo(MapMarkerLabels.HomeMarkerSize));
        Assert.That(adaHome.MarkerSize, Is.LessThan(pickupMarker.MarkerSize));
        geocode.VerifyNoOtherCalls();
    }

    [Test]
    public async Task InitializeMapData_WithRouteWaypoints_RefreshesDrivePathAndKeepsTrailCamera()
    {
        var dest = new Mock<IDestinationService>();
        dest.Setup(d => d.GetActiveSchoolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Destination
                {
                    Name = "Wiley School",
                    Latitude = 38.1535m,
                    Longitude = -102.7195m
                }
            });

        var stored = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)]);
        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(
        [
            new Route { RouteId = 21, RouteName = "AM-Trail", WaypointsJson = stored, IsActive = true }
        ]);
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);

        var routing = new Mock<IRoutingService>(MockBehavior.Strict);
        routing.Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default))
            .ReturnsAsync(new DrivePathResult
            {
                EncodedPolyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
                Points = [(38.15, -102.72), (38.155, -102.715), (38.16, -102.71)],
                DistanceMeters = 400,
                Duration = "45s"
            });

        var vm = await CreateSettledViewModelAsync(
            geoData: geo.Object,
            destinations: dest.Object,
            routing: routing.Object);

        Assert.That(vm.SelectedRoute?.RouteId, Is.EqualTo(21));
        Assert.That(vm.RouteLinePoints.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Waypoint), Is.True);
        Assert.That(vm.MapCenter.X, Is.EqualTo(38.155).Within(0.01));
        routing.Verify(
            r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double Latitude, double Longitude)>>(),
                default),
            Times.Once);
    }

    [Test]
    public async Task BulkPlot_UsesPickupStopInsteadOfHomeAndSkipsGeocode()
    {
        var pickups = new Mock<IPickupStopService>();
        pickups.Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PickupStop
                {
                    PickupStopId = 9,
                    Name = "Main & Elm",
                    Latitude = 38.2m,
                    Longitude = -102.5m
                }
            });

        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync(
        [
            new Student
            {
                StudentId = 3,
                StudentName = "Cara",
                PickupStopId = 9,
                HomeAddress = "1 Home St",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        ]);
        var geocode = new Mock<IGeocodingService>(MockBehavior.Strict);
        var vm = await CreateSettledViewModelAsync(
            pickupStops: pickups.Object,
            students: students.Object,
            geocoding: geocode.Object);

        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
        Assert.That(vm.MapMarkers[0].Label, Is.EqualTo(MapMarkerLabels.ForPickup("Main & Elm")));
        Assert.That(vm.MapMarkers[0].LatitudeDegrees, Is.EqualTo(38.2).Within(0.0001));
        students.Verify(s => s.UpdateStudentAsync(It.IsAny<Student>()), Times.Never);
        geocode.VerifyNoOtherCalls();
    }

    [Test]
    public async Task BulkPlot_GeocodesHomeWhenPickupAndStoredCoordsAreMissing()
    {
        var students = new Mock<IStudentService>();
        var stu = new Student
        {
            StudentId = 4,
            StudentName = "Dee",
            HomeAddress = "2 Home St",
            City = "Wiley",
            State = "CO",
            Zip = "81092"
        };
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync([stu]);
        students.Setup(s => s.UpdateStudentAsync(It.IsAny<Student>())).ReturnsAsync(true);

        var geocode = new Mock<IGeocodingService>();
        geocode.Setup(g => g.GeocodeAsync("2 Home St", "Wiley", "CO", "81092"))
            .ReturnsAsync((38.11, -102.66));

        var vm = await CreateSettledViewModelAsync(students: students.Object, geocoding: geocode.Object);
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
        Assert.That(vm.MapMarkers[0].LatitudeDegrees, Is.EqualTo(38.11).Within(0.0001));
        Assert.That(vm.MapMarkers[0].Label, Is.EqualTo(MapMarkerLabels.ForHome("Dee")));
        students.Verify(s => s.UpdateStudentAsync(It.Is<Student>(x =>
            x.StudentId == 4
            && x.Latitude.HasValue
            && x.Longitude.HasValue
            && Math.Abs((double)x.Latitude.Value - 38.11) < 0.0001
            && Math.Abs((double)x.Longitude.Value - (-102.66)) < 0.0001)), Times.Once);
    }

    [Test]
    public async Task BulkPlot_GeocodesHomeWhenAssignedPickupHasNoCoords()
    {
        var pickups = new Mock<IPickupStopService>();
        pickups.Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PickupStop { PickupStopId = 9, Name = "Unset", Latitude = 0m, Longitude = 0m }
            });

        var students = new Mock<IStudentService>();
        var stu = new Student
        {
            StudentId = 8,
            StudentName = "Fay",
            PickupStopId = 9,
            HomeAddress = "3 Home St",
            City = "Wiley",
            State = "CO",
            Zip = "81092"
        };
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync([stu]);
        students.Setup(s => s.UpdateStudentAsync(It.IsAny<Student>())).ReturnsAsync(true);

        var geocode = new Mock<IGeocodingService>();
        geocode.Setup(g => g.GeocodeAsync("3 Home St", "Wiley", "CO", "81092"))
            .ReturnsAsync((38.12, -102.64));

        var vm = await CreateSettledViewModelAsync(
            pickupStops: pickups.Object,
            students: students.Object,
            geocoding: geocode.Object);
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.False);
        Assert.That(vm.MapMarkers, Has.Count.EqualTo(1));
        Assert.That(vm.MapMarkers[0].Kind, Is.EqualTo(MapMarkerLabels.Kind.Home));
        Assert.That(vm.MapMarkers[0].Label, Is.EqualTo(MapMarkerLabels.ForHome("Fay")));
        Assert.That(vm.MapMarkers[0].LatitudeDegrees, Is.EqualTo(38.12).Within(0.0001));
        Assert.That(vm.MapMarkers[0].MarkerSize, Is.EqualTo(MapMarkerLabels.HomeMarkerSize));
        students.Verify(s => s.UpdateStudentAsync(It.Is<Student>(x => x.StudentId == 8)), Times.Once);
    }

    [Test]
    public async Task InitializeMapData_SkipsPickupWithoutGps()
    {
        var pickups = new Mock<IPickupStopService>();
        pickups.Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PickupStop { PickupStopId = 4, Name = "NoGps" }
            });
        var vm = await CreateSettledViewModelAsync(pickupStops: pickups.Object);

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
            Is.EqualTo(MapMarkerLabels.HomeMarkerSize));
    }

    [Test]
    public async Task BulkPlot_NoGeocodeResult_DoesNotPersistFakeCoords()
    {
        var students = new Mock<IStudentService>();
        var stu = new Student
        {
            StudentId = 5,
            StudentName = "Eve",
            HomeAddress = "9 Nowhere",
            City = "Wiley",
            State = "CO",
            Zip = "81092"
        };
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync([stu]);
        var geocode = new Mock<IGeocodingService>(MockBehavior.Strict);
        geocode.Setup(g => g.GeocodeAsync("9 Nowhere", "Wiley", "CO", "81092"))
            .ReturnsAsync(((double, double)?)null);

        var vm = await CreateSettledViewModelAsync(students: students.Object, geocoding: geocode.Object);
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers, Is.Empty);
        students.Verify(s => s.UpdateStudentAsync(It.IsAny<Student>()), Times.Never);
        geocode.Verify(g => g.GeocodeAsync("9 Nowhere", "Wiley", "CO", "81092"), Times.Once);
    }

    [Test]
    public async Task BulkPlot_WithoutGeocodingService_WritesZeroFakeCoords()
    {
        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync(
        [
            new Student
            {
                StudentId = 6,
                StudentName = "Fay",
                HomeAddress = "3 Main",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            },
            new Student
            {
                StudentId = 7,
                StudentName = "Gus",
                HomeAddress = "4 Main",
                City = "Wiley",
                State = "CO",
                Zip = "81092"
            }
        ]);

        // No IGeocodingService (no Maps API key / unregistered) — fail-open, never invent GPS.
        var vm = await CreateSettledViewModelAsync(students: students.Object, geocoding: null);
        await ((IAsyncRelayCommand)vm.BulkPlotEligibleStudentsCommand).ExecuteAsync(null);

        Assert.That(vm.MapMarkers, Is.Empty);
        Assert.That(vm.StatusMessage, Does.Contain("no locations").IgnoreCase);
        students.Verify(s => s.UpdateStudentAsync(It.IsAny<Student>()), Times.Never);
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
        Assert.That(vm, Does.Contain("DistrictCameraUi.ResolveAsync"));
        Assert.That(vm, Does.Contain("DistrictDepot.TryGetCoordinates"));
        Assert.That(vm, Does.Contain("PlotPickupStopsCommand"));
        Assert.That(vm, Does.Contain("MapMarkerLabels"));
        Assert.That(vm, Does.Contain("MapDistrictLayers"));
        Assert.That(vm, Does.Contain("MapDefaults.ZoomForBounds"));
        Assert.That(vm, Does.Contain("ResolveDepotMarker"));
        Assert.That(vm, Does.Contain("PlotDepotPins()"));
        Assert.That(vm, Does.Contain("PlotSchoolsAsync()"));
        Assert.That(vm, Does.Contain("PlotPickupsAsync()"));
        Assert.That(vm, Does.Contain("PlotStoredStudentsAsync()"));
        Assert.That(vm, Does.Contain("UpdateMapForRouteAsync(routeWithTrail, refreshDrivePath: true)"));

        var layers = XamlViewFile.Read("Utilities/MapDistrictLayers.cs");
        Assert.That(layers, Does.Contain("StudentPlotLocation.PinsFromStored"));
        Assert.That(layers, Does.Contain("HasGpsCoordinates"));
        Assert.That(layers, Does.Contain("MapStudentPlot.Draw"));
        Assert.That(layers, Does.Contain("LoadDistrictLayersAsync"));
        Assert.That(layers, Does.Contain("PlotStoredStudentsAsync"));
        Assert.That(layers, Does.Contain("IGeocodingService"));
        Assert.That(layers, Does.Not.Contain("IMapsGeoService"));
        Assert.That(layers, Does.Contain("PlotDepot"));
        Assert.That(layers, Does.Not.Contain("SeedAsync"));

        var plotPolicy = CoreSourceFile.Read("Mapping/StudentPlotLocation.cs");
        Assert.That(plotPolicy, Does.Contain("PinsFromStored"));
        Assert.That(plotPolicy, Does.Contain("HasGpsCoordinates"));

        var trail = XamlViewFile.Read("Utilities/MapRouteTrail.cs");
        Assert.That(trail, Does.Contain("RouteDrivePathRefresher.TryRefreshAsync"));
        Assert.That(trail, Does.Contain("MarkerStops"));

        var mapsGeo = CoreSourceFile.Read("Services/GoogleMaps/IMapsGeoService.cs");
        Assert.That(mapsGeo, Does.Contain("interface IMapsGeoService"));
        Assert.That(mapsGeo, Does.Contain("IsConfigured"));
        Assert.That(mapsGeo, Does.Contain("GeocodeAsync"));

        var depot = CoreSourceFile.Read("Mapping/DistrictDepot.cs");
        Assert.That(depot, Does.Contain("static class DistrictDepot"));
        Assert.That(depot, Does.Contain("TryGetCoordinates"));

        var refresher = CoreSourceFile.Read("Services/GoogleMaps/RouteDrivePathRefresher.cs");
        Assert.That(refresher, Does.Contain("static class RouteDrivePathRefresher"));
        Assert.That(refresher, Does.Contain("TryRefreshAsync"));
    }

    [Test]
    public void MapViewCodeBehind_SubscribesToMapMarkersChangedWithoutLayerSelectionHandler()
    {
        var codeBehind = XamlViewFile.Read("Views/Map/MapView.xaml.cs");
        Assert.That(codeBehind, Does.Contain("vm.MapMarkersChanged +="));
        Assert.That(codeBehind, Does.Contain("nameof(MapViewModel.MapCenter)"));
        Assert.That(codeBehind, Does.Contain("nameof(MapViewModel.MapFitRadiusKm)"));
        Assert.That(codeBehind, Does.Contain("vm.CenterOnMarkers()"));
        Assert.That(codeBehind, Does.Contain("DistanceType.KiloMeter"));
        Assert.That(codeBehind, Does.Not.Contain("SchoolZoomLevel"));
        Assert.That(codeBehind, Does.Contain("ReplayRouteLineFromViewModel"));
        Assert.That(codeBehind, Does.Contain("MapRouteTrailLayer.Apply"));
        Assert.That(codeBehind, Does.Contain("ApplyMarkerTemplates"));
        Assert.That(codeBehind, Does.Contain("DistrictMarkerTemplateSelector"));
        Assert.That(codeBehind, Does.Not.Contain("CheckBackendConnectivityAsync"));
        Assert.That(codeBehind, Does.Not.Contain("MapControl.Layers.Add"));
        Assert.That(codeBehind, Does.Not.Contain("MapLayerComboBox_SelectionChanged"));
    }

    private static MapViewModel CreateViewModel(
        IGeoDataService? geoData = null,
        IUserSettingsService? userSettings = null,
        IRoutingService? routing = null,
        IPickupStopService? pickupStops = null,
        IDestinationService? destinations = null,
        IStudentService? students = null,
        IGeocodingService? geocoding = null,
        IDistrictSettingsAccessor? districtSettings = null)
    {
        if (geoData is null)
        {
            var geo = new Mock<IGeoDataService>();
            geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
            geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);
            geoData = geo.Object;
        }

        return new MapViewModel(
            geoData,
            geocodingService: geocoding,
            studentService: students,
            userSettings: userSettings,
            routingService: routing,
            pickupStops: pickupStops,
            destinations: destinations,
            districtSettings: districtSettings);
    }

    private static async Task<MapViewModel> CreateSettledViewModelAsync(
        IGeoDataService? geoData = null,
        IUserSettingsService? userSettings = null,
        IRoutingService? routing = null,
        IPickupStopService? pickupStops = null,
        IDestinationService? destinations = null,
        IStudentService? students = null,
        IGeocodingService? geocoding = null,
        IDistrictSettingsAccessor? districtSettings = null)
    {
        var vm = CreateViewModel(geoData, userSettings, routing, pickupStops, destinations, students, geocoding, districtSettings);
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
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }
    }
}
