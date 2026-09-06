using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
public class StudentFormViewModelSaveTests
{
    [Test]
    public async Task SaveStudentAsync_WithRegisteredService_StillCallsMapsValidation()
    {
        var studentService = new Mock<IStudentService>();
        studentService
            .Setup(s => s.ValidateStudentAsync(It.IsAny<Student>()))
            .ReturnsAsync(new List<string>());

        var mapsGeo = new Mock<IMapsGeoService>();
        mapsGeo.Setup(m => m.IsConfigured).Returns(true);
        mapsGeo
            .Setup(m => m.ValidateAndGeocodeAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MapsGeocodeResult
            {
                Ok = true,
                Latitude = 37.123,
                Longitude = -102.456,
                FormattedAddress = "100 Main St, Wiley, CO 81092",
                Precision = "ROOFTOP",
            });

        var vm = new StudentFormViewModel(studentService.Object, enableValidation: true, mapsGeoService: mapsGeo.Object)
        {
            Student =
            {
                StudentName = "Test Student",
                Grade = "3",
                HomeAddress = "100 Main St",
                City = "Wiley",
                State = "CO",
                Zip = "81092",
            }
        };

        if (vm.SaveCommand is IAsyncRelayCommand asyncSave)
        {
            await asyncSave.ExecuteAsync(null);
        }

        mapsGeo.Verify(
            m => m.ValidateAndGeocodeAsync(
                "100 Main St",
                "Wiley",
                "CO",
                "81092",
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        vm.Student.Latitude.Should().Be(37.123m);
        vm.Student.Longitude.Should().Be(-102.456m);
    }

    [Test]
    public async Task SaveStudentAsync_InvalidRouteFromService_ReportsFieldErrorWithoutThrowing()
    {
        var studentService = new Mock<IStudentService>();
        studentService
            .Setup(s => s.ValidateStudentAsync(It.IsAny<Student>()))
            .ReturnsAsync(new List<string> { "AM Route 'Route Z' does not exist" });

        var vm = new StudentFormViewModel(studentService.Object, student: null, enableValidation: false)
        {
            Student =
            {
                StudentName = "Test Student",
                Grade = "3",
                AMRoute = "Route Z"
            }
        };

        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        if (vm.SaveCommand is IAsyncRelayCommand asyncSave)
        {
            await asyncSave.ExecuteAsync(null);
        }
        else
        {
            Assert.Fail("SaveCommand should be IAsyncRelayCommand");
        }

        closed.Should().BeFalse();
        vm.HasValidationErrors.Should().BeTrue();
        vm.ValidationErrors.Should().Contain(e => e.Contains("Route Z", StringComparison.OrdinalIgnoreCase));
        studentService.Verify(s => s.AddStudentAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public async Task SaveStudentAsync_BlankName_DoesNotCallService()
    {
        var studentService = new Mock<IStudentService>();
        var vm = new StudentFormViewModel(studentService.Object, student: null, enableValidation: false)
        {
            Student =
            {
                StudentName = "  ",
                Grade = "3"
            }
        };

        if (vm.SaveCommand is IAsyncRelayCommand asyncSave)
        {
            await asyncSave.ExecuteAsync(null);
        }

        vm.HasStudentNameFieldError.Should().BeTrue();
        studentService.Verify(s => s.ValidateStudentAsync(It.IsAny<Student>()), Times.Never);
        studentService.Verify(s => s.AddStudentAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public void StudentFormFields_IncludesRouteAndPhoneKeys()
    {
        StudentFormFields.AMRoute.Should().Be("AMRoute");
        StudentFormFields.CellPhone.Should().Be("CellPhone");
    }

    [Test]
    public void StudentFormXaml_WiresPlacesAutocompletePopup()
    {
        var xaml = XamlViewFile.Read("Views/Student/StudentForm.xaml");
        Assert.That(xaml, Does.Contain("AddressSuggestionsPopup"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding AddressSuggestions}\""));
        Assert.That(xaml, Does.Contain("IsOpen=\"{Binding IsAddressSuggestionPopupOpen, Mode=OneWay}\""));
    }
}
