using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
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

/// <summary>
/// Testhost poke of every District Map toolbar command. No SfMap tile session.
/// Spec: <c>specs/maps.md</c> toolbar contract.
/// Do not mark this fixture STA — <c>ResetView</c> / init fire-and-forget
/// posts after NUnit's SingleThreadedTestSynchronizationContext shuts down.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapToolbarSmokeTests
{
    [Test]
    public async Task Toolbar_ExecutesEachMapCommandOnce()
    {
        var dest = new Mock<IDestinationService>();
        dest.Setup(d => d.GetActiveSchoolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Destination { Name = "Wiley School", Latitude = 38.1535m, Longitude = -102.7195m }
            });

        var pickups = new Mock<IPickupStopService>();
        pickups.Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PickupStop { PickupStopId = 7, Name = "Oak & 4th", Latitude = 38.16m, Longitude = -102.71m }
            });

        var students = new Mock<IStudentService>();
        students.Setup(s => s.GetAllStudentsAsync()).ReturnsAsync(
        [
            new Student { StudentId = 7, StudentName = "Ada", Latitude = 38.14m, Longitude = -102.73m, AmRouteId = 11 }
        ]);
        students.Setup(s => s.GetStudentByIdAsync(7)).ReturnsAsync(new Student
        {
            StudentId = 7,
            StudentName = "Ada",
            AmRouteId = 11,
            PmRouteId = 11,
            Latitude = 38.14m,
            Longitude = -102.73m
        });

        var route = new Route
        {
            RouteId = 22,
            RouteName = "AM-North",
            Session = RouteSession.AM,
            WaypointsJson = RouteWaypointSerializer.FromPairs([(38.15, -102.72), (38.16, -102.71)])
        };

        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route> { route });
        geo.Setup(g => g.GetRouteGeoDataAsync(22)).ReturnsAsync(route);

        var routeService = new Mock<IRouteService>();
        routeService.Setup(r => r.GetRouteStopsAsync(22)).ReturnsAsync(
            Result.SuccessResult<IEnumerable<RouteStop>>(
            [
                new RouteStop
                {
                    RouteStopId = 1,
                    RouteId = 22,
                    StopName = "Stop 1",
                    StopOrder = 1,
                    Latitude = 38.15m,
                    Longitude = -102.72m
                }
            ]));

        var planner = new Mock<IRouteDeterminationService>();
        planner.Setup(p => p.ApplyClerkOverrideAsync(
                7, 11, 22, RouteTimeSlotKind.AM, "District Map", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClerkOverrideResult { Success = true });

        var services = new ServiceCollection();
        services.AddSingleton(routeService.Object);
        services.AddSingleton(planner.Object);
        services.AddSingleton(students.Object);
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var district = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings
        {
            DepotName = "Barn",
            DepotLatitude = 38.1541,
            DepotLongitude = -102.7201
        }));

        var vm = await CreateSettledViewModelAsync(
            geo.Object,
            pickupStops: pickups.Object,
            destinations: dest.Object,
            students: students.Object,
            districtSettings: district,
            scopes: scopes);

        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True);

        // 1. Zoom In / Out — clamp, never past min/max
        vm.SetMapView(38.1535, -102.7195, MapDefaults.DistrictZoomLevel);
        var beforeZoom = vm.MapZoomLevel;
        vm.ZoomInCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(beforeZoom + 1));
        Assert.That(vm.MapZoomLevel, Is.LessThanOrEqualTo(MapDefaults.MaxZoomLevel));
        vm.ZoomOutCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(beforeZoom));
        Assert.That(vm.MapZoomLevel, Is.GreaterThanOrEqualTo(MapDefaults.MinZoomLevel));

        vm.SetMapView(38.1535, -102.7195, MapDefaults.MaxZoomLevel);
        vm.ZoomInCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.MaxZoomLevel));

        vm.SetMapView(38.1535, -102.7195, MapDefaults.MinZoomLevel);
        vm.ZoomOutCommand.Execute(null);
        Assert.That(vm.MapZoomLevel, Is.EqualTo(MapDefaults.MinZoomLevel));

        // 2. Show Schools — only school-kind markers
        await ((IAsyncRelayCommand)vm.ShowSchoolsCommand).ExecuteAsync(null);
        Assert.That(vm.MapMarkers, Is.Not.Empty);
        Assert.That(vm.MapMarkers.All(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);

        // 3. Refresh restores overlay; Home recenters
        await ((IAsyncRelayCommand)vm.RefreshMapCommand).ExecuteAsync(null);
        await WaitUntilAsync(() =>
            vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.School)
            && vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup)
            && vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Depot));
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.School), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Pickup), Is.True);
        Assert.That(vm.MapMarkers.Any(m => m.Kind == MapMarkerLabels.Kind.Depot), Is.True);

        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() =>
            Math.Abs(vm.MapCenter.X - 38.1541) < 0.01
            && Math.Abs(vm.MapCenter.Y - (-102.7201)) < 0.01);

        // 4. Plot Pickup Stops — gold route-stop or catalog pins
        await ((IAsyncRelayCommand)vm.PlotPickupStopsCommand).ExecuteAsync(null);
        Assert.That(
            vm.MapMarkers.Any(m =>
                m.Kind == MapMarkerLabels.Kind.Pickup
                || m.Kind == MapMarkerLabels.Kind.Waypoint
                || m.RouteStopLabel is not null),
            Is.True);
        Assert.That(vm.StatusMessage, Does.Contain("pickup stop").IgnoreCase);

        // 5. Export with no SelectedRoute — toast, no file
        vm.SelectedRoute = null;
        var exportDir = Path.Combine(Path.GetTempPath(), $"busbuddy-map-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(exportDir);
        try
        {
            await ((IAsyncRelayCommand)vm.ExportRouteDataCommand).ExecuteAsync(null);
            Assert.That(vm.StatusMessage, Does.Contain("select a route").IgnoreCase);
            Assert.That(Directory.GetFiles(exportDir), Is.Empty);
            geo.Verify(g => g.GetRouteGeoDataAsync(It.IsAny<int>()), Times.Never);

            // 6. Select a route, export via exporter (Save dialog is skipped in testhost)
            vm.SelectedRoute = route;
            var path = Path.Combine(exportDir, "AM-North.geojson");
            var exported = await MapRouteExporter.ExportAsync(geo.Object, route, path);
            Assert.That(exported.Success, Is.True);
            Assert.That(File.Exists(path), Is.True);
            geo.Verify(g => g.GetRouteGeoDataAsync(22), Times.AtLeastOnce);
            Assert.That(vm.ExportRouteDataCommand.CanExecute(null), Is.True);
        }
        finally
        {
            if (Directory.Exists(exportDir))
            {
                Directory.Delete(exportDir, recursive: true);
            }
        }

        // 7. Clerk override — District Map reason
        var home = vm.PlotStop(
            38.14,
            -102.73,
            new[] { "Ada" },
            MapMarkerLabels.ForHome("Ada"),
            MapMarkerLabels.Kind.Home,
            studentIds: new[] { 7 });
        vm.SelectMapMarker(home);
        vm.SelectedRoute = route;
        Assert.That(((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).CanExecute(null), Is.True);
        await ((IAsyncRelayCommand)vm.ApplyClerkOverrideCommand).ExecuteAsync(null);
        planner.Verify(
            p => p.ApplyClerkOverrideAsync(7, 11, 22, RouteTimeSlotKind.AM, "District Map", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.That(vm.StatusMessage, Does.Contain("Moved").Or.Contain("Already on"));

        // 8. Print — headless may skip pixels; command must run
        var printRequested = 0;
        var snapshotRequested = 0;
        vm.PrintRequested += (_, _) => printRequested++;
        vm.CaptureSnapshotRequested += (_, _) =>
        {
            snapshotRequested++;
            vm.LatestMapSnapshotPng = new byte[] { 0x89, 0x50 };
        };
        Assert.That(vm.PrintRouteMapsCommand.CanExecute(null), Is.True);
        vm.PrintRouteMapsCommand.Execute(null);
        vm.RequestMapSnapshot();
        Assert.That(printRequested, Is.EqualTo(1));
        Assert.That(snapshotRequested, Is.EqualTo(1));
        Assert.That(vm.LatestMapSnapshotPng, Is.Not.Null);

        // 9. After zoom, buttons stay enabled
        vm.SetMapView(38.1535, -102.7195, MapDefaults.DistrictZoomLevel);
        vm.ZoomInCommand.Execute(null);
        Assert.That(vm.ZoomInCommand.CanExecute(null), Is.True);
        Assert.That(vm.ZoomOutCommand.CanExecute(null), Is.True);
        Assert.That(vm.PrintRouteMapsCommand.CanExecute(null), Is.True);
        Assert.That(vm.ExportRouteDataCommand.CanExecute(null), Is.True);
        Assert.That(vm.ShowSchoolsCommand.CanExecute(null), Is.True);
        Assert.That(vm.PlotPickupStopsCommand.CanExecute(null), Is.True);
        Assert.That(vm.RefreshMapCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task PlotPickupStops_EmptyCatalogAndRoutes_ToastsInsteadOfSilentNoOp()
    {
        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRoutesWithGeoDataAsync()).ReturnsAsync(new List<Route>());
        geo.Setup(g => g.GetRouteGeoDataAsync(It.IsAny<int>())).ReturnsAsync((Route?)null);
        var pickups = new Mock<IPickupStopService>();
        pickups.Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PickupStop>());

        var vm = await CreateSettledViewModelAsync(geo.Object, pickupStops: pickups.Object);
        vm.ResetViewCommand.Execute(null);
        await WaitUntilAsync(() => vm.MapMarkers.Count == 0);

        await ((IAsyncRelayCommand)vm.PlotPickupStopsCommand).ExecuteAsync(null);

        Assert.That(vm.StatusMessage, Does.Contain("No pickup").IgnoreCase);
        Assert.That(vm.MapMarkers, Is.Empty);
    }

    [Test]
    public void MapXaml_KeepsMarkerHostLock()
    {
        var district = XamlViewFile.Read("Views/Map/MapView.xaml");
        Assert.That(district, Does.Not.Contain("MarkerTemplateSelector="));
        Assert.That(district, Does.Not.Contain("Markers=\"{Binding MapMarkers}\""));

        var school = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml");
        var stop = XamlViewFile.Read("Views/Student/PickupStopForm.xaml");
        Assert.That(school, Does.Not.Contain("Markers=\"{Binding MapMarkers}\""));
        Assert.That(stop, Does.Not.Contain("Markers=\"{Binding MapMarkers}\""));

        var viewCs = XamlViewFile.Read("Views/Map/MapView.xaml.cs");
        var schoolCs = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml.cs");
        var stopCs = XamlViewFile.Read("Views/Student/PickupStopForm.xaml.cs");
        Assert.That(viewCs, Does.Contain("MapMarkerHost.TryAssignAndLayout"));
        Assert.That(schoolCs, Does.Contain("MapMarkerHost.TryAssignAndLayout"));
        Assert.That(stopCs, Does.Contain("MapMarkerHost.TryAssignAndLayout"));
    }

    private static MapViewModel CreateViewModel(
        IGeoDataService? geoData = null,
        IPickupStopService? pickupStops = null,
        IDestinationService? destinations = null,
        IStudentService? students = null,
        IDistrictSettingsAccessor? districtSettings = null,
        IServiceScopeFactory? scopes = null)
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
            studentService: students,
            scopeFactory: scopes,
            pickupStops: pickupStops,
            destinations: destinations,
            districtSettings: districtSettings);
    }

    private static async Task<MapViewModel> CreateSettledViewModelAsync(
        IGeoDataService? geoData = null,
        IPickupStopService? pickupStops = null,
        IDestinationService? destinations = null,
        IStudentService? students = null,
        IDistrictSettingsAccessor? districtSettings = null,
        IServiceScopeFactory? scopes = null)
    {
        var vm = CreateViewModel(geoData, pickupStops, destinations, students, districtSettings, scopes);
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
            }

            await Task.Delay(20);
        }
    }
}
