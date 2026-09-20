using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Route;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class RouteStopEditDialogViewModelTests
{
    [Test]
    public void ApplyAddress_WritesCoordinatesAndEnablesSave()
    {
        var vm = new RouteStopEditDialogViewModel("Stop 1", string.Empty);
        Assert.That(vm.SaveCommand.CanExecute(null), Is.False);

        vm.ApplyAddress(new PlaceAddressApplier.AppliedAddress(
            "100 Main St",
            "Wiley",
            "CO",
            "81092",
            38.15,
            -102.6,
            "100 Main St, Wiley, CO 81092",
            "ChIJ"));

        Assert.That(vm.StopAddress, Does.Contain("100 Main St"));
        Assert.That(vm.Latitude, Is.EqualTo(38.15m).Within(0.0001m));
        Assert.That(vm.Longitude, Is.EqualTo(-102.6m).Within(0.0001m));
        Assert.That(vm.HasValidatedCoordinates, Is.True);
        Assert.That(vm.SaveCommand.CanExecute(null), Is.True);
        Assert.That(vm.CoordinateStatus, Does.Contain("38.15"));
    }
}
