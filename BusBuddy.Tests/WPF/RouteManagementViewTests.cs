using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteManagementViewTests
{
    [Test]
    public void RouteManagementViewXaml_WiresVehicleAssignmentPanel()
    {
        var xaml = XamlViewFile.Read("Views/Route/RouteManagementView.xaml");
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding AvailableBuses}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedBus, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedTimeSlot, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignVehicleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding OpenRouteAssignmentCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDrivePathCommand}\""));
        Assert.That(xaml, Does.Contain("Visibility=\"{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}\""));
        Assert.That(xaml, Does.Not.Contain("ViewMapCommand"));
        Assert.That(xaml, Does.Not.Contain("AssignStudentsButton"));
    }

    [Test]
    public void RouteManagementView_ResolvesViewModelFromDi()
    {
        var source = XamlViewFile.Read("Views/Route/RouteManagementView.xaml.cs");
        Assert.That(source, Does.Contain("GetRequiredService<RouteManagementViewModel>()"));
        Assert.That(source, Does.Contain("InitializeAsync"));
        Assert.That(source, Does.Not.Contain("GetProperty(\"RefreshCommand\")"));
    }
}
