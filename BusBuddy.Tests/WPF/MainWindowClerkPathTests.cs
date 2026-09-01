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
        Assert.That(xaml, Does.Not.Contain("MONDAY DEMO"));
        Assert.That(xaml, Does.Not.Contain("bb-route-demo"));
        Assert.That(xaml, Does.Not.Contain("MVP"));
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
        Assert.That(xaml, Does.Contain("Click=\"SaveSchoolButton_Click\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CancelCommand}\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"School latitude\""));
        Assert.That(xaml, Does.Contain("SchoolPickMap"));
        Assert.That(xaml, Does.Contain("SchoolPickMap_MouseLeftButtonUp"));
        Assert.That(xaml, Does.Contain("DoubleTextBox"));
        Assert.That(xaml, Does.Contain("AutoCompleteMode=\"None\""));
        Assert.That(xaml, Does.Not.Contain("<syncfusion:SfMaskedEdit"));
        Assert.That(xaml, Does.Not.Contain("MaskType=\"Text\""));
        Assert.That(xaml, Does.Contain("Height=\"40\""));
        var codeBehind = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml.cs");
        Assert.That(codeBehind, Does.Contain("GetLatLonFromPoint"));
        Assert.That(codeBehind, Does.Contain("ApplyMapClick"));
        Assert.That(codeBehind, Does.Contain("PushFieldsToViewModel"));
        Assert.That(codeBehind, Does.Contain("NumPad"));
    }

    [Test]
    public void DriverFormXaml_UsesValueMasksAndStatusMessage()
    {
        var xaml = XamlViewFile.Read("Views/Driver/DriverForm.xaml");
        Assert.That(xaml, Does.Contain("SfTextBoxExt Text=\"{Binding Driver.FirstName"));
        Assert.That(xaml, Does.Contain("SfTextBoxExt Text=\"{Binding Driver.DriverEmail"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding StatusOptions}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding LicenseClassOptions}\""));
        Assert.That(xaml, Does.Contain("Value=\"{Binding Driver.DriverPhone"));
        Assert.That(xaml, Does.Not.Contain("Mask=\"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+"));
        Assert.That(xaml, Does.Not.Contain("SelectedValuePath=\"Content\""));
        Assert.That(xaml, Does.Contain("Value=\"{Binding Driver.Zip"));
        Assert.That(xaml, Does.Contain("HasStatusMessage"));
        Assert.That(xaml, Does.Contain("Text=\"{Binding StatusMessage}\""));
        Assert.That(xaml, Does.Not.Contain("HasErrors"));
        Assert.That(xaml, Does.Not.Contain("MaskType=\"Text\""));
        var vm = XamlViewFile.Read("ViewModels/Driver/DriverFormViewModel.cs");
        Assert.That(vm, Does.Contain("OnDriverModelPropertyChanged"));
        Assert.That(vm, Does.Contain("HasUsableDriverName"));
        Assert.That(vm, Does.Contain("HireDate = driver.HireDate"));
        Assert.That(vm, Does.Contain("StatusOptions"));
        Assert.That(vm, Does.Contain("LicenseClassOptions"));
    }

    [Test]
    public void RouteEditDialog_SaveCommandIsImplemented()
    {
        var vm = XamlViewFile.Read("ViewModels/Route/RouteEditDialogViewModel.cs");
        Assert.That(vm, Does.Contain("RequestClose"));
        Assert.That(vm, Does.Contain("Start location is required"));
        Assert.That(vm, Does.Not.Contain("Validation stub"));
        var xaml = XamlViewFile.Read("Views/Route/RouteEditDialog.xaml");
        Assert.That(xaml, Does.Contain("Height=\"40\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CancelCommand}\""));
    }

    [Test]
    public void TransferAndBusForms_UseTextBoxExtAndIntegerInputs()
    {
        var transfer = XamlViewFile.Read("Views/Student/StudentSchoolTransferForm.xaml");
        Assert.That(transfer, Does.Contain("SfTextBoxExt Text=\"{Binding PickupAddress"));
        Assert.That(transfer, Does.Contain("Value=\"{Binding PickupTimeText"));
        Assert.That(transfer, Does.Not.Contain("MaskType=\"Text\""));

        var transferVm = XamlViewFile.Read("ViewModels/Student/StudentSchoolTransferViewModel.cs");
        Assert.That(transferVm, Does.Contain("Replace(\"_\""),
            "Time parse must strip SfMaskedEdit prompt chars");

        var bus = XamlViewFile.Read("Views/Bus/BusForm.xaml");
        Assert.That(bus, Does.Contain("IntegerTextBox"));
        Assert.That(bus, Does.Contain("Value=\"{Binding Year"));
        Assert.That(bus, Does.Contain("SfTextBoxExt Height=\"40\""));

        var fuel = XamlViewFile.Read("Views/Fuel/FuelDialog.xaml");
        Assert.That(fuel, Does.Contain("Mode=TwoWay, UpdateSourceTrigger=PropertyChanged"));
        Assert.That(fuel, Does.Contain("SfTextBoxExt"));
    }
}
