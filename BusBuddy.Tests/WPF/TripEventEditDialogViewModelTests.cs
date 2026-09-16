using BusBuddy.Core.Models.Trips;
using BusBuddy.WPF.ViewModels.Activity;
using CommunityToolkit.Mvvm.Input;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class TripEventEditDialogViewModelTests
{
    [Test]
    public async Task BuildTrip_MapsKnownSportToAthleticType()
    {
        var vm = new TripEventEditDialogViewModel(null, null, null, null, null);
        vm.Purpose = "Sports";
        vm.SportKind = "Volleyball";
        vm.LeaveTimeText = "14:30";
        vm.ReturnTimeText = "18:00";

        Assert.That(vm.ValidateTrip(), Is.True);
        var trip = await vm.BuildTripAsync();

        Assert.That(trip.Type, Is.EqualTo(TripType.Athletic_Volleyball));
        Assert.That(trip.CustomType, Is.Null);
        Assert.That(trip.RouteId, Is.Null);
        Assert.That(trip.Purpose, Is.EqualTo("Sports"));
    }

    [Test]
    public async Task BuildTrip_MapsClerkTypedSportToCustomType()
    {
        var vm = new TripEventEditDialogViewModel(null, null, null, null, null);
        vm.Purpose = "Sports";
        vm.SportKind = "Track";
        vm.LeaveTimeText = "08:00";
        vm.ReturnTimeText = "16:00";

        var trip = await vm.BuildTripAsync();

        Assert.That(trip.Type, Is.EqualTo(TripType.Custom));
        Assert.That(trip.CustomType, Is.EqualTo("Track"));
        Assert.That(trip.Purpose, Is.EqualTo("Track"));
    }

    [Test]
    public async Task BuildTrip_MapsNonSportsPurposeToCustom()
    {
        var vm = new TripEventEditDialogViewModel(null, null, null, null, null);
        vm.Purpose = "Band";
        vm.LeaveTimeText = "09:00";
        vm.ReturnTimeText = "15:00";

        var trip = await vm.BuildTripAsync();

        Assert.That(trip.Type, Is.EqualTo(TripType.Custom));
        Assert.That(trip.CustomType, Is.EqualTo("Band"));
        Assert.That(vm.IsSports, Is.False);
    }

    [Test]
    public void SaveCommand_AlignsWithDialogSurface()
    {
        var vm = new TripEventEditDialogViewModel(null, null, null, null, null);
        Assert.That(vm.SaveCommand, Is.InstanceOf<IAsyncRelayCommand>());
        Assert.That(vm.RememberPurposeCommand, Is.Not.Null);
        Assert.That(vm.RemovePurposeCommand, Is.Not.Null);
        Assert.That(vm.RememberSportCommand, Is.Not.Null);
        Assert.That(vm.RemoveSportCommand, Is.Not.Null);
    }

    [Test]
    public void ValidateTrip_AllowsEmptyLeaveAndOvernightReturn()
    {
        var vm = new TripEventEditDialogViewModel(null, null, null, null, null);
        vm.LeaveTimeText = string.Empty;
        vm.ReturnTimeText = "15:00";
        Assert.That(vm.ValidateTrip(), Is.True);

        vm.LeaveTimeText = "18:00";
        vm.ReturnTimeText = "01:00";
        Assert.That(vm.ValidateTrip(), Is.True);
    }

    [Test]
    public async Task BuildTrip_OvernightReturn_SetsReturnIsNextDay()
    {
        var vm = new TripEventEditDialogViewModel(null, null, null, null, null);
        vm.LeaveTimeText = "18:00";
        vm.ReturnTimeText = "01:00";
        var trip = await vm.BuildTripAsync();
        Assert.That(trip.ReturnIsNextDay, Is.True);
        Assert.That(trip.Status, Is.Not.EqualTo(TripStatus.Confirmed));
    }

    [Test]
    public async Task BuildTrip_ChangingDestinationName_ClearsCatalogId()
    {
        var original = new TripEvent
        {
            TripEventId = 9,
            DestinationName = "Strasburg HS",
            DestinationLocationId = 44,
            PickupTime = TimeSpan.FromHours(6),
            ReturnClockTime = TimeSpan.FromHours(23),
            Status = TripStatus.Assigned
        };
        var vm = new TripEventEditDialogViewModel(original, null, null, null, null);
        vm.DestinationName = "Woodland Park HS";
        var trip = await vm.BuildTripAsync();
        Assert.That(trip.DestinationLocationId, Is.Null);
        Assert.That(trip.DestinationName, Is.EqualTo("Woodland Park HS"));
    }

    [Test]
    public async Task BuildTrip_PreservesMultiAssetWhenNoVehicleSelected()
    {
        var original = new TripEvent
        {
            TripEventId = 11,
            IsMultiAsset = true,
            AssignedBusNumber = "25, 23",
            PickupTime = TimeSpan.FromHours(8),
            ReturnClockTime = TimeSpan.FromHours(9),
            DestinationName = "Lamar High School"
        };
        var vm = new TripEventEditDialogViewModel(original, null, null, null, null);
        var trip = await vm.BuildTripAsync();
        Assert.That(trip.IsMultiAsset, Is.True);
        Assert.That(trip.VehicleId, Is.Null);
        Assert.That(trip.AssignedBusNumber, Is.EqualTo("25, 23"));
    }
}
