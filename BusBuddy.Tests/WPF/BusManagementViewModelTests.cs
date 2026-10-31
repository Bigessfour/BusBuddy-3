using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Bus;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Bus Management VM save gates and Cancel snapshot restore (detail panel, not inline grid edit).
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class BusManagementViewModelTests
{
    [Test]
    public async Task SaveBusCommand_NewBus_CallsAddBusAsync()
    {
        var busService = new Mock<IBusService>();
        busService
            .Setup(s => s.GetAllBusesAsync())
            .ReturnsAsync(Array.Empty<Bus>());
        busService
            .Setup(s => s.AddBusAsync(It.IsAny<Bus>()))
            .ReturnsAsync((Bus b) =>
            {
                b.BusId = 11;
                return b;
            });

        var vm = new BusManagementViewModel(busService.Object);
        await WaitUntilIdleAsync(vm);

        var draft = ValidDraft("BUS-11");
        vm.SelectedBus = draft;

        vm.SaveBusCommand.CanExecute(null).Should().BeTrue();
        await ((IAsyncRelayCommand)vm.SaveBusCommand).ExecuteAsync(null);

        busService.Verify(
            s => s.AddBusAsync(It.Is<Bus>(b => b.BusNumber == "BUS-11")),
            Times.Once);
        busService.Verify(s => s.UpdateBusAsync(It.IsAny<Bus>()), Times.Never);
        vm.SelectedBus!.BusId.Should().Be(11);
        vm.StatusMessage.Should().Contain("added");
    }

    [Test]
    public async Task SaveBusCommand_ExistingBus_CallsUpdateBusAsync()
    {
        var existing = ValidExisting(7, "BUS-07");
        var busService = new Mock<IBusService>();
        busService
            .Setup(s => s.GetAllBusesAsync())
            .ReturnsAsync(new List<Bus> { existing });
        busService
            .Setup(s => s.UpdateBusAsync(It.IsAny<Bus>()))
            .ReturnsAsync(true);

        var vm = new BusManagementViewModel(busService.Object);
        await WaitUntilIdleAsync(vm);

        vm.SelectedBus = vm.Buses.Should().ContainSingle(b => b.BusId == 7).Subject;
        vm.SelectedBus.Make = "Blue Bird";

        await ((IAsyncRelayCommand)vm.SaveBusCommand).ExecuteAsync(null);

        busService.Verify(
            s => s.UpdateBusAsync(It.Is<Bus>(b => b.BusId == 7 && b.Make == "Blue Bird")),
            Times.Once);
        busService.Verify(s => s.AddBusAsync(It.IsAny<Bus>()), Times.Never);
        vm.StatusMessage.Should().Contain("updated");
    }

    [Test]
    public async Task CanSaveBus_False_WhenBusNumberBlank()
    {
        var busService = new Mock<IBusService>();
        busService
            .Setup(s => s.GetAllBusesAsync())
            .ReturnsAsync(Array.Empty<Bus>());

        var vm = new BusManagementViewModel(busService.Object);
        await WaitUntilIdleAsync(vm);

        // BusNumber setter coerces whitespace to BUS-{id}; leave the backing default empty.
        vm.SelectedBus = new Bus
        {
            BusId = 0,
            Make = "Thomas",
            Model = "C2",
            LicenseNumber = "ABC1234",
            VINNumber = "1HGBH41JXMN109186",
            SeatingCapacity = 40,
            Year = 2024,
            Status = "Active",
            FleetType = "Regular"
        };
        vm.SelectedBus.BusNumber.Should().BeEmpty();
        vm.SaveBusCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedBus.BusNumber = "BUS-READY";
        vm.SaveBusCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public async Task CancelEdit_RestoresSnapshotForExistingBus()
    {
        var existing = ValidExisting(3, "BUS-03");
        existing.Make = "IC";
        var busService = new Mock<IBusService>();
        busService
            .Setup(s => s.GetAllBusesAsync())
            .ReturnsAsync(new List<Bus> { existing });

        var vm = new BusManagementViewModel(busService.Object);
        await WaitUntilIdleAsync(vm);

        vm.SelectedBus = vm.Buses.Should().ContainSingle().Subject;
        vm.SelectedBus.BusNumber = "CHANGED";
        vm.SelectedBus.Make = "Wrong";

        vm.CancelEditCommand.Execute(null);

        vm.SelectedBus.BusNumber.Should().Be("BUS-03");
        vm.SelectedBus.Make.Should().Be("IC");
        vm.StatusMessage.Should().Be("Edit cancelled");
        busService.Verify(s => s.UpdateBusAsync(It.IsAny<Bus>()), Times.Never);
    }

    private static Bus ValidDraft(string busNumber) =>
        new()
        {
            BusId = 0,
            BusNumber = busNumber,
            Make = "Thomas",
            Model = "C2",
            LicenseNumber = "ABC1234",
            VINNumber = "1HGBH41JXMN109186",
            SeatingCapacity = 40,
            Year = 2024,
            Status = "Active",
            FleetType = "Regular"
        };

    private static Bus ValidExisting(int id, string busNumber)
    {
        var bus = ValidDraft(busNumber);
        bus.BusId = id;
        return bus;
    }

    private static async Task WaitUntilIdleAsync(BusManagementViewModel vm)
    {
        for (var i = 0; i < 100 && vm.IsBusy; i++)
        {
            await Task.Delay(10);
        }

        vm.IsBusy.Should().BeFalse("initial fleet load should finish before assertions");
    }
}
