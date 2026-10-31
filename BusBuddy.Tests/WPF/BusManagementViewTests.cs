using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// XAML/command wiring contract for Bus Management (Syncfusion grid + detail Save).
/// ViewModel sample-data guards stay in <see cref="BusBuddy.Tests.Core.MainWindowClerkPathTests"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class BusManagementViewTests
{
    [Test]
    public void BusManagementViewXaml_WiresFleetCommandsAndSyncfusionGrid()
    {
        var xaml = XamlViewFile.Read("Views/Bus/BusManagementView.xaml");

        Assert.That(xaml, Does.Contain("Command=\"{Binding AddBusCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteBusCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding SaveBusCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CancelEditCommand}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding FilteredBuses}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedBus, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("AllowEditing=\"False\""));
        Assert.That(xaml, Does.Contain("Text=\"{Binding SelectedBus.BusNumber, Mode=TwoWay"));
        Assert.That(xaml, Does.Contain("Value=\"{Binding SelectedBus.SeatingCapacity, Mode=TwoWay"));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedBus.Status, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedBus.FleetType, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Visibility=\"{Binding IsBusy, Converter={StaticResource BooleanToVisibilityConverter}}\""));
        Assert.That(xaml, Does.Contain("syncfusion:SfDataGrid"));
        Assert.That(xaml, Does.Contain("syncfusion:ButtonAdv"));
        Assert.That(xaml, Does.Contain("syncfusion:SfBusyIndicator"));
        Assert.That(xaml, Does.Not.Contain("<DataGrid"));
        Assert.That(xaml, Does.Not.Contain("System.Windows.Controls.DataGrid"));
        Assert.That(xaml, Does.Not.Contain("BusEditDialog"));
    }

    [Test]
    public void BusManagementView_ResolvesViewModelAndAppliesStartup()
    {
        var code = XamlViewFile.Read("Views/Bus/BusManagementView.xaml.cs");

        Assert.That(code, Does.Contain("GetRequiredService<IBusService>()"));
        Assert.That(code, Does.Contain("new BusManagementViewModel(busService)"));
        Assert.That(code, Does.Contain("ApplyStartupAsync(_startup)"));
        Assert.That(code, Does.Contain("BusManagementStartup"));
        Assert.That(code, Does.Contain("ButtonAccessibilityAudit.Run"));
    }
}
