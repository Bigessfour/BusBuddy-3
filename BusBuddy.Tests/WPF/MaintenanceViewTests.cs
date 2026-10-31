using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MaintenanceViewTests
{
    [Test]
    public void MaintenanceViewXaml_WiresCrudCommands()
    {
        var xaml = XamlViewFile.Read("Views/Maintenance/MaintenanceView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding SaveCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
    }

    [Test]
    public void MaintenanceViewXaml_UsesBusComboAndClosedLists()
    {
        var xaml = XamlViewFile.Read("Views/Maintenance/MaintenanceView.xaml");
        Assert.That(xaml, Does.Contain("GridComboBoxColumn"));
        Assert.That(xaml, Does.Contain("MappingName=\"VehicleId\""));
        Assert.That(xaml, Does.Contain("x:Name=\"MaintenanceRoot\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding DataContext.Vehicles, ElementName=MaintenanceRoot}\""));
        Assert.That(xaml, Does.Contain("SelectedValuePath=\"BusId\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding DataContext.StatusOptions, ElementName=MaintenanceRoot}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding DataContext.PriorityOptions, ElementName=MaintenanceRoot}\""));
        Assert.That(xaml, Does.Not.Contain("GridNumericColumn MappingName=\"VehicleId\""));
        Assert.That(xaml, Does.Not.Contain("RelativeSource AncestorType=UserControl"));
    }
}
