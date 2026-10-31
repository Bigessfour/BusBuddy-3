using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Fuel;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Fuel dialog VM: validate gates CloseRequested; valid Save persists via <see cref="IFuelService"/>.
/// XAML contracts stay in <see cref="FuelDialogXamlContractTests"/>.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class FuelDialogViewModelTests
{
    private static Bus SampleBus(int id = 5) =>
        new()
        {
            BusId = id,
            BusNumber = $"BUS-{id:D2}",
            Make = "Thomas",
            Model = "C2",
            Year = 2024,
            Status = "Active",
            SeatingCapacity = 40
        };

    private static Fuel NewDraft(int vehicleId = 5) =>
        new()
        {
            FuelId = 0,
            FuelDate = DateTime.SpecifyKind(new DateTime(2026, 10, 31), DateTimeKind.Utc),
            FuelLocation = string.Empty,
            VehicleFueledId = vehicleId,
            VehicleOdometerReading = 0,
            FuelType = "Diesel",
            Gallons = null,
            PricePerGallon = null,
            TotalCost = null
        };

    private static Fuel Existing(int fuelId = 9, int vehicleId = 5) =>
        new()
        {
            FuelId = fuelId,
            FuelDate = DateTime.SpecifyKind(new DateTime(2026, 10, 15), DateTimeKind.Utc),
            FuelLocation = "Barn",
            VehicleFueledId = vehicleId,
            VehicleOdometerReading = 12000,
            FuelType = "Diesel",
            Gallons = 40m,
            PricePerGallon = 3.5m,
            TotalCost = 140m
        };

    private static (FuelDialogViewModel Vm, Mock<IFuelService> Fuel, Mock<IBusService> Bus) Create(
        Fuel fuel,
        IEnumerable<Bus>? buses = null)
    {
        var busService = new Mock<IBusService>();
        busService.Setup(b => b.GetAllBusesAsync()).ReturnsAsync(buses ?? new List<Bus> { SampleBus() });

        var fuelService = new Mock<IFuelService>();
        fuelService.Setup(f => f.GetDistinctFuelLocationsAsync())
            .ReturnsAsync(new List<string> { "Barn", "Co-op" });
        fuelService.Setup(f => f.GetFuelRecordsByVehicleAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<Fuel>());

        var catalog = new Mock<IFuelLocationCatalog>();
        catalog.Setup(c => c.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Barn", "Co-op" });

        var vm = new FuelDialogViewModel(fuel, busService.Object, catalog.Object, fuelService.Object);
        return (vm, fuelService, busService);
    }

    private static async Task WaitForBusesAsync(FuelDialogViewModel vm, int expectedCount = 1)
    {
        var sw = Stopwatch.StartNew();
        while (vm.AvailableBuses.Count < expectedCount)
        {
            if (sw.ElapsedMilliseconds > 5000)
            {
                throw new TimeoutException("Fuel dialog buses did not load in time.");
            }

            await Task.Delay(25);
        }
    }

    private static void FillValidFields(FuelDialogViewModel vm, Bus bus)
    {
        vm.SelectedBus = bus;
        vm.FuelLocationText = "Barn";
        vm.Fuel.FuelType = "Diesel";
        vm.GallonsText = "25.5";
        vm.PricePerGallonText = "3.499";
        vm.OdometerText = "15000";
        vm.CommitNumericFormatting();
    }

    [Test]
    public async Task Save_WhenInvalid_DoesNotCallFuelService_OrClose()
    {
        var (vm, fuel, _) = Create(NewDraft());
        await WaitForBusesAsync(vm);

        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        closed.Should().BeFalse();
        vm.IsValid.Should().BeFalse();
        fuel.Verify(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
        fuel.Verify(f => f.UpdateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
    }

    [Test]
    public async Task Save_NewValidRecord_CallsCreateFuelRecordAsync_AndCloses()
    {
        var draft = NewDraft();
        var (vm, fuel, _) = Create(draft);
        await WaitForBusesAsync(vm);

        fuel.Setup(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()))
            .ReturnsAsync((Fuel f) =>
            {
                f.FuelId = 42;
                return f;
            });

        FillValidFields(vm, vm.AvailableBuses[0]);

        var closedWithSave = false;
        vm.CloseRequested += (_, saved) => closedWithSave = saved;

        await vm.SaveCommand.ExecuteAsync(null);

        closedWithSave.Should().BeTrue();
        draft.FuelId.Should().Be(42);
        fuel.Verify(
            f => f.CreateFuelRecordAsync(It.Is<Fuel>(r =>
                r.VehicleFueledId == 5
                && r.FuelLocation == "Barn"
                && r.Gallons == 25.5m)),
            Times.Once);
        fuel.Verify(f => f.UpdateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
    }

    [Test]
    public async Task Save_ExistingValidRecord_CallsUpdateFuelRecordAsync_AndCloses()
    {
        var existing = Existing();
        var (vm, fuel, _) = Create(existing);
        await WaitForBusesAsync(vm);

        fuel.Setup(f => f.UpdateFuelRecordAsync(It.IsAny<Fuel>()))
            .ReturnsAsync((Fuel f) => f);

        FillValidFields(vm, vm.AvailableBuses[0]);
        vm.GallonsText = "30";
        vm.CommitNumericFormatting();

        var closedWithSave = false;
        vm.CloseRequested += (_, saved) => closedWithSave = saved;

        await vm.SaveCommand.ExecuteAsync(null);

        closedWithSave.Should().BeTrue();
        fuel.Verify(
            f => f.UpdateFuelRecordAsync(It.Is<Fuel>(r => r.FuelId == 9 && r.Gallons == 30m)),
            Times.Once);
        fuel.Verify(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
    }

    [Test]
    public async Task Save_WhenServiceThrows_DoesNotClose()
    {
        var (vm, fuel, _) = Create(NewDraft());
        await WaitForBusesAsync(vm);

        fuel.Setup(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()))
            .ThrowsAsync(new InvalidOperationException("Could not save fuel record — the selected bus is missing or invalid."));

        FillValidFields(vm, vm.AvailableBuses[0]);

        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        closed.Should().BeFalse();
        fuel.Verify(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()), Times.Once);
    }
}
