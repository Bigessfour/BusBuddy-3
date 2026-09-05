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
        Assert.That(source, Does.Not.Contain("Vehicles.Max(v => v.BusId)"));
        Assert.That(source, Does.Not.Contain("catch { /* ignore service failure */ }"));
        Assert.That(source, Does.Contain("await _busService.AddBusAsync"));
        Assert.That(source, Does.Contain("await _busService.DeleteBusAsync"));
        Assert.That(source, Does.Contain("Log.ForContext<VehicleManagementViewModel>"));
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
    public void RouteEditDialog_AndRouteForm_AreRemoved()
    {
        Assert.That(XamlViewFile.Exists("Views/Route/RouteEditDialog.xaml"), Is.False);
        Assert.That(XamlViewFile.Exists("Views/Route/RouteEditDialog.xaml.cs"), Is.False);
        Assert.That(XamlViewFile.Exists("ViewModels/Route/RouteEditDialogViewModel.cs"), Is.False);
        Assert.That(XamlViewFile.Exists("Views/Route/RouteForm.xaml"), Is.False);

        Assert.That(XamlViewFile.Exists("Views/Route/RouteStopsEditor.xaml"), Is.True);
        var assignment = XamlViewFile.Read("Views/Route/RouteAssignmentView.xaml");
        Assert.That(assignment, Does.Contain("RouteStopsEditor"));
    }

    [Test]
    public void TransferForm_UsesTextBoxExtAndMaskedTime()
    {
        var transfer = XamlViewFile.Read("Views/Student/StudentSchoolTransferForm.xaml");
        Assert.That(transfer, Does.Contain("SfTextBoxExt Text=\"{Binding PickupAddress"));
        Assert.That(transfer, Does.Contain("Value=\"{Binding PickupTimeText"));
        Assert.That(transfer, Does.Not.Contain("MaskType=\"Text\""));

        var transferVm = XamlViewFile.Read("ViewModels/Student/StudentSchoolTransferViewModel.cs");
        Assert.That(transferVm, Does.Contain("Replace(\"_\""),
            "Time parse must strip SfMaskedEdit prompt chars");

        var fuel = XamlViewFile.Read("Views/Fuel/FuelDialog.xaml");
        Assert.That(fuel, Does.Contain("Mode=TwoWay, UpdateSourceTrigger=PropertyChanged"));
        Assert.That(fuel, Does.Contain("SfTextBoxExt"));
    }

    [Test]
    public void BusForm_IsRemoved_FleetUsesVehicleFleetLauncher()
    {
        Assert.That(XamlViewFile.Exists("Views/Bus/BusForm.xaml"), Is.False);
        Assert.That(XamlViewFile.Exists("ViewModels/Bus/BusFormViewModel.cs"), Is.False);

        var main = XamlViewFile.Read("Views/Main/MainWindow.xaml.cs");
        Assert.That(main, Does.Contain("VehicleFleetLauncher.ShowDialog"));
        Assert.That(main, Does.Contain("VehicleManagementStartup.AddVehicle"));
        Assert.That(main, Does.Not.Contain("new BusForm"));
    }

    [Test]
    public void SettingsView_BindsAllViewModelPreferences()
    {
        var xaml = XamlViewFile.Read("Views/Settings/SettingsView.xaml");
        Assert.That(xaml, Does.Contain("SelectedTheme"));
        Assert.That(xaml, Does.Contain("EnableActivityLogging"));
        Assert.That(xaml, Does.Contain("ShowDashboardOnStartup"));
        Assert.That(xaml, Does.Contain("StatusMessage"));
        Assert.That(xaml, Does.Contain("IsBusy"));
        Assert.That(xaml, Does.Contain("IsEditable=\"False\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Enable activity logging\""));
        Assert.That(xaml, Does.Contain("AutomationProperties.Name=\"Show dashboard on startup\""));
        Assert.That(xaml, Does.Contain("SettingsPrimaryButton"));

        var vm = XamlViewFile.Read("ViewModels/Settings/SettingsViewModel.cs");
        Assert.That(vm, Does.Contain("UserSettingsKeys"));
        Assert.That(vm, Does.Contain("Log.ForContext<SettingsViewModel>"));

        var main = XamlViewFile.Read("Views/Main/MainWindow.xaml.cs");
        Assert.That(main, Does.Contain("TryShowDashboardOnStartup"));
    }

    [Test]
    public void MainWindowNavigation_OpensMapAndRouteManagementWindows()
    {
        var main = XamlViewFile.Read("Views/Main/MainWindow.xaml.cs");
        Assert.That(main, Does.Contain("MapViewLauncher.Show"));
        Assert.That(main, Does.Not.Contain("_mapWindow"));
        Assert.That(main, Does.Contain("ShowViewInWindow(new RouteManagementView()"));
        Assert.That(main, Does.Contain("ActivateRouteAssignmentsPane()"));
        Assert.That(main, Does.Not.Contain("RouteManagementButton_Click(sender, e)"));

        var assignment = XamlViewFile.Read("Utilities/RouteAssignmentLauncher.cs");
        Assert.That(assignment, Does.Contain("DialogOwner.Resolve"));
        Assert.That(assignment, Does.Contain("ShowDialog"));
    }
}
