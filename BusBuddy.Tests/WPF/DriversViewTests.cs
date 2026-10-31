using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Drivers roster XAML contract: school-bus endorsement + home-route warning columns and toolbar commands.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DriversViewTests
{
    [Test]
    public void DriversViewXaml_BindsEndorsementWarningAndGridCommands()
    {
        var xaml = XamlViewFile.Read("Views/Driver/DriversView.xaml");

        Assert.That(xaml, Does.Contain("Command=\"{Binding AddDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding EditDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding GenerateReportsCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ViewLicenseCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding TrainingRecordsCommand}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding FilteredDrivers}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedDriver, Mode=TwoWay"));
        Assert.That(xaml, Does.Contain("MappingName=\"SchoolBusEndorsement\""));
        Assert.That(xaml, Does.Contain("HeaderText=\"School Bus S\""));
        Assert.That(xaml, Does.Contain("MappingName=\"HomeRouteEligibilityWarning\""));
        Assert.That(xaml, Does.Contain("HeaderText=\"Home Route Warning\""));
        Assert.That(xaml, Does.Contain("syncfusion:SfDataGrid"));
        Assert.That(xaml, Does.Contain("syncfusion:GridCheckBoxColumn"));
        Assert.That(xaml, Does.Contain("syncfusion:ButtonAdv"));
        Assert.That(xaml, Does.Not.Contain("<DataGrid"));
        Assert.That(xaml, Does.Not.Contain("System.Windows.Controls.DataGrid"));
    }

    [Test]
    public void DriversView_PushesGridSelectionIntoViewModel()
    {
        var code = XamlViewFile.Read("Views/Driver/DriversView.xaml.cs");

        Assert.That(code, Does.Contain("DriversDataGrid_SelectionChanged"));
        Assert.That(code, Does.Contain("vm.SelectedDriver"));
        Assert.That(code, Does.Contain("LoadDriversAsync()"));
        Assert.That(code, Does.Contain("GetService<DriversViewModel>()"));
    }
}
