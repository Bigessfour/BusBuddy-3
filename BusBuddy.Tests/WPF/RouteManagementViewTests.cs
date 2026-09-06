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
        Assert.That(xaml, Does.Contain("SelectedValuePath=\"BusId\""));
        Assert.That(xaml, Does.Contain("SelectedValue=\"{Binding SelectedBusId, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedTimeSlot, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding AssignVehicleCommand}\""));
        Assert.That(xaml, Does.Contain("AllowEditing=\"True\""));
        Assert.That(xaml, Does.Contain("SelectedValuePath=\"BusNumber\""));
        Assert.That(xaml, Does.Contain("MappingName=\"BusNumber\""));
        Assert.That(xaml, Does.Contain("MappingName=\"School\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding AvailableSchools}\""));
        Assert.That(xaml, Does.Contain("SelectedValuePath=\"Name\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding OpenRouteAssignmentCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshDrivePathCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateScheduleCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateTransferRoutesCommand}\""));
        Assert.That(xaml, Does.Contain("BusBuddy.Brush.SafetyOrange"));
        Assert.That(xaml, Does.Contain("WrapPanel"));
        Assert.That(xaml, Does.Contain("ButtonAdvTextOnly.xaml"));
        Assert.That(xaml, Does.Contain("CurrentCellEndEdit=\"RoutesDataGrid_CurrentCellEndEdit\""));
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
        Assert.That(source, Does.Contain("RoutesDataGrid_CurrentCellEndEdit"));
        Assert.That(source, Does.Not.Contain("GetProperty(\"RefreshCommand\")"));
    }
}
