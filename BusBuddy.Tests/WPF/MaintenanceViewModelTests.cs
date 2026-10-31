using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Maintenance;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Maintenance grid VM: load/save/delete against mocked <see cref="IMaintenanceService"/>.
/// View XAML contracts stay in <see cref="MaintenanceViewTests"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MaintenanceViewModelTests
{
    private static Bus SampleBus(int id = 3) =>
        new()
        {
            BusId = id,
            BusNumber = $"BUS-{id:D2}",
            Make = "IC",
            Model = "CE",
            Year = 2022,
            Status = "Active",
            SeatingCapacity = 72,
            CurrentOdometer = 88000
        };

    private static Maintenance ExistingRecord(int id = 11, int vehicleId = 3) =>
        new()
        {
            MaintenanceId = id,
            Date = DateTime.SpecifyKind(new DateTime(2026, 10, 1), DateTimeKind.Utc),
            VehicleId = vehicleId,
            OdometerReading = 88000,
            MaintenanceCompleted = "Oil change",
            Vendor = "Fleet Shop",
            RepairCost = 120m,
            Status = MaintenanceRecordValidator.AllowedStatuses[2],
            Priority = MaintenanceRecordValidator.AllowedPriorities[1]
        };

    private static (
        MaintenanceViewModel Vm,
        Mock<IMaintenanceService> Maintenance,
        Mock<IBusService> Bus,
        List<(string Message, string Caption)> Confirms) Create(
        IEnumerable<Maintenance>? records = null,
        IEnumerable<Bus>? buses = null,
        bool confirmDelete = true)
    {
        var maintenance = new Mock<IMaintenanceService>();
        maintenance.Setup(m => m.GetAllMaintenanceRecordsAsync())
            .ReturnsAsync(records ?? new List<Maintenance> { ExistingRecord() });

        var bus = new Mock<IBusService>();
        bus.Setup(b => b.GetAllBusesAsync()).ReturnsAsync(buses ?? new List<Bus> { SampleBus() });

        var confirms = new List<(string, string)>();
        Func<string, string, bool> confirm = (message, caption) =>
        {
            confirms.Add((message, caption));
            return confirmDelete;
        };

        var vm = new MaintenanceViewModel(maintenance.Object, bus.Object, confirm);
        return (vm, maintenance, bus, confirms);
    }

    private static async Task WaitUntilLoadedAsync(MaintenanceViewModel vm, string? statusContains = null)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            var loaded = !string.Equals(vm.StatusMessage, "Loading maintenance records...", StringComparison.Ordinal)
                         && vm.StatusMessage is { Length: > 0 };
            var matches = statusContains is null
                || (vm.StatusMessage?.Contains(statusContains, StringComparison.Ordinal) ?? false);
            if (loaded && matches)
            {
                return;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                throw new TimeoutException(
                    $"Maintenance VM did not reach expected load state. Status={vm.StatusMessage}");
            }

            await Task.Delay(25);
        }
    }

    [Test]
    public async Task Load_PopulatesRecordsAndVehiclesFromServices()
    {
        var (vm, maintenance, bus, _) = Create(
            records: new List<Maintenance> { ExistingRecord(11), ExistingRecord(12) },
            buses: new List<Bus> { SampleBus(3), SampleBus(4) });

        await WaitUntilLoadedAsync(vm, "maintenance records");

        vm.Records.Should().HaveCount(2);
        vm.Vehicles.Should().HaveCount(2);
        maintenance.Verify(m => m.GetAllMaintenanceRecordsAsync(), Times.Once);
        bus.Verify(b => b.GetAllBusesAsync(), Times.Once);
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
        vm.DeleteCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public async Task Save_NewDraft_CallsCreateThenReloads()
    {
        var (vm, maintenance, _, _) = Create(records: new List<Maintenance>());
        await WaitUntilLoadedAsync(vm, "0 maintenance");

        maintenance.Setup(m => m.CreateMaintenanceRecordAsync(It.IsAny<Maintenance>()))
            .ReturnsAsync((Maintenance m) =>
            {
                m.MaintenanceId = 77;
                return m;
            });
        maintenance.Setup(m => m.GetAllMaintenanceRecordsAsync())
            .ReturnsAsync(() => new List<Maintenance> { ExistingRecord(77) });

        await vm.AddCommand.ExecuteAsync(null);
        vm.SelectedRecord.Should().NotBeNull();
        vm.SelectedRecord!.MaintenanceId.Should().Be(0);
        vm.SelectedRecord.MaintenanceCompleted = "Tire rotation";
        vm.SelectedRecord.Vendor = "Co-op";

        await ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

        maintenance.Verify(
            m => m.CreateMaintenanceRecordAsync(It.Is<Maintenance>(r =>
                r.MaintenanceCompleted == "Tire rotation"
                && r.Vendor == "Co-op")),
            Times.Once);
        maintenance.Verify(m => m.UpdateMaintenanceRecordAsync(It.IsAny<Maintenance>()), Times.Never);
        await WaitUntilLoadedAsync(vm, "maintenance records");
        vm.StatusMessage.Should().Be("1 maintenance records");
    }

    [Test]
    public async Task Save_ExistingRecord_CallsUpdateThenReloads()
    {
        var (vm, maintenance, _, _) = Create();
        await WaitUntilLoadedAsync(vm, "1 maintenance");

        maintenance.Setup(m => m.UpdateMaintenanceRecordAsync(It.IsAny<Maintenance>()))
            .ReturnsAsync((Maintenance m) => m);

        vm.SelectedRecord = vm.Records[0];
        vm.SelectedRecord.Vendor = "Updated Shop";

        await ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

        maintenance.Verify(
            m => m.UpdateMaintenanceRecordAsync(It.Is<Maintenance>(r =>
                r.MaintenanceId == 11 && r.Vendor == "Updated Shop")),
            Times.Once);
        maintenance.Verify(m => m.CreateMaintenanceRecordAsync(It.IsAny<Maintenance>()), Times.Never);
        vm.StatusMessage.Should().Be("1 maintenance records");
    }

    [Test]
    public async Task Delete_WhenConfirmed_CallsDeleteMaintenanceRecordAsync()
    {
        var (vm, maintenance, _, confirms) = Create(confirmDelete: true);
        await WaitUntilLoadedAsync(vm, "1 maintenance");

        maintenance.Setup(m => m.DeleteMaintenanceRecordAsync(11)).ReturnsAsync(true);

        vm.SelectedRecord = vm.Records[0];
        await ((IAsyncRelayCommand)vm.DeleteCommand).ExecuteAsync(null);

        confirms.Should().ContainSingle();
        maintenance.Verify(m => m.DeleteMaintenanceRecordAsync(11), Times.Once);
        vm.Records.Should().BeEmpty();
        vm.SelectedRecord.Should().BeNull();
        vm.StatusMessage.Should().Be("Deleted");
    }

    [Test]
    public async Task Delete_WhenCancelled_DoesNotCallService()
    {
        var (vm, maintenance, _, confirms) = Create(confirmDelete: false);
        await WaitUntilLoadedAsync(vm, "1 maintenance");

        vm.SelectedRecord = vm.Records[0];
        await ((IAsyncRelayCommand)vm.DeleteCommand).ExecuteAsync(null);

        confirms.Should().ContainSingle();
        maintenance.Verify(m => m.DeleteMaintenanceRecordAsync(It.IsAny<int>()), Times.Never);
        vm.Records.Should().ContainSingle();
        vm.StatusMessage.Should().Be("Delete cancelled");
    }

    [Test]
    public async Task Delete_Draft_DiscardsWithoutServiceCall()
    {
        var (vm, maintenance, _, _) = Create(records: new List<Maintenance>());
        await WaitUntilLoadedAsync(vm, "0 maintenance");

        await vm.AddCommand.ExecuteAsync(null);
        vm.SelectedRecord.Should().NotBeNull();
        vm.SelectedRecord!.MaintenanceId.Should().Be(0);

        await ((IAsyncRelayCommand)vm.DeleteCommand).ExecuteAsync(null);

        maintenance.Verify(m => m.DeleteMaintenanceRecordAsync(It.IsAny<int>()), Times.Never);
        vm.Records.Should().BeEmpty();
        vm.StatusMessage.Should().Be("Draft discarded");
    }
}
