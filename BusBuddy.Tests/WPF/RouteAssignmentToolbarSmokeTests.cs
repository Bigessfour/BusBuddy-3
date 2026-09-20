using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.ViewModels.Route;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Testhost poke of Route Assignments ribbon commands (no live SfMap / Save dialogs).
/// Layout contract: pickers on row 1; Fleet / Route / Publish ribbon rows.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteAssignmentToolbarSmokeTests
{
    [Test]
    public async Task Ribbon_RefreshAndCommandGates_AfterRouteLoad()
    {
        var route = new Route
        {
            RouteId = 4,
            RouteName = "AM Special Needs Bus 5",
            Session = RouteSession.AM,
            IsActive = true
        };

        var stops = new List<RouteStop>
        {
            new()
            {
                RouteStopId = 1,
                RouteId = 4,
                StopName = "Stop 1",
                StopOrder = 1,
                Latitude = 38.15m,
                Longitude = -102.72m
            },
            new()
            {
                RouteStopId = 2,
                RouteId = 4,
                StopName = "Stop 2",
                StopOrder = 2,
                Latitude = 38.16m,
                Longitude = -102.71m
            }
        };

        var routes = Mock.Of<IRouteService>();
        var mock = Mock.Get(routes);
        mock.Setup(r => r.GetUnassignedStudentsAsync(It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(new List<Student>()));
        mock.Setup(r => r.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new[] { route }));
        mock.Setup(r => r.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        mock.Setup(r => r.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));
        mock.Setup(r => r.GetRouteStopsAsync(4))
            .ReturnsAsync(Result.SuccessResult<IEnumerable<RouteStop>>(stops));
        mock.Setup(r => r.GetStudentsForRouteAsync(4, It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(new List<Student>()));
        mock.Setup(r => r.GetRiderExceptionStudentIdsAsync(4, It.IsAny<DateTime>()))
            .ReturnsAsync(Result.SuccessResult<IReadOnlyList<int>>(Array.Empty<int>()));

        var vm = new RouteAssignmentViewModel(routes);
        await WaitUntilAsync(() => !vm.IsLoading && vm.AvailableRoutes.Count > 0 && vm.SelectedRoute != null);

        Assert.That(vm.SelectedRoute!.RouteId, Is.EqualTo(4));
        Assert.That(vm.RouteStops, Has.Count.EqualTo(2));

        vm.RefreshDataCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsLoading && vm.StatusMessage.Contains("Data refreshed successfully", StringComparison.Ordinal));
        mock.Verify(r => r.GetAllRoutesAsync(), Times.AtLeast(2));
        Assert.That(vm.StatusMessage, Does.Contain("Data refreshed successfully"));

        Assert.That(vm.RefreshDataCommand.CanExecute(null), Is.True);
        Assert.That(vm.ViewScheduleCommand.CanExecute(null), Is.True);
        Assert.That(vm.TimeRouteCommand.CanExecute(null), Is.True);
        Assert.That(vm.RefreshDrivePathCommand.CanExecute(null), Is.True);
        Assert.That(vm.PlotRouteOnMapCommand.CanExecute(null), Is.True);
        Assert.That(vm.PrintRouteSheetCommand.CanExecute(null), Is.True);
        Assert.That(vm.ExportRouteSheetCommand.CanExecute(null), Is.True);
        Assert.That(vm.GenerateRoutesCommand.CanExecute(null), Is.True);
        Assert.That(vm.GenerateTransferRoutesCommand.CanExecute(null), Is.True);

        vm.PlotRouteOnMapCommand.Execute(null);
        await Task.Delay(200);
        Assert.That(vm.StatusMessage, Does.Contain("Map VM not registered").Or.Contain("No students").IgnoreCase);
    }

    [Test]
    public async Task TimeRoute_SpreadsPathDurationAcrossStops()
    {
        var route = new Route
        {
            RouteId = 4,
            RouteName = "AM Special Needs Bus 5",
            Session = RouteSession.AM,
            IsActive = true,
            EstimatedDuration = 60
        };
        var stops = new List<RouteStop>
        {
            new()
            {
                RouteStopId = 1,
                RouteId = 4,
                StopName = "Stop 1",
                StopOrder = 1,
                Latitude = 38.15m,
                Longitude = -102.72m,
                StopDuration = 1
            },
            new()
            {
                RouteStopId = 2,
                RouteId = 4,
                StopName = "Stop 2",
                StopOrder = 2,
                Latitude = 38.09m,
                Longitude = -102.62m,
                StopDuration = 1
            }
        };

        var routes = Mock.Of<IRouteService>();
        var mock = Mock.Get(routes);
        mock.Setup(r => r.GetUnassignedStudentsAsync(It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(new List<Student>()));
        mock.Setup(r => r.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(new[] { route }));
        mock.Setup(r => r.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        mock.Setup(r => r.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));
        mock.Setup(r => r.GetRouteStopsAsync(4))
            .ReturnsAsync(Result.SuccessResult<IEnumerable<RouteStop>>(stops));
        mock.Setup(r => r.GetStudentsForRouteAsync(4, It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(new List<Student>()));
        mock.Setup(r => r.GetRiderExceptionStudentIdsAsync(4, It.IsAny<DateTime>()))
            .ReturnsAsync(Result.SuccessResult<IReadOnlyList<int>>(Array.Empty<int>()));
        mock.Setup(r => r.UpdateRouteStopsTimingAsync(4, It.IsAny<IEnumerable<RouteStop>>()))
            .ReturnsAsync(Result.SuccessResult(true));

        var vm = new RouteAssignmentViewModel(routes);
        await WaitUntilAsync(() => !vm.IsLoading && vm.SelectedRoute != null && vm.RouteStops.Count == 2);

        vm.TimeRouteCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsLoading && vm.RouteStops[1].ScheduledArrival != default);

        Assert.That(vm.RouteStops[0].ScheduledArrival, Is.EqualTo(new TimeSpan(7, 30, 0)));
        Assert.That(vm.RouteStops[1].ScheduledArrival, Is.EqualTo(new TimeSpan(8, 31, 0)));
        Assert.That(vm.StatusMessage, Does.Contain("60 min travel"));
        mock.Verify(r => r.UpdateRouteStopsTimingAsync(4, It.IsAny<IEnumerable<RouteStop>>()), Times.Once);
    }

    [Test]
    public void AssignmentVm_DoesNotAutoRetimeAfterStopEdits()
    {
        var assignment = XamlViewFile.ReadFolder("ViewModels/Route");
        Assert.That(assignment, Does.Contain("PublishedStopClockPlanner.Apply"));
        Assert.That(assignment, Does.Contain("MarkPublishedClocksStale"));
        Assert.That(assignment, Does.Contain("Refresh Route Data started"));
        Assert.That(assignment, Does.Contain("Data refreshed successfully"));
        Assert.That(assignment, Does.Not.Contain("Auto-retiming route stops"));
        Assert.That(assignment, Does.Not.Contain("_retimeDebounceTimer"));
    }

    [Test]
    public async Task Ribbon_WithoutRoute_DisablesScheduleAndDrivePath()
    {
        var routes = Mock.Of<IRouteService>();
        var mock = Mock.Get(routes);
        mock.Setup(r => r.GetUnassignedStudentsAsync(It.IsAny<RouteTimeSlot>()))
            .ReturnsAsync(Result.SuccessResult(new List<Student>()));
        mock.Setup(r => r.GetAllRoutesAsync())
            .ReturnsAsync(Result.SuccessResult<IEnumerable<Route>>(Array.Empty<Route>()));
        mock.Setup(r => r.GetAvailableBusesAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Bus>()));
        mock.Setup(r => r.GetAvailableDriversAsync())
            .ReturnsAsync(Result.SuccessResult(new List<Driver>()));

        var vm = new RouteAssignmentViewModel(routes);
        await WaitUntilAsync(() => !vm.IsLoading);

        vm.SelectedRoute = null;
        Assert.That(vm.ViewScheduleCommand.CanExecute(null), Is.False);
        Assert.That(vm.RefreshDrivePathCommand.CanExecute(null), Is.False);
        Assert.That(vm.TimeRouteCommand.CanExecute(null), Is.False);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 8000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!predicate())
        {
            if (Environment.TickCount64 > deadline)
            {
                Assert.Fail("Timed out waiting for RouteAssignmentViewModel load.");
            }

            await Task.Delay(50);
        }
    }
}
