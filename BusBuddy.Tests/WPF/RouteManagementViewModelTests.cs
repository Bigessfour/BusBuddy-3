using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.ViewModels.Route;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteManagementViewModelTests
{
    [Test]
    public async Task InitializeAsync_LoadsRoutesViaService()
    {
        var routes = new List<Route>
        {
            new() { RouteId = 1, RouteName = "Alpha", IsActive = true },
            new() { RouteId = 2, RouteName = "Beta", IsActive = true },
        };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(routes));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var contextFactory = new Mock<IBusBuddyDbContextFactory>();
        var vm = new RouteManagementViewModel(contextFactory.Object, routeService.Object, null, null);

        await vm.InitializeAsync();

        routeService.Verify(s => s.GetAllRoutesAsync(), Times.Once);
        routeService.Verify(s => s.GetAvailableBusesAsync(), Times.Once);
        routeService.Verify(s => s.GetAvailableDriversAsync(), Times.Once);
        vm.Routes.Should().HaveCount(2);
        vm.StatusMessage.Should().Contain("Loaded 2 routes");
    }

    [Test]
    public async Task AddRouteAsync_CallsCreateRouteAsync()
    {
        var created = new Route { RouteId = 42, RouteName = "Route 120000", IsActive = true };
        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route>()));
        routeService.Setup(s => s.CreateRouteAsync(It.IsAny<Route>()))
            .ReturnsAsync(Result.SuccessResult(created));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);

        if (vm.AddRouteCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand add)
        {
            await add.ExecuteAsync(null);
        }

        routeService.Verify(s => s.CreateRouteAsync(It.IsAny<Route>()), Times.Once);
        vm.SelectedRoute?.RouteId.Should().Be(42);
    }

    [Test]
    public async Task SelectingRoute_EnablesSelectionDependentCommands()
    {
        var routes = new List<Route>
        {
            new() { RouteId = 1, RouteName = "Alpha", IsActive = true, StopCount = 14 }
        };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(routes));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();

        vm.EditRouteCommand.CanExecute(null).Should().BeFalse();
        vm.AssignVehicleCommand.CanExecute(null).Should().BeFalse();
        vm.AssignDriverCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedRoute = vm.Routes[0];

        vm.EditRouteCommand.CanExecute(null).Should().BeTrue();
        vm.CopyRouteCommand.CanExecute(null).Should().BeTrue();
        vm.DeleteRouteCommand.CanExecute(null).Should().BeTrue();
        vm.OpenRouteAssignmentCommand.CanExecute(null).Should().BeTrue();
        vm.GenerateScheduleCommand.CanExecute(null).Should().BeTrue();
        vm.PrintScheduleCommand.CanExecute(null).Should().BeTrue();
        vm.RefreshDrivePathCommand.CanExecute(null).Should().BeTrue();
        vm.OptimizeStopOrderCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void CanRefreshDrivePathFor_RequiresAtLeastTwoStops()
    {
        RouteManagementViewModel.CanRefreshDrivePathFor(new Route { StopCount = null }).Should().BeTrue();
        RouteManagementViewModel.CanRefreshDrivePathFor(new Route { StopCount = 0 }).Should().BeFalse();
        RouteManagementViewModel.CanRefreshDrivePathFor(new Route { StopCount = 1 }).Should().BeFalse();
        RouteManagementViewModel.CanRefreshDrivePathFor(new Route { StopCount = 2 }).Should().BeTrue();
    }

    [Test]
    public async Task RefreshDrivePath_DisabledWhenSelectedRouteHasFewerThanTwoStops()
    {
        var routes = new List<Route>
        {
            new() { RouteId = 16, RouteName = "Copy of Route 174632", IsActive = true, StopCount = 0 }
        };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(routes));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];

        vm.RefreshDrivePathCommand.CanExecute(null).Should().BeFalse();
        vm.PrintScheduleCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public async Task AssignVehicleCommand_UsesSelectedBusId()
    {
        var route = new Route { RouteId = 1, RouteName = "Alpha", IsActive = true, BusNumber = "BUS-5" };
        var bus = new Bus { BusId = 7, BusNumber = "BUS-5" };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { route }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus> { bus }));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));
        routeService.Setup(s => s.AssignVehicleToRouteAsync(1, 7, It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(true));
        routeService.Setup(s => s.GetRouteByIdAsync(1))
            .ReturnsAsync(Result.SuccessResult(route));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];
        vm.SelectedBusId = 7;
        vm.SelectedTimeSlot = RouteTimeSlot.Both;

        vm.AssignVehicleCommand.CanExecute(null).Should().BeTrue();
        if (vm.AssignVehicleCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand assign)
        {
            await assign.ExecuteAsync(null);
        }

        routeService.Verify(s => s.AssignVehicleToRouteAsync(1, 7, RouteTimeSlot.Both), Times.Once);
        vm.StatusMessage.Should().Contain("Assigned bus BUS-5");
    }

    [Test]
    public async Task AssignDriverCommand_UsesSelectedDriverId()
    {
        var route = new Route { RouteId = 1, RouteName = "Alpha", IsActive = true };
        var driver = new Driver { DriverId = 9, DriverName = "Pat Driver" };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { route }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver> { driver }));
        routeService.Setup(s => s.AssignDriverToRouteAsync(1, 9, It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(true));
        routeService.Setup(s => s.GetRouteByIdAsync(1))
            .ReturnsAsync(Result.SuccessResult(new Route
            {
                RouteId = 1,
                RouteName = "Alpha",
                IsActive = true,
                AMDriverId = 9
            }));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];
        vm.SelectedDriverId = 9;
        vm.SelectedTimeSlot = RouteTimeSlot.AM;

        vm.AssignDriverCommand.CanExecute(null).Should().BeTrue();
        if (vm.AssignDriverCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand assign)
        {
            await assign.ExecuteAsync(null);
        }

        routeService.Verify(s => s.AssignDriverToRouteAsync(1, 9, RouteTimeSlot.AM), Times.Once);
        vm.StatusMessage.Should().Contain("Assigned Pat Driver");
        vm.SelectedDriverId.Should().Be(9);
    }

    [Test]
    public async Task GenerateScheduleCommand_PersistsScheduleRow()
    {
        var route = new Route
        {
            RouteId = 1,
            RouteName = "Alpha",
            IsActive = true,
            School = "Wiley School",
            AMVehicleId = 7,
            AMDriverId = 9,
            AMBeginTime = TimeSpan.FromHours(7),
            EstimatedDuration = 40
        };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { route }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var schedule = new Mock<IScheduleService>();
        schedule.Setup(s => s.AddScheduleAsync(It.IsAny<Schedule>()))
            .Returns(Task.CompletedTask);

        var report = new Mock<IOperationalReportService>();
        report.Setup(s => s.GenerateAsync(It.IsAny<OperationalReportRequest>()))
            .ReturnsAsync(new OperationalReportResult
            {
                FilePath = Path.Combine(Path.GetTempPath(), "busbuddy-hop5-missing.pdf")
            });

        var vm = new RouteManagementViewModel(
            new Mock<IBusBuddyDbContextFactory>().Object,
            routeService.Object,
            null,
            scheduleService: schedule.Object,
            reportService: report.Object);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.GenerateScheduleCommand).ExecuteAsync(null);

        schedule.Verify(s => s.AddScheduleAsync(It.Is<Schedule>(row =>
            row.RouteId == 1 && row.BusId == 7 && row.DriverId == 9)), Times.Once);
        report.Verify(s => s.GenerateAsync(It.Is<OperationalReportRequest>(r =>
            r.Kind == OperationalReportKind.DailySchedule && r.RouteId == 1)), Times.Once);
        vm.StatusMessage.Should().Contain("Schedule saved");
    }

    [Test]
    public async Task SelectingRouteWithoutBus_ClearsSelectedBusId()
    {
        var assigned = new Route { RouteId = 1, RouteName = "Alpha", IsActive = true, AMVehicleId = 7, BusNumber = "BUS-5" };
        var unassigned = new Route { RouteId = 2, RouteName = "Beta", IsActive = true };
        var bus = new Bus { BusId = 7, BusNumber = "BUS-5" };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { assigned, unassigned }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus> { bus }));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes.First(r => r.RouteId == 1);
        vm.SelectedBusId.Should().Be(7);

        vm.SelectedRoute = vm.Routes.First(r => r.RouteId == 2);
        vm.SelectedBusId.Should().BeNull();
        vm.AssignVehicleCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public async Task SelectingTimeSlot_SyncsPanelToAmOrPmAssignments()
    {
        var route = new Route
        {
            RouteId = 1,
            RouteName = "Alpha",
            IsActive = true,
            AMVehicleId = 7,
            PMVehicleId = 8,
            AMDriverId = 9,
            PMDriverId = 10,
            BusNumber = "BUS-AM"
        };
        var amBus = new Bus { BusId = 7, BusNumber = "BUS-AM" };
        var pmBus = new Bus { BusId = 8, BusNumber = "BUS-PM" };
        var amDriver = new Driver { DriverId = 9, DriverName = "AM Driver" };
        var pmDriver = new Driver { DriverId = 10, DriverName = "PM Driver" };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { route }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus> { amBus, pmBus }));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver> { amDriver, pmDriver }));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];

        vm.SelectedTimeSlot = RouteTimeSlot.AM;
        vm.SelectedBusId.Should().Be(7);
        vm.SelectedDriverId.Should().Be(9);

        vm.SelectedTimeSlot = RouteTimeSlot.PM;
        vm.SelectedBusId.Should().Be(8);
        vm.SelectedDriverId.Should().Be(10);

        vm.SelectedTimeSlot = RouteTimeSlot.Both;
        vm.SelectedBusId.Should().Be(7);
        vm.SelectedDriverId.Should().Be(9);
    }

    [Test]
    public async Task InitializeAsync_LoadsSchoolDestinationsForGridCombo()
    {
        var dest = new Mock<IDestinationService>();
        dest.Setup(d => d.GetActiveSchoolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Destination>
            {
                new() { DestinationId = 3, Name = "Wiley School", DestinationType = "School" }
            });

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route>()));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var vm = new RouteManagementViewModel(
            new Mock<IBusBuddyDbContextFactory>().Object,
            routeService.Object,
            null,
            dest.Object);

        await vm.InitializeAsync();

        vm.AvailableSchools.Should().ContainSingle(s => s.Name == "Wiley School");
    }

    [Test]
    public async Task RefreshDrivePath_CallsServiceThenReloadsRoute()
    {
        var route = new Route { RouteId = 4, RouteName = "AM Special Needs Bus 5", IsActive = true };
        var refreshed = new Route
        {
            RouteId = 4,
            RouteName = "AM Special Needs Bus 5",
            IsActive = true,
            Distance = 1.00m,
            EstimatedDuration = 3,
            Path = "1.0 mi · 180s",
            WaypointsJson = "{\"encodedPolyline\":\"encoded\"}"
        };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { route }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));
        routeService.Setup(s => s.RefreshDrivePathAsync(4))
            .ReturnsAsync(Result.SuccessResult(DrivePathRefreshResult.Succeeded(new DrivePathResult
            {
                EncodedPolyline = "encoded",
                Points = new[] { (38.07, -102.61), (38.08, -102.62) },
                DistanceMeters = 1609,
                Duration = "180s"
            })));
        routeService.Setup(s => s.GetRouteByIdAsync(4))
            .ReturnsAsync(Result.SuccessResult(refreshed));

        var vm = new RouteManagementViewModel(
            new Mock<IBusBuddyDbContextFactory>().Object,
            routeService.Object,
            null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.RefreshDrivePathCommand).ExecuteAsync(null);

        routeService.Verify(s => s.RefreshDrivePathAsync(4), Times.Once);
        routeService.Verify(s => s.GetRouteByIdAsync(4), Times.Once);
        vm.SelectedRoute!.Distance.Should().Be(1.00m);
        vm.StatusMessage.Should().Contain("Drive path updated");
    }

    [Test]
    public async Task OptimizeStopOrder_ReloadsRouteInsteadOfStalePolyline()
    {
        var route = new Route { RouteId = 4, RouteName = "SN", IsActive = true, WaypointsJson = "[]" };
        var stops = new List<RouteStop>
        {
            new() { RouteStopId = 10, RouteId = 4, StopOrder = 1, Latitude = 38.07m, Longitude = -102.61m },
            new() { RouteStopId = 11, RouteId = 4, StopOrder = 2, Latitude = 38.08m, Longitude = -102.62m },
            new() { RouteStopId = 12, RouteId = 4, StopOrder = 3, Latitude = 38.09m, Longitude = -102.63m },
        };
        var reloaded = new Route
        {
            RouteId = 4,
            RouteName = "SN",
            IsActive = true,
            WaypointsJson = "{\"encodedPolyline\":\"abc\",\"stops\":[]}",
            StopCount = 3
        };

        var optimizer = new Mock<IRouteOptimizationService>();
        optimizer.Setup(s => s.IsConfigured).Returns(true);
        optimizer.Setup(s => s.OptimizeToursAsync(It.IsAny<OptimizeToursProblem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OptimizeToursResult.Ok(new[]
            {
                new OptimizedVisit { ShipmentLabel = "11", IsPickup = true }
            }));

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new List<Route> { route }));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        routeService.Setup(s => s.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));
        routeService.Setup(s => s.GetRouteStopsAsync(4))
            .ReturnsAsync(Result.SuccessResult<IEnumerable<RouteStop>>(stops));
        routeService.Setup(s => s.GetStudentsForRouteAsync(4, It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(new List<Student>()));
        routeService.Setup(s => s.ReorderRouteStopsAsync(4, It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 10, 11, 12 }))))
            .ReturnsAsync(Result.SuccessResult(true));
        routeService.Setup(s => s.GetRouteByIdAsync(4))
            .ReturnsAsync(Result.SuccessResult(reloaded));

        var vm = new RouteManagementViewModel(
            new Mock<IBusBuddyDbContextFactory>().Object,
            routeService.Object,
            null,
            routeOptimization: optimizer.Object);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes[0];

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.OptimizeStopOrderCommand).ExecuteAsync(null);

        routeService.Verify(s => s.UpdateRouteAsync(It.IsAny<Route>()), Times.Never);
        routeService.Verify(s => s.GetRouteByIdAsync(4), Times.Once);
        vm.SelectedRoute!.WaypointsJson.Should().Contain("encodedPolyline");
        vm.StatusMessage.Should().Contain("Stop order optimized");
    }
}
