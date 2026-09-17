using System.Text;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Route;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteScheduleViewModelTests
{
    [Test]
    public void ViewSchedule_RequiresSelectedRoute()
    {
        var opened = RouteScheduleViewModel.TryCreate(
            route: null,
            Array.Empty<RouteStop>(),
            Array.Empty<Student>(),
            bus: null,
            driver: null,
            RouteTimeSlot.AM,
            out var vm);

        Assert.That(opened, Is.False);
        Assert.That(vm, Is.Null);
    }

    [Test]
    public void Sheet_OrdersByScheduledArrivalThenStopOrder()
    {
        var route = new Route
        {
            RouteName = "Draft-Hop1_Proof_School_20260909224028-R0C0-1",
            School = "Wiley School",
            Session = RouteSession.AM,
            EstimatedDuration = 361
        };
        var stops = new[]
        {
            Stop(1, "Late", new TimeSpan(7, 59, 0), new TimeSpan(7, 59, 0)),
            Stop(50, "First", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0)),
            Stop(51, "Middle", new TimeSpan(7, 30, 0), new TimeSpan(7, 31, 0))
        };

        Assert.That(
            RouteScheduleViewModel.TryCreate(route, stops, Array.Empty<Student>(), null, null, RouteTimeSlot.AM, out var vm),
            Is.True);
        Assert.That(vm, Is.Not.Null);
        Assert.That(vm!.Sheet.Stops.Select(s => s.Name).ToArray(), Is.EqualTo(new[] { "First", "Middle", "Late" }));
        Assert.That(vm.Sheet.DisplayName, Does.Contain("Wiley School"));
        Assert.That(vm.Sheet.DisplayName, Does.Not.Contain("Draft-Hop1"));
    }

    [Test]
    public void Sheet_EmptyClocksAreEmDash_NotMidnight()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School", EstimatedDuration = 45 };
        var stop = Stop(1, "Barn", default, default);
        stop.EstimatedArrivalTime = new DateTime(2026, 9, 17, 13, 1, 0, DateTimeKind.Utc);
        stop.EstimatedDepartureTime = new DateTime(2026, 9, 17, 13, 1, 0, DateTimeKind.Utc);

        Assert.That(
            RouteScheduleViewModel.TryCreate(route, new[] { stop }, Array.Empty<Student>(), null, null, RouteTimeSlot.AM, out var vm),
            Is.True);
        Assert.That(vm!.Sheet.Stops[0].Arrival, Is.EqualTo(RouteScheduleViewModel.EmDash));
        Assert.That(vm.Sheet.Stops[0].Departure, Is.EqualTo(RouteScheduleViewModel.EmDash));
        Assert.That(vm.Sheet.DepartureText, Is.EqualTo(RouteScheduleViewModel.EmDash));
        Assert.That(vm.Sheet.ArrivalText, Is.EqualTo(RouteScheduleViewModel.EmDash));
        Assert.That(vm.Sheet.DepartureText, Does.Not.Contain("00:00"));
        Assert.That(vm.Sheet.DepartureText, Does.Not.Contain("13:01"));
    }

    [Test]
    public void Sheet_DoesNotUseEstimatedDurationForHeader()
    {
        var route = new Route
        {
            RouteName = "Town AM",
            School = "Wiley School",
            EstimatedDuration = 361,
            Distance = 0
        };

        Assert.That(
            RouteScheduleViewModel.TryCreate(
                route,
                new[] { Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 2, 0)) },
                Array.Empty<Student>(),
                null,
                null,
                RouteTimeSlot.AM,
                out var vm),
            Is.True);
        Assert.That(vm!.Sheet.DepartureText, Is.EqualTo("07:00"));
        Assert.That(vm.Sheet.ArrivalText, Is.EqualTo("07:00"));
        Assert.That(vm.Sheet.TotalMilesText, Is.EqualTo(RouteScheduleViewModel.EmDash));
        Assert.That(vm.FirstLastClockText, Is.EqualTo("07:00 → 07:00"));
        Assert.That(vm.Sheet.Title, Does.Not.Contain("361"));
    }

    [Test]
    public void EmptyStops_StillCreatesWindowModel_WithHint()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School" };
        Assert.That(
            RouteScheduleViewModel.TryCreate(route, Array.Empty<RouteStop>(), Array.Empty<Student>(), null, null, RouteTimeSlot.AM, out var vm),
            Is.True);
        Assert.That(vm!.HasEmptyStops, Is.True);
        Assert.That(vm.StatusMessage, Is.EqualTo(RouteScheduleViewModel.EmptyStopsHint));
        Assert.That(vm.ReTimeCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Print_UsesPdfGridRenderer()
    {
        var route = new Route
        {
            RouteName = "Town AM",
            School = "Wiley School",
            Session = RouteSession.AM,
            EstimatedDuration = 361
        };
        var stops = new[]
        {
            Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0)),
            Stop(2, "School", new TimeSpan(7, 20, 0), new TimeSpan(7, 20, 0))
        };

        var bytes = RouteSummaryPdfRenderer.Render(
            route,
            stops,
            Array.Empty<Student>(),
            new Bus { BusNumber = "5" },
            new Driver { DriverName = "Robert Truitt" },
            RouteTimeSlot.AM);

        Assert.That(bytes.Length, Is.GreaterThan(200));
        Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("%PDF"));

        var printed = false;
        Assert.That(
            RouteScheduleViewModel.TryCreate(
                route,
                stops,
                Array.Empty<Student>(),
                new Bus { BusNumber = "5" },
                new Driver { DriverName = "Robert Truitt" },
                RouteTimeSlot.AM,
                out var vm,
                print: () =>
                {
                    printed = true;
                    var again = RouteSummaryPdfRenderer.Render(
                        route, stops, Array.Empty<Student>(), new Bus { BusNumber = "5" }, null, RouteTimeSlot.AM);
                    Assert.That(Encoding.ASCII.GetString(again, 0, 4), Is.EqualTo("%PDF"));
                }),
            Is.True);
        vm!.PrintCommand.Execute(null);
        Assert.That(printed, Is.True);
    }

    [Test]
    public void ReTime_ConfirmsWhenClocksExist_AndDoesNotRewriteOnCancel()
    {
        var route = new Route { RouteName = "Town AM" };
        var stops = new[] { Stop(1, "Barn", new TimeSpan(7, 0, 0), new TimeSpan(7, 1, 0)) };
        var retimeCalls = 0;
        Assert.That(
            RouteScheduleViewModel.TryCreate(
                route,
                stops,
                Array.Empty<Student>(),
                null,
                null,
                RouteTimeSlot.AM,
                out var vm,
                reTimeAsync: () =>
                {
                    retimeCalls++;
                    return Task.FromResult<RouteSummarySheet?>(null);
                },
                confirmOverwriteClocks: () => false),
            Is.True);

        vm!.ReTimeCommand.Execute(null);
        Assert.That(retimeCalls, Is.EqualTo(0));
        Assert.That(vm.Sheet.DepartureText, Is.EqualTo("07:00"));
        Assert.That(vm.StatusMessage, Is.EqualTo("Published times left unchanged"));
    }

    [Test]
    public void Roster_ShowsNotRidingTodayBadge_WithoutDroppingAssignment()
    {
        var route = new Route { RouteName = "Town AM", School = "Wiley School", Session = RouteSession.AM };
        var student = new Student { StudentId = 9, StudentName = "Ada Clark", Grade = "3", HomeAddress = "100 Main" };
        var stop = Stop(1, "100 Main", new TimeSpan(7, 10, 0), new TimeSpan(7, 11, 0));
        stop.StopAddress = "100 Main St";

        Assert.That(
            RouteScheduleViewModel.TryCreate(
                route,
                new[] { stop },
                new[] { student },
                null,
                null,
                RouteTimeSlot.AM,
                out var vm,
                notRidingStudentIds: new HashSet<int> { 9 }),
            Is.True);
        Assert.That(vm!.Sheet.RosterCount, Is.EqualTo(1));
        Assert.That(vm.Sheet.Students[0].Status, Is.EqualTo("Not riding today"));
        Assert.That(vm.Sheet.Stops[0].Arrival, Is.EqualTo("07:10"));
    }

    [Test]
    public void RouteScheduleWindowXaml_UsesPublishedGridAndActions()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteScheduleWindow.xaml");
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route Schedule\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route schedule stops\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Route schedule roster\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ReTimeCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PrintCommand}\""));
        Assert.That(xaml, Does.Not.Contain("SfScheduler"));
        Assert.That(xaml, Does.Not.Contain("DriverSchedule"));
    }

    private static RouteStop Stop(int order, string name, TimeSpan arr, TimeSpan dep) =>
        new()
        {
            StopOrder = order,
            StopName = name,
            StopAddress = name.Contains("Main", StringComparison.Ordinal) ? "100 Main St" : string.Empty,
            ScheduledArrival = arr,
            ScheduledDeparture = dep,
            Status = "Active"
        };
}
