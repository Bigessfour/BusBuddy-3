using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
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
        studentService
            .Setup(s => s.AddStudentAsync(It.IsAny<Student>()))
            .ReturnsAsync((Student s) =>
            {
                s.StudentId = 42;
                return Result.Success(s);
            });

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
        else
        {
            Assert.Fail("SaveCommand should be IAsyncRelayCommand");
        }

        mapsGeo.Verify(
            m => m.ValidateAndGeocodeAsync(
                "100 Main St",
                "Wiley",
                "CO",
                "81092",
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        vm.Student.Should().NotBeNull();
        vm.Student.Latitude.Should().Be(37.123m);
        vm.Student.Longitude.Should().Be(-102.456m);
        studentService.Verify(s => s.AddStudentAsync(It.IsAny<Student>()), Times.Once);
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
    public async Task SaveStudentAsync_FailedAddressValidation_DoesNotWriteCoordinates()
    {
        var studentService = new Mock<IStudentService>();
        studentService
            .Setup(s => s.ValidateStudentAsync(It.IsAny<Student>()))
            .ReturnsAsync(new List<string>());
        studentService
            .Setup(s => s.AddStudentAsync(It.IsAny<Student>()))
            .ReturnsAsync((Student s) =>
            {
                s.StudentId = 43;
                return Result.Success(s);
            });

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
                Ok = false,
                ErrorMessage = "undeliverable or incomplete",
            });

        var vm = new StudentFormViewModel(studentService.Object, enableValidation: true, mapsGeoService: mapsGeo.Object)
        {
            Student =
            {
                StudentName = "TEST_STUDENT_UNVALIDATED",
                Grade = "3",
                HomeAddress = "not a real street",
                City = "Wiley",
                State = "CO",
                Zip = "81092",
            }
        };

        if (vm.SaveCommand is IAsyncRelayCommand asyncSave)
        {
            await asyncSave.ExecuteAsync(null);
        }
        else
        {
            Assert.Fail("SaveCommand should be IAsyncRelayCommand");
        }

        vm.Student.Latitude.Should().BeNull();
        vm.Student.Longitude.Should().BeNull();
        vm.Student.HasValidatedHomeCoordinates.Should().BeFalse();
        studentService.Verify(s => s.AddStudentAsync(It.IsAny<Student>()), Times.Once);
    }

    [Test]
    public async Task PlacesApply_DoesNotPinUntilAddressValidationSucceeds()
    {
        var studentService = new Mock<IStudentService>();
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
                Ok = false,
                Precision = "OTHER",
                ErrorMessage =
                    "Rejected. Google could not confirm this as a house. No map pin. Correct the house number and street name, or pick a Google suggestion, then click Validate Address.",
            });

        var vm = new StudentFormViewModel(
            studentService.Object,
            enableValidation: true,
            mapsGeoService: mapsGeo.Object);

        await vm.ApplyAppliedAddressAsync(new PlaceAddressApplier.AppliedAddress(
            "12200 BenVerified Ave",
            "Lamar",
            "CO",
            "81052",
            38.0872,
            -102.6208,
            "Lamar, CO 81052, USA",
            "ChIJcity"));

        vm.Student.HomeAddress.Should().Be("12200 BenVerified Ave");
        vm.Student.City.Should().Be("Lamar");
        vm.Student.State.Should().Be("CO");
        vm.Student.Zip.Should().Be("81052");
        vm.Student.Latitude.Should().BeNull();
        vm.Student.Longitude.Should().BeNull();
        vm.Student.HasValidatedHomeCoordinates.Should().BeFalse();
        vm.AddressValidationMessage.Should().StartWith("Rejected.");
        vm.AddressValidationMessage.Should().Contain("No map pin");
        vm.AddressValidationMessage.Should().Contain("Validate Address");
    }

    [Test]
    public async Task ValidateAddress_FailedPrecision_ClearsExistingPinAndPersistsNulls()
    {
        var studentService = new Mock<IStudentService>();
        studentService
            .Setup(s => s.UpdateHomeGeocodeAsync(63, null, null, null))
            .ReturnsAsync(true);

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
                Ok = false,
                Precision = "OTHER",
                ErrorMessage =
                    "Rejected. Google could not confirm this as a house. No map pin. Correct the house number and street name, or pick a Google suggestion, then click Validate Address.",
            });

        var existing = new Student
        {
            StudentId = 63,
            StudentName = "TEST_STUDENT_BENVERIFIED",
            Grade = "3",
            HomeAddress = "12200 BenVerified Ave",
            City = "Lamar",
            State = "CO",
            Zip = "81052",
            Latitude = 38.0872m,
            Longitude = -102.6207m,
            PlaceId = "ChIJstale",
        };

        var vm = new StudentFormViewModel(
            studentService.Object,
            existing,
            enableValidation: true,
            mapsGeo.Object,
            null);

        if (vm.ValidateAddressCommand is IAsyncRelayCommand asyncValidate)
        {
            await asyncValidate.ExecuteAsync(null);
        }
        else
        {
            Assert.Fail("ValidateAddressCommand should be IAsyncRelayCommand");
        }

        vm.Student.Latitude.Should().BeNull();
        vm.Student.Longitude.Should().BeNull();
        vm.Student.PlaceId.Should().BeNull();
        vm.Student.HasValidatedHomeCoordinates.Should().BeFalse();
        vm.AddressValidationMessage.Should().StartWith("Rejected.");
        vm.AddressValidationMessage.Should().Contain("No map pin");
        studentService.Verify(s => s.UpdateHomeGeocodeAsync(63, null, null, null), Times.Once);
    }

    [Test]
    public async Task SpecialNeedsStudent_CannotBeSwitchedToCatalogStop()
    {
        var studentService = new Mock<IStudentService>();
        var vm = new StudentFormViewModel(studentService.Object, student: null, enableValidation: false)
        {
            Student =
            {
                StudentName = "TEST_STUDENT_SN",
                Grade = "K",
                RequiresSpecialNeedsBus = true,
                Latitude = 38.08m,
                Longitude = -102.62m,
            }
        };

        vm.SelectedPickupStop = new PickupStop { PickupStopId = 7, Name = "Town corner" };
        vm.Student.PickupStopId.Should().BeNull();
        vm.Student.PickupMode.Should().Be(LocationTypes.PickupModeHome);

        if (vm.SuggestNearestPickupStopCommand is IAsyncRelayCommand suggest)
        {
            await suggest.ExecuteAsync(null);
        }

        vm.Student.PickupStopId.Should().BeNull();
        vm.PickupStopHint.Should().Contain("home pickup");
    }

    [Test]
    public void CheckingSpecialNeeds_ClearsCatalogStop()
    {
        var studentService = new Mock<IStudentService>();
        var vm = new StudentFormViewModel(studentService.Object, student: null, enableValidation: false);
        vm.SelectedPickupStop = new PickupStop { PickupStopId = 9, Name = "Main & 4th" };
        vm.Student.PickupStopId.Should().Be(9);

        vm.Student.RequiresSpecialNeedsBus = true;

        vm.Student.PickupStopId.Should().BeNull();
        vm.Student.PickupMode.Should().Be(LocationTypes.PickupModeHome);
        vm.UsesHomeAsPickupStop.Should().BeTrue();
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
        Assert.That(xaml, Does.Contain("controls:PlacesAddressBox"));
        Assert.That(xaml, Does.Contain("AddressApplied=\"HomeAddress_Applied\""));
        Assert.That(xaml, Does.Contain("AddressText"));
    }

    [Test]
    public void StudentFormXaml_PickupComboAndActionButtonsFollowSyncfusionIconAndDisplayRules()
    {
        var xaml = XamlViewFile.Read("Views/Student/StudentForm.xaml");
        Assert.That(xaml, Does.Contain("Property=\"LargeIcon\" Value=\"{x:Null}\""));
        Assert.That(xaml, Does.Contain("DisplayMemberPath=\"Name\""));
        Assert.That(xaml, Does.Not.Contain("SelectedValuePath"));
        Assert.That(xaml, Does.Contain("Name=\"PickupStopComboBox\""));
        Assert.That(xaml, Does.Contain("Style=\"{StaticResource StudentFormActionButtonStyle}\""));
        Assert.That(xaml, Does.Contain("Label=\"Suggest nearest\""));
        Assert.That(xaml, Does.Contain("Label=\"Use home as stop\""));
        Assert.That(xaml, Does.Not.Contain("SizeMode=\"Small\""));
        Assert.That(xaml, Does.Not.Contain("Import CSV"), "CSV import stays on the roster toolbar, not Edit Student");
    }
}
