using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteAssignmentViewTests
{
    [Test]
    public void RouteAssignmentViewXaml_WiresGenerateCommands()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateTransferRoutesCommand}\""));
        Assert.That(xaml, Does.Not.Contain("MVP"));
    }

    [Test]
    public void RouteAssignmentViewXaml_WiresAssignAndRefreshCommands()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignVehicleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDataCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding MarkNotRidingTodayCommand}\""));
        Assert.That(xaml, Does.Contain("SelectedRouteBusDisplay"));
        Assert.That(xaml, Does.Contain("SelectedRouteDriverDisplay"));
        Assert.That(xaml, Does.Contain("Visibility=\"{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}\""));
    }

    [Test]
    public void RouteAssignmentViewXaml_WiresPdfGridSheetCommands()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding ViewScheduleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDrivePathCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PrintRouteSheetCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ExportRouteSheetCommand}\""));
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding ViewRouteTimetableCommand}\""));
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding PrintMapCommand}\""));
        Assert.That(xaml, Does.Not.Contain("Command=\"{Binding GenerateReportCommand}\""));
    }
}
