using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
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

        var contextFactory = new Mock<IBusBuddyDbContextFactory>();
        var vm = new RouteManagementViewModel(contextFactory.Object, routeService.Object, null, null);

        await vm.InitializeAsync();

        routeService.Verify(s => s.GetAllRoutesAsync(), Times.Once);
        routeService.Verify(s => s.GetAvailableBusesAsync(), Times.Once);
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
            new() { RouteId = 1, RouteName = "Alpha", IsActive = true }
        };

        var routeService = new Mock<IRouteService>();
        routeService.Setup(s => s.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(routes));
        routeService.Setup(s => s.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();

        vm.EditRouteCommand.CanExecute(null).Should().BeFalse();
        vm.AssignVehicleCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedRoute = vm.Routes[0];

        vm.EditRouteCommand.CanExecute(null).Should().BeTrue();
        vm.CopyRouteCommand.CanExecute(null).Should().BeTrue();
        vm.DeleteRouteCommand.CanExecute(null).Should().BeTrue();
        vm.OpenRouteAssignmentCommand.CanExecute(null).Should().BeTrue();
        vm.GenerateScheduleCommand.CanExecute(null).Should().BeTrue();
        vm.PrintScheduleCommand.CanExecute(null).Should().BeTrue();
        vm.RefreshDrivePathCommand.CanExecute(null).Should().BeTrue();
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

        var vm = new RouteManagementViewModel(new Mock<IBusBuddyDbContextFactory>().Object, routeService.Object, null, null);
        await vm.InitializeAsync();
        vm.SelectedRoute = vm.Routes.First(r => r.RouteId == 1);
        vm.SelectedBusId.Should().Be(7);

        vm.SelectedRoute = vm.Routes.First(r => r.RouteId == 2);
        vm.SelectedBusId.Should().BeNull();
        vm.AssignVehicleCommand.CanExecute(null).Should().BeFalse();
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

        var vm = new RouteManagementViewModel(
            new Mock<IBusBuddyDbContextFactory>().Object,
            routeService.Object,
            null,
            dest.Object);

        await vm.InitializeAsync();

        vm.AvailableSchools.Should().ContainSingle(s => s.Name == "Wiley School");
    }
}
