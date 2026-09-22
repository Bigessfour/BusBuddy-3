using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Fuel;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Behavioral proof for the fuel reconciliation surface: vehicle gallons come from records,
/// bulk gallons only from the clerk, and the discrepancy is never invented.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class FuelReconciliationViewModelTests
{
    private const string LoadingText = "Loading reconciliation data...";
    private static readonly DateTime Day1 = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc);

    private static List<Fuel> SampleRecords() =>
    [
        new() { FuelId = 1, FuelDate = Day1, Gallons = 10m, VehicleFueledId = 1, FuelLocation = "Barn", VehicleOdometerReading = 1000 },
        new() { FuelId = 2, FuelDate = Day1, Gallons = 5.5m, VehicleFueledId = 2, FuelLocation = "Co-op", VehicleOdometerReading = 2000 },
        new() { FuelId = 3, FuelDate = Day2, Gallons = 20m, VehicleFueledId = 1, FuelLocation = "Barn", VehicleOdometerReading = 1100 },
    ];

    private static List<Bus> SampleBuses() =>
    [
        new() { BusId = 1, BusNumber = "05" },
        new() { BusId = 2, BusNumber = "12" },
    ];

    private static (FuelReconciliationViewModel Vm, Mock<IFuelService> Fuel, Mock<IBusService> Bus) Create(
        IEnumerable<Fuel>? records = null,
        IReadOnlyList<string>? locations = null)
    {
        var fuel = new Mock<IFuelService>();
        fuel.Setup(f => f.GetFuelRecordsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(records ?? SampleRecords());
        fuel.Setup(f => f.GetDistinctFuelLocationsAsync())
            .ReturnsAsync(locations ?? new List<string> { "Barn", "Co-op" });

        var bus = new Mock<IBusService>();
        bus.Setup(b => b.GetAllBusesAsync()).ReturnsAsync(SampleBuses());

        var vm = new FuelReconciliationViewModel(fuel.Object, bus.Object);
        return (vm, fuel, bus);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!predicate())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("View model did not reach the expected state in time.");
            }

            await Task.Delay(25);
        }
    }

    private static Task WaitForInitialLoadAsync(FuelReconciliationViewModel vm) =>
        WaitUntilAsync(() => !string.Equals(vm.ReconciliationSummary, LoadingText, StringComparison.Ordinal));

    [Test]
    public async Task InitialLoad_WithoutBulkReading_SumsVehicleGallonsPerDay()
    {
        var (vm, _, _) = Create();
        using var _ = vm;

        await WaitForInitialLoadAsync(vm);

        vm.HasBulkReading.Should().BeFalse();
        vm.VehicleUsageGallons.Should().BeApproximately(35.5, 0.001);
        vm.DiscrepancyGallons.Should().Be(0);
        vm.DailyReconciliation.Should().HaveCount(2);
        vm.DailyReconciliation[0].Date.Should().Be(Day1.Date);
        vm.DailyReconciliation[0].VehicleUsageGallons.Should().BeApproximately(15.5, 0.001);
        vm.DailyReconciliation[0].FillCount.Should().Be(2);
        vm.DailyReconciliation[1].VehicleUsageGallons.Should().BeApproximately(20, 0.001);
        vm.DiscrepancyDetails.Should().BeEmpty();
        vm.HasDailyChartData.Should().BeTrue();
        vm.ReconciliationSummary.Should().Contain("3 vehicle fill-up(s)");
        vm.ReconciliationSummary.Should().Contain("Enter the bulk station meter gallons");
    }

    [Test]
    public async Task InitialLoad_PopulatesLocationsFromServiceBehindAllLocations()
    {
        var (vm, fuel, _) = Create();
        using var _ = vm;

        await WaitForInitialLoadAsync(vm);

        vm.FuelLocations[0].Should().Be("All Locations");
        vm.FuelLocations.Should().Contain("Barn").And.Contain("Co-op");
        vm.SelectedLocation.Should().Be("All Locations");
        fuel.Verify(f => f.GetDistinctFuelLocationsAsync(), Times.Once);
    }

    [Test]
    public async Task BulkReading_ComputesDiscrepancyAndListsFillUpsToVerify()
    {
        var (vm, _, _) = Create();
        using var _ = vm;
        await WaitForInitialLoadAsync(vm);

        vm.BulkStationGallonsText = "40";
        await WaitUntilAsync(() => vm.HasBulkReading && vm.ReconciliationSummary.Contains("versus the station meter", StringComparison.Ordinal));

        vm.BulkStationGallons.Should().BeApproximately(40, 0.001);
        vm.DiscrepancyGallons.Should().BeApproximately(4.5, 0.001);
        vm.DiscrepancyPercentage.Should().BeApproximately(4.5 / 35.5, 0.0001);
        vm.ReconciliationSummary.Should().Contain("bulk surplus").And.Contain("requires investigation");

        // > 3% variance: every fill-up in the period is listed with the bus number from the fleet lookup.
        vm.DiscrepancyDetails.Should().HaveCount(3);
        vm.DiscrepancyDetails.Select(d => d.BusNumber).Should().BeEquivalentTo("05", "12", "05");
        vm.DiscrepancyDetails.Should().OnlyContain(d => d.DiscrepancyType == "Bulk surplus");
        vm.DiscrepancyDetails.First().OdometerReading.Should().Be(1000);
    }

    [Test]
    public async Task BulkReading_WithinTolerance_NoDetailsAndToleranceMessage()
    {
        var (vm, _, _) = Create();
        using var _ = vm;
        await WaitForInitialLoadAsync(vm);

        vm.BulkStationGallonsText = "36"; // 1.4% over 35.5
        await WaitUntilAsync(() => vm.HasBulkReading && vm.ReconciliationSummary.Contains("versus the station meter", StringComparison.Ordinal));

        vm.DiscrepancyDetails.Should().BeEmpty();
        vm.ReconciliationSummary.Should().Contain("within a typical 5% tolerance");
    }

    [Test]
    public async Task SelectedLocation_FiltersRecordsToThatStation()
    {
        var (vm, _, _) = Create();
        using var _ = vm;
        await WaitForInitialLoadAsync(vm);

        vm.SelectedLocation = "Co-op";
        await WaitUntilAsync(() => vm.ReconciliationSummary.Contains("1 vehicle fill-up(s)", StringComparison.Ordinal));

        vm.VehicleUsageGallons.Should().BeApproximately(5.5, 0.001);
        vm.DailyReconciliation.Should().HaveCount(1);
        vm.DailyReconciliation[0].FillCount.Should().Be(1);
    }

    [Test]
    public async Task EndDateBeforeStartDate_ShowsValidationSummaryWithoutQuerying()
    {
        var (vm, fuel, _) = Create();
        using var _ = vm;
        await WaitForInitialLoadAsync(vm);
        fuel.Invocations.Clear();

        vm.EndDate = vm.StartDate.AddDays(-1);
        await WaitUntilAsync(() => vm.ReconciliationSummary.StartsWith("End date must be on or after", StringComparison.Ordinal));

        fuel.Verify(f => f.GetFuelRecordsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Test]
    public async Task NoRecords_ShowsEmptyPeriodMessage()
    {
        var (vm, _, _) = Create(records: new List<Fuel>());
        using var _ = vm;

        await WaitForInitialLoadAsync(vm);

        vm.ReconciliationSummary.Should().Be("No fuel data available for the selected period and location.");
        vm.HasDailyChartData.Should().BeFalse();
        vm.VehicleUsageGallons.Should().Be(0);
    }

    [Test]
    public async Task RememberBulkCommand_CanExecuteOnlyForNonNegativeNumber()
    {
        var (vm, _, _) = Create();
        using var _ = vm;
        await WaitForInitialLoadAsync(vm);

        vm.RememberBulkCommand.CanExecute(null).Should().BeFalse("empty text is not a meter reading");

        vm.BulkStationGallonsText = "abc";
        vm.RememberBulkCommand.CanExecute(null).Should().BeFalse();

        vm.BulkStationGallonsText = "-5";
        vm.RememberBulkCommand.CanExecute(null).Should().BeFalse();

        vm.BulkStationGallonsText = "120.5";
        vm.RememberBulkCommand.CanExecute(null).Should().BeTrue();
    }
}
