using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels.Student;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class CatalogPickerDialogViewModelTests
{
    [Test]
    public void Constructor_DoesNotPreselectTheFirstItem()
    {
        var schools = new[]
        {
            new Destination { DestinationId = 1, Name = "Oakridge School" },
            new Destination { DestinationId = 2, Name = "Central High" }
        };

        var vm = new CatalogPickerDialogViewModel<Destination>(schools, "Choose a school", "Select school");

        Assert.That(vm.SelectedItem, Is.Null);
        Assert.That(vm.ConfirmCommand.CanExecute(null), Is.False);
        Assert.That(vm.Title, Is.EqualTo("Select school"));
        Assert.That(vm.Items, Is.EqualTo(schools));
    }

    [Test]
    public void SelectedItem_IsTheTypedCatalogRow()
    {
        var school = new Destination { DestinationId = 1, Name = "Oakridge School" };
        var vm = new CatalogPickerDialogViewModel<Destination>(
            new[] { school },
            "Choose a school",
            "Select school");
        vm.SelectedItem = school;

        Assert.That(vm.ConfirmCommand.CanExecute(null), Is.True);
        Assert.That(vm.SelectedItem, Is.SameAs(school));
    }
}
