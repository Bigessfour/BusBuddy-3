using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
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
}
