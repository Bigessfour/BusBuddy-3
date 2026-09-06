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
        Assert.That(xaml, Does.Contain("SelectedRouteBusDisplay"));
        Assert.That(xaml, Does.Contain("SelectedRouteDriverDisplay"));
        Assert.That(xaml, Does.Contain("Visibility=\"{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}\""));
    }
}
