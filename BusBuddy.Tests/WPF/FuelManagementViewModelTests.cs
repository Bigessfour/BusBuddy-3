using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Fuel;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Guards double-persist: after a successful fuel dialog, management must not call Create/Update again.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class FuelManagementViewModelTests
{
    [Test]
    public async Task AddFuelRecord_AfterSuccessfulDialog_DoesNotCallCreateOrUpdateAgain()
    {
        var bus = new Bus
        {
            BusId = 5,
            BusNumber = "BUS-05",
            Make = "Thomas",
            Model = "C2",
            Year = 2024,
            Status = "Active",
            SeatingCapacity = 40
        };

        var fuelService = new Mock<IFuelService>();
        fuelService.Setup(f => f.GetAllFuelRecordsAsync()).ReturnsAsync(Array.Empty<Fuel>());
        fuelService.Setup(f => f.GetDistinctFuelLocationsAsync())
            .ReturnsAsync(new List<string> { "Barn" });

        var busService = new Mock<IBusService>();
        busService.Setup(b => b.GetAllBusesAsync()).ReturnsAsync(new List<Bus> { bus });

        Fuel? dialogFuel = null;
        (bool? Result, bool RememberedNewLocation) ShowDialog(Fuel fuel)
        {
            dialogFuel = fuel;
            fuel.FuelId = 88;
            fuel.FuelLocation = "Barn";
            fuel.Gallons = 12.5m;
            fuel.PricePerGallon = 3.2m;
            fuel.TotalCost = 40m;
            fuel.VehicleOdometerReading = 10000;
            fuel.FuelType = "Diesel";
            return (true, false);
        }

        var vm = new FuelManagementViewModel(
            fuelService.Object,
            busService.Object,
            showFuelDialog: ShowDialog);

        await WaitUntilLoadedAsync(vm);

        await vm.AddCommand.ExecuteAsync(null);

        dialogFuel.Should().NotBeNull();
        fuelService.Verify(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
        fuelService.Verify(f => f.UpdateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
        vm.FuelRecords.Should().ContainSingle(r => r.FuelId == 88 && r.Gallons == 12.5m);
        vm.SelectedFuelRecord!.FuelId.Should().Be(88);
        vm.StatusMessage.Should().Contain("Saved fuel record #88");
    }

    [Test]
    public async Task EditFuelRecord_AfterSuccessfulDialog_DoesNotCallCreateOrUpdateAgain()
    {
        var existing = new Fuel
        {
            FuelId = 7,
            FuelDate = DateTime.SpecifyKind(new DateTime(2026, 10, 1), DateTimeKind.Utc),
            FuelLocation = "Barn",
            VehicleFueledId = 5,
            VehicleOdometerReading = 9000,
            FuelType = "Diesel",
            Gallons = 10m,
            PricePerGallon = 3m,
            TotalCost = 30m
        };

        var fuelService = new Mock<IFuelService>();
        fuelService.Setup(f => f.GetAllFuelRecordsAsync()).ReturnsAsync(new List<Fuel> { existing });

        var busService = new Mock<IBusService>();
        busService.Setup(b => b.GetAllBusesAsync()).ReturnsAsync(new List<Bus>
        {
            new()
            {
                BusId = 5,
                BusNumber = "BUS-05",
                Make = "Thomas",
                Model = "C2",
                Year = 2024,
                Status = "Active",
                SeatingCapacity = 40
            }
        });

        (bool? Result, bool RememberedNewLocation) ShowDialog(Fuel fuel)
        {
            fuel.Gallons = 15m;
            fuel.TotalCost = 45m;
            return (true, false);
        }

        var vm = new FuelManagementViewModel(
            fuelService.Object,
            busService.Object,
            showFuelDialog: ShowDialog);

        await WaitUntilLoadedAsync(vm);
        vm.SelectedFuelRecord = vm.FuelRecords[0];

        await vm.EditCommand.ExecuteAsync(null);

        fuelService.Verify(f => f.CreateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
        fuelService.Verify(f => f.UpdateFuelRecordAsync(It.IsAny<Fuel>()), Times.Never);
        vm.SelectedFuelRecord!.Gallons.Should().Be(15m);
        vm.StatusMessage.Should().Contain("Updated fuel record #7");
    }

    private static async Task WaitUntilLoadedAsync(FuelManagementViewModel vm)
    {
        var sw = Stopwatch.StartNew();
        while (!vm.StatusMessage.StartsWith("Loaded ", StringComparison.Ordinal))
        {
            if (sw.ElapsedMilliseconds > 5000)
            {
                throw new TimeoutException($"Fuel management did not finish load. Status={vm.StatusMessage}");
            }

            await Task.Delay(25);
        }
    }
}
