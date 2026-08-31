using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MainWindowClerkPathTests
{
    [Test]
    public void MainWindowXaml_WiresClerkPathAndSelectedItems()
    {
        var xaml = XamlViewFile.Read("Views/Main/MainWindow.xaml");
        Assert.That(xaml, Does.Contain("Click=\"FuelManagementButton_Click\""));
        Assert.That(xaml, Does.Contain("Click=\"Maintenance_Click\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedStudent, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedBus, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Contain("Text=\"{Binding StatusMessage"));
        Assert.That(xaml, Does.Not.Contain("Monday Ready Demo"));
        Assert.That(xaml, Does.Not.Contain("bb-route-demo"));
    }

    [Test]
    public void MainWindowViewModelSource_DoesNotInventSampleRows()
    {
        var source = XamlViewFile.Read("ViewModels/MainWindowViewModel.cs");
        Assert.That(source, Does.Not.Contain("LoadSampleData"));
        Assert.That(source, Does.Not.Contain("John Doe"));
        Assert.That(source, Does.Not.Contain("Jane Smith"));
        Assert.That(source, Does.Contain("IBusService"));
    }

    [Test]
    public void VehicleManagementViewModelSource_DoesNotInventSampleBuses()
    {
        var source = XamlViewFile.Read("ViewModels/Vehicle/VehicleManagementViewModel.cs");
        Assert.That(source, Does.Not.Contain("LoadSampleData"));
        Assert.That(source, Does.Not.Contain("BUS001"));
    }

    [Test]
    public void SchoolDestinationFormXaml_WiresSaveCommand()
    {
        var xaml = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding SaveCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CancelCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"School latitude\""));
    }
}
