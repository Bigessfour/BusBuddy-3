using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Duty;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DutyLogViewModelTests
{
    private static Bus SampleBus(int id = 1) =>
        new()
        {
            BusId = id,
            BusNumber = $"BUS-{id:D2}",
            Make = "IC",
            Model = "CE",
            Year = 2022,
            Status = "Active",
            SeatingCapacity = 72
        };

    private static Driver SampleDriver(int id = 10) =>
        new()
        {
            DriverId = id,
            DriverName = $"Driver {id}",
            DriverPhone = "555-0100",
            DriversLicenceType = "CDL-B"
        };

    private static DutyEvent SampleEvent(int id = 5) =>
        new()
        {
            DutyEventId = id,
            DriverId = 10,
            BusId = 1,
            Session = DutySession.AM,
            EventType = DutyEventType.PreTrip,
            EventedAt = DateTime.UtcNow,
            Driver = SampleDriver(),
            Bus = SampleBus()
        };

    private static (
        DutyLogViewModel Vm,
        Mock<IDutyEventService> Duty,
        Mock<IBusService> Bus,
        Mock<IDriverService> Driver) Create(
        IEnumerable<DutyEvent>? events = null,
        IEnumerable<MissingPreTripRow>? missing = null)
    {
        var duty = new Mock<IDutyEventService>();
        IReadOnlyList<DutyEvent> eventList = (events ?? new List<DutyEvent> { SampleEvent() }).ToList();
        IReadOnlyList<MissingPreTripRow> missingList =
            (missing ?? new List<MissingPreTripRow>()).ToList();
        duty.Setup(d => d.GetForDateAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(eventList));
        duty.Setup(d => d.GetMissingPreTripsAsync(It.IsAny<DateTime>()))
            .Returns(Task.FromResult(missingList));
        duty.Setup(d => d.CreateAsync(It.IsAny<DutyEvent>()))
            .ReturnsAsync((DutyEvent e) =>
            {
                e.DutyEventId = 99;
                return e;
            });

        var bus = new Mock<IBusService>();
        bus.Setup(b => b.GetAllBusesAsync()).ReturnsAsync(new List<Bus> { SampleBus() });

        var driver = new Mock<IDriverService>();
        driver.Setup(d => d.GetAllDriversAsync()).ReturnsAsync(new List<Driver> { SampleDriver() });

        var vm = new DutyLogViewModel(duty.Object, bus.Object, driver.Object, (_, _) => true);
        return (vm, duty, bus, driver);
    }

    private static async Task WaitUntilLoadedAsync(DutyLogViewModel vm)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            if (!string.Equals(vm.StatusMessage, "Loading duty log...", StringComparison.Ordinal)
                && vm.StatusMessage is { Length: > 0 })
            {
                return;
            }

            if (sw.ElapsedMilliseconds > 5000)
            {
                throw new TimeoutException($"Duty VM load timed out. Status={vm.StatusMessage}");
            }

            await Task.Delay(25);
        }
    }

    [Test]
    public async Task Load_PopulatesEventsBusesDrivers()
    {
        var (vm, duty, bus, driver) = Create(
            events: new List<DutyEvent> { SampleEvent(1), SampleEvent(2) });

        await WaitUntilLoadedAsync(vm);

        vm.Events.Should().HaveCount(2);
        vm.Buses.Should().HaveCount(1);
        vm.Drivers.Should().HaveCount(1);
        duty.Verify(d => d.GetForDateAsync(It.IsAny<DateTime>()), Times.AtLeastOnce);
        duty.Verify(d => d.GetMissingPreTripsAsync(It.IsAny<DateTime>()), Times.AtLeastOnce);
        bus.Verify(b => b.GetAllBusesAsync(), Times.AtLeastOnce);
        driver.Verify(d => d.GetAllDriversAsync(), Times.AtLeastOnce);
    }

    [Test]
    public async Task Add_CallsCreateOnceThenReloads()
    {
        var (vm, duty, _, _) = Create();
        await WaitUntilLoadedAsync(vm);

        vm.DraftDriverId = 10;
        vm.DraftBusId = 1;
        vm.DraftSession = DutySession.AM;
        vm.DraftEventType = DutyEventType.Defect;
        vm.DraftNotes = "Horn weak";

        await vm.AddCommand.ExecuteAsync(null);
        await WaitUntilLoadedAsync(vm);

        duty.Verify(d => d.CreateAsync(It.Is<DutyEvent>(e =>
            e.EventType == DutyEventType.Defect
            && e.Notes == "Horn weak"
            && e.DriverId == 10
            && e.BusId == 1)), Times.Once);
        duty.Verify(d => d.GetForDateAsync(It.IsAny<DateTime>()), Times.AtLeast(2));
    }

    [Test]
    public async Task Add_StampsEventedAtOntoSelectedDate()
    {
        var (vm, duty, _, _) = Create();
        await WaitUntilLoadedAsync(vm);

        var past = DateTime.SpecifyKind(new DateTime(2026, 10, 15), DateTimeKind.Utc);
        vm.SelectedDate = past;
        await WaitUntilLoadedAsync(vm);

        vm.DraftDriverId = 10;
        vm.DraftBusId = 1;
        vm.DraftEventType = DutyEventType.PreTrip;

        await vm.AddCommand.ExecuteAsync(null);

        duty.Verify(d => d.CreateAsync(It.Is<DutyEvent>(e =>
            e.EventedAt.Date == past.Date
            && e.EventedAt.Kind == DateTimeKind.Utc)), Times.Once);
    }

    [Test]
    public void EventedAtForSelectedDate_KeepsTodayClock_UsesPastDayClock()
    {
        var now = new DateTime(2026, 11, 1, 14, 30, 0, DateTimeKind.Utc);
        var today = DutyLogViewModel.EventedAtForSelectedDate(now.Date, now);
        today.Should().Be(now);

        var pastDay = DateTime.SpecifyKind(new DateTime(2026, 10, 20), DateTimeKind.Utc);
        var stamped = DutyLogViewModel.EventedAtForSelectedDate(pastDay, now);
        stamped.Should().Be(pastDay.Add(now.TimeOfDay));
    }

    [Test]
    public async Task Add_RejectsNonNumericOdometer()
    {
        var (vm, duty, _, _) = Create();
        await WaitUntilLoadedAsync(vm);

        vm.DraftDriverId = 10;
        vm.DraftBusId = 1;
        vm.DraftOdometerText = "abc";

        await vm.AddCommand.ExecuteAsync(null);

        duty.Verify(d => d.CreateAsync(It.IsAny<DutyEvent>()), Times.Never);
        vm.StatusMessage.Should().Contain("Odometer");
    }
}
