using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Tests.WPF;
using BusBuddy.WPF.ViewModels.Activity;
using CommunityToolkit.Mvvm.Input;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class ActivityOperationalSurfaceTests
{
    [Test]
    public void UiProofLog_UsesGreppableTemplateAndDiagnosticsFile()
    {
        var proof = XamlViewFile.Read("Logging/UiProofLog.cs");
        Assert.That(proof, Does.Contain("UI proof Click={Click} Surface={Surface} Outcome={Outcome} Detail={Detail}"));
        Assert.That(proof, Does.Contain("UiDiagnosticsLog.Write"));
        var diagnostics = XamlViewFile.Read("Logging/UiDiagnosticsLog.cs");
        Assert.That(diagnostics, Does.Contain("ui-diagnostics-.log"));
    }

    [Test]
    public void ActivityTimelineXaml_WiresTripBoardClickAndRefresh()
    {
        var xaml = XamlViewFile.Read("Views/Activity/ActivityTimelineView.xaml");
        Assert.That(xaml, Does.Contain("Click=\"TripBoardButton_Click\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding TimelineEvents}\""));
        Assert.That(xaml, Does.Not.Contain("ShortDateTime"));
        Assert.That(xaml, Does.Contain("Pattern=\"CustomPattern\""));
        Assert.That(xaml, Does.Contain("CustomPattern=\"MM/dd/yyyy h:mm tt\""));
    }

    [Test]
    public void ActivityManagementXaml_WiresTripBoardGridToServiceCommands()
    {
        var xaml = XamlViewFile.Read("Views/Activity/ActivityManagementView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding ImportCsvCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding NewTripCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding EditTripCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding ConfirmTripCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding CalculateDistanceCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding PrintTicketCommand}\""));
        Assert.That(xaml, Does.Contain("SfScheduler"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Trips}\""));
        Assert.That(xaml, Does.Contain("MappingName=\"ExternalTicketNo\""));
        Assert.That(xaml, Does.Contain("MappingName=\"DisplayTripType\""));
        Assert.That(xaml, Does.Contain("MappingName=\"DriverHours\""));
        Assert.That(xaml, Does.Contain("SelectedItem=\"{Binding SelectedTrip, Mode=TwoWay}\""));
        Assert.That(xaml, Does.Not.Contain("IsTrip"));
        Assert.That(xaml, Does.Not.Contain("new ActivitySchedule"));

        var dialog = XamlViewFile.Read("Views/Activity/TripEventEditDialog.xaml");
        Assert.That(dialog, Does.Contain("IsEditable=\"True\""));
        Assert.That(dialog, Does.Contain("Command=\"{Binding SaveCommand}\""));
        Assert.That(dialog, Does.Contain("Command=\"{Binding RememberPurposeCommand}\""));
        Assert.That(dialog, Does.Contain("Command=\"{Binding RememberSportCommand}\""));
        Assert.That(dialog, Does.Contain("Text=\"{Binding Purpose, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\""));
        Assert.That(dialog, Does.Contain("Text=\"{Binding SportKind, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\""));
        Assert.That(dialog, Does.Contain("Text=\"{Binding StatusDisplay, Mode=OneWay}\""));
        Assert.That(dialog, Does.Contain("Text=\"{Binding PathMilesDisplay, Mode=OneWay}\""));
        Assert.That(dialog, Does.Contain("Text=\"{Binding DriverHoursDisplay, Mode=OneWay}\""));
        Assert.That(dialog, Does.Not.Contain("<Run Text=\"{Binding StatusDisplay"));
        Assert.That(dialog, Does.Not.Contain("<Run Text=\"{Binding PathMilesDisplay"));
        Assert.That(dialog, Does.Not.Contain("<Run Text=\"{Binding DriverHoursDisplay"));
        var dialogVm = XamlViewFile.Read("ViewModels/Activity/TripEventEditDialogViewModel.cs");
        Assert.That(dialogVm, Does.Contain("ITripReasonCatalog"));
        Assert.That(dialogVm, Does.Contain("never sets RouteId"));
    }

    [Test]
    public void DriverManagementViewXaml_BindsAddEditDeleteToDriversViewModel()
    {
        var xaml = XamlViewFile.Read("Views/Driver/DriverManagementView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding AddDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding EditDriverCommand}\""));
        Assert.That(xaml, Does.Contain("Command=\"{Binding DeleteDriverCommand}\""));
        var code = XamlViewFile.Read("Views/Driver/DriverManagementView.xaml.cs");
        Assert.That(code, Does.Contain("DriversViewModel"));
    }

    [Test]
    public void VehicleForm_HostsVehiclesView()
    {
        var form = XamlViewFile.Read("Views/Vehicle/VehicleForm.xaml.cs");
        Assert.That(form, Does.Contain("new VehiclesView(startup)"));
        var host = XamlViewFile.Read("Views/Vehicle/VehiclesView.xaml.cs");
        Assert.That(host, Does.Contain("new VehicleManagementView(startup)"));
    }

    [Test]
    public async Task TripBoardRefresh_LoadsRowsFromTripEventService()
    {
        var mock = new Mock<ITripEventService>();
        mock.Setup(s => s.GetAllTripsAsync()).ReturnsAsync(
        [
            new TripEvent
            {
                ExternalTicketNo = "319098876",
                GroupOrActivity = "Football Game - JV",
                RequestingSchool = "HS"
            }
        ]);

        var vm = new ActivityManagementViewModel(mock.Object);
        await ((IAsyncRelayCommand)vm.RefreshCommand).ExecuteAsync(null);

        Assert.That(vm.Trips, Has.Count.EqualTo(1));
        Assert.That(vm.Trips[0].ExternalTicketNo, Is.EqualTo("319098876"));
        Assert.That(vm.StatusMessage, Does.Contain("1"));
        mock.Verify(s => s.GetAllTripsAsync(), Times.AtLeastOnce);
    }
}
