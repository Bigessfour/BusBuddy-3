using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public sealed class RouteDatedSubstituteSurfaceTests
{
    [Test]
    public void ScheduleWindow_ExposesSubstituteCommand()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteScheduleWindow.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding SubstituteCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Substitute for this date\""));

        var vm = XamlViewFile.Read("ViewModels/Route/RouteScheduleViewModel.cs");
        Assert.That(vm, Does.Contain("SubstituteCommand"));
        Assert.That(vm, Does.Contain("substituteAsync"));
    }

    [Test]
    public void DatedSubstituteDialog_BindsBusAndDriver()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteDatedSubstituteDialog.xaml");
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Buses}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Drivers}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding SaveCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Dated substitute dialog\""));

        var assignment = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.Schedule.cs");
        Assert.That(assignment, Does.Contain("SaveDatedSubstituteAsync"));
        Assert.That(assignment, Does.Contain("RouteDailyPairing.BuildSessionRow"));
        Assert.That(assignment, Does.Contain("GetAssignmentWarningsAsync"));
        Assert.That(assignment, Does.Contain("SaveRouteSessionScheduleAsync"));
        Assert.That(assignment, Does.Contain("Trip overlap check is unavailable"));
        Assert.That(assignment, Does.Not.Contain("AssignDriverToRouteAsync"));
        Assert.That(assignment, Does.Not.Contain("AssignVehicleToRouteAsync"));

        var reports = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.Reports.cs");
        Assert.That(reports, Does.Contain("RefreshDatedSchedulesAsync"));
        Assert.That(reports, Does.Contain("SaveRouteSheetAsync"));
        Assert.That(reports, Does.Contain("await RefreshDatedSchedulesAsync"));

        var export = XamlViewFile.Read("ViewModels/Route/RouteManagementExportHelper.cs");
        Assert.That(export, Does.Contain("matchedSchedule?.Bus"));
        Assert.That(export, Does.Contain("matchedSchedule?.Driver"));
        Assert.That(export, Does.Contain("SaveRouteSessionScheduleAsync"));
    }
}
