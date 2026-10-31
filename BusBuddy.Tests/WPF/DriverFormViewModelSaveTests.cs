using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Driver;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Driver form Save path must persist <see cref="Driver.SchoolBusEndorsement"/> (UI checkbox + LoadDriverForEdit copy).
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DriverFormViewModelSaveTests
{
    [Test]
    public async Task SaveDriver_NewDriver_PassesSchoolBusEndorsementToService()
    {
        Driver? saved = null;
        var driverService = new Mock<IDriverService>();
        driverService
            .Setup(s => s.GetAllDriversAsync())
            .ReturnsAsync(new List<Driver>());
        driverService
            .Setup(s => s.ValidateDriverAsync(It.IsAny<Driver>()))
            .ReturnsAsync(new List<string>());
        driverService
            .Setup(s => s.AddDriverAsync(It.IsAny<Driver>()))
            .ReturnsAsync((Driver d) =>
            {
                d.DriverId = 42;
                saved = d;
                return d;
            });

        var vm = new DriverFormViewModel(driverService.Object);
        vm.PrepareNewDriver();
        vm.Driver.FirstName = "Pat";
        vm.Driver.LastName = "Driver";
        vm.Driver.DriverPhone = "7195550100";
        vm.Driver.LicenseNumber = "D1234567";
        vm.Driver.LicenseClass = "Class B";
        vm.Driver.SchoolBusEndorsement = true;

        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        if (vm.SaveDriverCommand is not IAsyncRelayCommand asyncSave)
        {
            Assert.Fail("SaveDriverCommand should be IAsyncRelayCommand");
            return;
        }

        await asyncSave.ExecuteAsync(null);

        closed.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.SchoolBusEndorsement.Should().BeTrue();
        saved.DriverName.Should().Contain("Pat");
        driverService.Verify(s => s.AddDriverAsync(It.Is<Driver>(d => d.SchoolBusEndorsement)), Times.Once);
        driverService.Verify(s => s.UpdateDriverAsync(It.IsAny<Driver>()), Times.Never);
    }

    [Test]
    public async Task SaveDriver_EditMode_PreservesSchoolBusEndorsementOnUpdate()
    {
        var existing = new Driver
        {
            DriverId = 7,
            DriverName = "Sam Driver",
            FirstName = "Sam",
            LastName = "Driver",
            DriverPhone = "7195550199",
            LicenseNumber = "D7654321",
            LicenseClass = "Class A",
            DriversLicenceType = "CDL",
            Status = "Active",
            SchoolBusEndorsement = false,
            TrainingComplete = true,
            LicenseExpiryDate = DateTime.Today.AddDays(120),
            CreatedDate = DateTime.UtcNow.AddDays(-30)
        };

        Driver? updated = null;
        var driverService = new Mock<IDriverService>();
        driverService
            .Setup(s => s.GetAllDriversAsync())
            .ReturnsAsync(new List<Driver> { existing });
        driverService
            .Setup(s => s.ValidateDriverAsync(It.IsAny<Driver>()))
            .ReturnsAsync(new List<string>());
        driverService
            .Setup(s => s.UpdateDriverAsync(It.IsAny<Driver>()))
            .ReturnsAsync((Driver d) =>
            {
                updated = d;
                return true;
            });

        var vm = new DriverFormViewModel(driverService.Object);
        vm.SelectedDriver = existing;
        vm.IsEditMode.Should().BeTrue();
        vm.Driver.SchoolBusEndorsement.Should().BeFalse();

        vm.Driver.SchoolBusEndorsement = true;

        if (vm.SaveDriverCommand is not IAsyncRelayCommand asyncSave)
        {
            Assert.Fail("SaveDriverCommand should be IAsyncRelayCommand");
            return;
        }

        await asyncSave.ExecuteAsync(null);

        updated.Should().NotBeNull();
        updated!.DriverId.Should().Be(7);
        updated.SchoolBusEndorsement.Should().BeTrue();
        driverService.Verify(s => s.UpdateDriverAsync(It.Is<Driver>(d => d.SchoolBusEndorsement && d.DriverId == 7)), Times.Once);
        driverService.Verify(s => s.AddDriverAsync(It.IsAny<Driver>()), Times.Never);
    }
}
