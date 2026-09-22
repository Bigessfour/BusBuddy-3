using System;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class PlacesAddressSurfaceTests
{
    [Test]
    public void StudentAndSchoolForms_KeepPlacesAutocompletePopups()
    {
        var student = XamlViewFile.Read("Views/Student/StudentForm.xaml");
        Assert.That(student, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(student, Does.Contain("AddressApplied=\"HomeAddress_Applied\""));
        Assert.That(student, Does.Contain("AddCatalogStopCommand"));

        var school = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml");
        Assert.That(school, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(school, Does.Contain("AddressApplied=\"SchoolAddress_Applied\""));
        Assert.That(school, Does.Contain("ValidateSchoolAddressButton_Click"));
        Assert.That(school, Does.Not.Contain("SchoolAddressSuggestionsPopup"));
        var placesBox = XamlViewFile.Read("Controls/PlacesAddressBox.xaml");
        Assert.That(placesBox, Does.Contain("BusBuddyInputStyles.xaml"));
    }

    [Test]
    public void RemainingAddressIntakeForms_UseSharedPlacesAddressBox()
    {
        var pickup = XamlViewFile.Read("Views/Student/PickupStopForm.xaml");
        Assert.That(pickup, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(pickup, Does.Contain("AddressApplied=\"StopAddress_Applied\""));
        Assert.That(pickup, Does.Contain("Watermark=\"e.g. Oak &amp; 4th\""));

        var driver = XamlViewFile.Read("Views/Driver/DriverForm.xaml");
        Assert.That(driver, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(driver, Does.Contain("AddressText=\"{Binding Driver.Address"));
        Assert.That(driver, Does.Contain("AddressApplied=\"DriverAddress_Applied\""));

        var settings = XamlViewFile.Read("Views/Settings/SettingsView.xaml");
        Assert.That(settings, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(settings, Does.Contain("AddressText=\"{Binding DepotAddress"));
        Assert.That(settings, Does.Contain("AddressApplied=\"DepotAddress_Applied\""));

        var routeStop = XamlViewFile.Read("Views/Route/RouteStopEditDialog.xaml");
        Assert.That(routeStop, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(routeStop, Does.Contain("UseFormattedAddress=\"True\""));
        Assert.That(routeStop, Does.Contain("AddressApplied=\"StopAddressBox_AddressApplied\""));
        Assert.That(routeStop, Does.Contain("AddressText=\"{Binding StopAddress"));
        Assert.That(routeStop, Does.Not.Contain("DataContext=\"{Binding}\""));
        var routeStopCode = XamlViewFile.Read("Views/Route/RouteStopEditDialog.xaml.cs");
        Assert.That(
            routeStopCode.IndexOf("InitializeComponent()", StringComparison.Ordinal),
            Is.LessThan(routeStopCode.IndexOf("ApplyTheme(this)", StringComparison.Ordinal)));
        Assert.That(
            routeStopCode.IndexOf("ApplyTheme(this)", StringComparison.Ordinal),
            Is.LessThan(routeStopCode.IndexOf("DataContext = ViewModel", StringComparison.Ordinal)));
        Assert.That(routeStopCode, Does.Contain("StopAddressBox.DataContext = ViewModel"));
        Assert.That(routeStopCode, Does.Not.Contain("BindViewModel"));
        var addStop = XamlViewFile.Read("ViewModels/Route/RouteAssignmentViewModel.Commands.cs");
        Assert.That(addStop, Does.Contain("new RouteStopEditDialogViewModel"));
        Assert.That(addStop, Does.Contain("new RouteStopEditDialog(stopVm)"));
        Assert.That(addStop, Does.Not.Contain("BindViewModel"));
        Assert.That(
            addStop.IndexOf("new RouteStopEditDialog(stopVm)", StringComparison.Ordinal),
            Is.LessThan(addStop.IndexOf("dialog.ShowDialog()", StringComparison.Ordinal)));

        var transfer = XamlViewFile.Read("Views/Student/StudentSchoolTransferForm.xaml");
        Assert.That(transfer, Does.Contain("AddressText=\"{Binding PickupAddress"));
        Assert.That(transfer, Does.Contain("AddressText=\"{Binding DropoffAddress"));

        var trip = XamlViewFile.Read("Views/Activity/TripEventEditDialog.xaml");
        Assert.That(trip, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(trip, Does.Contain("AddressText=\"{Binding DestinationName"));
    }

    [Test]
    public void SharedPlacesControl_WiresPopupAndCoordinator()
    {
        var xaml = XamlViewFile.Read("Controls/PlacesAddressBox.xaml");
        Assert.That(xaml, Does.Contain("SuggestionsPopup"));
        Assert.That(xaml, Does.Contain("AutoCompleteMode=\"None\""));
        var code = XamlViewFile.Read("Controls/PlacesAddressBox.xaml.cs");
        Assert.That(code, Does.Contain("PlacesAddressAutocompleteCoordinator"));
        Assert.That(code, Does.Contain("IPlacesAutocompleteService"));
        Assert.That(code, Does.Not.Contain("CityProperty"));
        Assert.That(code, Does.Contain("AddressApplied"));
    }

    [Test]
    public void PlaceAddressApplier_PrefersFormattedSingleLine()
    {
        var suggestion = new PlaceAutocompleteSuggestion
        {
            PlaceId = "ChIJ",
            PrimaryText = "100 Main St",
            DisplayText = "100 Main St, Wiley, CO"
        };
        var details = new PlaceAddressDetails
        {
            StreetLine = "100 Main St",
            City = "Wiley",
            State = "CO",
            Zip = "81092",
            FormattedAddress = "100 Main St, Wiley, CO 81092, USA",
            Latitude = 38.15,
            Longitude = -102.6
        };

        var applied = PlaceAddressApplier.Apply(suggestion, details);
        Assert.That(applied.Street, Is.EqualTo("100 Main St"));
        Assert.That(applied.SingleLine(), Is.EqualTo("100 Main St, Wiley, CO 81092, USA"));
        Assert.That(applied.Latitude, Is.EqualTo(38.15));
    }

    [Test]
    public void PlaceAddressApplier_FillsCityZipFromFormattedWhenComponentsMissing()
    {
        var suggestion = new PlaceAutocompleteSuggestion
        {
            PlaceId = "ChIJ",
            PrimaryText = "804 E Oak St",
            DisplayText = "804 E Oak St, Lamar, CO 81052, USA",
            SecondaryText = "Lamar, CO 81052, USA",
        };
        var details = new PlaceAddressDetails
        {
            StreetLine = "804 E Oak St",
            FormattedAddress = "804 E Oak St, Lamar, CO 81052, USA",
        };

        var applied = PlaceAddressApplier.Apply(suggestion, details);
        Assert.That(applied.City, Is.EqualTo("Lamar"));
        Assert.That(applied.State, Is.EqualTo("CO"));
        Assert.That(applied.Zip, Is.EqualTo("81052"));
    }

    [Test]
    public void UnhostedAddressValidationControl_IsGone_ClerkIntakeUsesPlacesAddressBox()
    {
        Assert.That(XamlViewFile.Exists("Controls/AddressValidationControl.xaml"), Is.False);
        Assert.That(XamlViewFile.Exists("Controls/AddressValidationControl.xaml.cs"), Is.False);
        Assert.That(XamlViewFile.Exists("Controls/AddressValidationControl.cs"), Is.False);
        Assert.That(XamlViewFile.Exists("Controls/PlacesAddressBox.xaml"), Is.True);
    }
}
