using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DutyEventServiceTests : IDisposable
{
    private DbContextOptions<BusBuddyDbContext> _options = null!;
    private DutyEventService _service = null!;
    private bool _disposed;

    [SetUp]
    public void Setup()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        _options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var seed = new BusBuddyDbContext(_options);
        seed.Buses.Add(new Bus
        {
            BusId = 1,
            BusNumber = "001",
            Year = 2020,
            Make = "Test",
            Model = "Bus",
            SeatingCapacity = 50,
            Status = "Active",
            VINNumber = "1TESTVIN000000001",
            LicenseNumber = "BUS-001"
        });
        seed.Buses.Add(new Bus
        {
            BusId = 2,
            BusNumber = "002",
            Year = 2021,
            Make = "Test",
            Model = "Bus",
            SeatingCapacity = 50,
            Status = "Active",
            VINNumber = "1TESTVIN000000002",
            LicenseNumber = "BUS-002"
        });
        seed.Drivers.Add(new Driver
        {
            DriverId = 10,
            DriverName = "Ada Driver",
            DriverPhone = "555-0100",
            DriversLicenceType = "CDL-B"
        });
        seed.Drivers.Add(new Driver
        {
            DriverId = 11,
            DriverName = "Ben Driver",
            DriverPhone = "555-0101",
            DriversLicenceType = "CDL-B"
        });
        seed.Routes.Add(new Route
        {
            RouteId = 100,
            RouteName = "AM Special",
            Date = DateTime.SpecifyKind(new DateTime(2026, 11, 1), DateTimeKind.Utc),
            IsActive = true,
            AMVehicleId = 1,
            AMDriverId = 10,
            PMVehicleId = 2,
            PMDriverId = 11,
            AMBeginTime = TimeSpan.FromHours(7),
            PMBeginTime = TimeSpan.FromHours(14)
        });
        seed.Database.EnsureCreated();
        seed.SaveChanges();

        _service = new DutyEventService(new InMemoryContextFactory(_options));
    }

    [TearDown]
    public void TearDown() => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private sealed class InMemoryContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public InMemoryContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext() => new(_options);
    }

    private static DutyEvent Draft(
        DutyEventType type = DutyEventType.PreTrip,
        DutySession session = DutySession.AM,
        int busId = 1,
        int driverId = 10,
        string? notes = null) =>
        new()
        {
            DriverId = driverId,
            BusId = busId,
            Session = session,
            EventType = type,
            EventedAt = DateTime.SpecifyKind(new DateTime(2026, 11, 1, 6, 30, 0), DateTimeKind.Utc),
            Notes = notes
        };

    [Test]
    public async Task CreateAsync_PreTripAndPostTrip_Persist()
    {
        var pre = await _service.CreateAsync(Draft(DutyEventType.PreTrip));
        var post = await _service.CreateAsync(Draft(DutyEventType.PostTrip, DutySession.AM));

        Assert.That(pre.DutyEventId, Is.GreaterThan(0));
        Assert.That(post.DutyEventId, Is.GreaterThan(0));

        var day = await _service.GetForDateAsync(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(day.Count, Is.EqualTo(2));
        Assert.That(day.Any(d => d.EventType == DutyEventType.PreTrip), Is.True);
        Assert.That(day.Any(d => d.EventType == DutyEventType.PostTrip), Is.True);
    }

    [Test]
    public async Task CreateAsync_Defect_CreatesMaintenanceWithSourceDutyEventId()
    {
        var defect = await _service.CreateAsync(
            Draft(DutyEventType.Defect, notes: "Left mirror cracked"));

        using var db = new BusBuddyDbContext(_options);
        var maintenance = db.MaintenanceRecords.Single(m => m.SourceDutyEventId == defect.DutyEventId);
        Assert.That(maintenance.MaintenanceCompleted, Is.EqualTo(DutyEventService.DefectWorkLabel));
        Assert.That(maintenance.Vendor, Is.EqualTo(DutyEventService.DefectVendorLabel));
        Assert.That(maintenance.Status, Is.EqualTo("Scheduled"));
        Assert.That(maintenance.Priority, Is.EqualTo("High"));
        Assert.That(maintenance.Description, Is.EqualTo("Left mirror cracked"));
        Assert.That(maintenance.VehicleId, Is.EqualTo(1));

        var bus = db.Buses.Single(b => b.BusId == 1);
        Assert.That(bus.Status, Is.EqualTo("Active"), "Defect must not auto-park the bus");
    }

    [Test]
    public async Task CreateAsync_FuelType_DoesNotInsertFuelRow()
    {
        var before = 0;
        using (var db = new BusBuddyDbContext(_options))
        {
            before = db.FuelRecords.Count();
        }

        var fuelDuty = await _service.CreateAsync(
            Draft(DutyEventType.Fuel, notes: "Fueled at barn"));

        using var afterDb = new BusBuddyDbContext(_options);
        Assert.That(afterDb.FuelRecords.Count(), Is.EqualTo(before));
        Assert.That(afterDb.DutyEvents.Count(d => d.DutyEventId == fuelDuty.DutyEventId), Is.EqualTo(1));
        Assert.That(fuelDuty.FuelId, Is.Null);
    }

    [Test]
    public async Task GetByBusAndDriverOnDateAsync_FiltersDay()
    {
        await _service.CreateAsync(Draft());
        await _service.CreateAsync(new DutyEvent
        {
            DriverId = 10,
            BusId = 1,
            Session = DutySession.PM,
            EventType = DutyEventType.PreTrip,
            EventedAt = DateTime.SpecifyKind(new DateTime(2026, 11, 2, 6, 0, 0), DateTimeKind.Utc)
        });

        var rows = await _service.GetByBusAndDriverOnDateAsync(
            1, 10, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(rows.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetMissingPreTripsAsync_ListsSessionsWithoutPreTrip()
    {
        var day = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var missingBefore = await _service.GetMissingPreTripsAsync(day);
        Assert.That(missingBefore.Any(m => m.Session == DutySession.AM && m.BusId == 1), Is.True);
        Assert.That(missingBefore.Any(m => m.Session == DutySession.PM && m.BusId == 2), Is.True);

        await _service.CreateAsync(Draft(DutyEventType.PreTrip, DutySession.AM, busId: 1, driverId: 10));

        var missingAfter = await _service.GetMissingPreTripsAsync(day);
        Assert.That(missingAfter.Any(m => m.Session == DutySession.AM && m.BusId == 1), Is.False);
        Assert.That(missingAfter.Any(m => m.Session == DutySession.PM && m.BusId == 2), Is.True);
    }

    [Test]
    public async Task CreateAsync_Defect_WritesDutyAndMaintenanceTogether()
    {
        var defect = await _service.CreateAsync(
            Draft(DutyEventType.Defect, notes: "Stop arm bent"));

        using var db = new BusBuddyDbContext(_options);
        Assert.That(db.DutyEvents.Count(d => d.DutyEventId == defect.DutyEventId), Is.EqualTo(1));
        Assert.That(
            db.MaintenanceRecords.Count(m => m.SourceDutyEventId == defect.DutyEventId),
            Is.EqualTo(1));
    }
}
