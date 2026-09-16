using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels.Student;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class SchoolCatalogPickerDialogViewModelTests
{
    [Test]
    public void Constructor_DoesNotPreselectTheFirstSchool()
    {
        var schools = new[]
        {
            new Destination { DestinationId = 1, Name = "Oakridge School" },
            new Destination { DestinationId = 2, Name = "Central High" }
        };

        var vm = new SchoolCatalogPickerDialogViewModel(schools, "Choose a school");

        Assert.That(vm.SelectedSchool, Is.Null);
        Assert.That(vm.ConfirmCommand.CanExecute(null), Is.False);
    }
}
