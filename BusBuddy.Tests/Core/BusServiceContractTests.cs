using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class BusServiceContractTests
{
    [Test]
    public async Task AddBusAsync_DuplicateNumber_Throws()
    {
        var (buses, _) = Create();
        await buses.AddBusAsync(NewBus("12", "VIN-12", "LIC-12"));

        try
        {
            await buses.AddBusAsync(NewBus("12", "VIN-12B", "LIC-12B"));
            Assert.Fail("Expected a duplicate bus number to be rejected.");
        }
        catch (InvalidOperationException ex)
        {
            Assert.That(ex.Message, Does.Contain("already in the fleet"));
        }
    }

    [Test]
    public async Task GetHomeRouteAsync_ReadsRouteVehicleFk_DoesNotWriteIt()
    {
        var (buses, factory) = Create();
        var bus = await buses.AddBusAsync(NewBus("5", "VIN-5", "LIC-5"));
        int routeId;
        using (var db = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "AM Special",
                Date = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                AMVehicleId = bus.BusId,
                BusNumber = bus.BusNumber
            };
            db.Routes.Add(route);
            await db.SaveChangesAsync();
            routeId = route.RouteId;
        }

        var home = await buses.GetHomeRouteAsync(bus.BusId);

        Assert.That(home, Is.Not.Null);
        Assert.That(home!.RouteId, Is.EqualTo(routeId));
        Assert.That(home.Slot, Is.EqualTo(RouteTimeSlot.AM));
        using var check = factory.CreateDbContext();
        Assert.That(await check.Routes.Select(r => r.AMVehicleId).SingleAsync(), Is.EqualTo(bus.BusId));
    }

    [Test]
    public async Task GetSessionLoadAsync_CountsSeatsAndWheelchairs()
    {
        var (buses, factory) = Create();
        var bus = await buses.AddBusAsync(new Bus
        {
            BusNumber = "5",
            Year = 2021,
            Make = "Thomas",
            Model = "HDX",
            SeatingCapacity = 2,
            WheelchairStations = 1,
            HasLift = true,
            FleetType = "Special Needs",
            VINNumber = "VIN-SN",
            LicenseNumber = "LIC-SN",
            Status = "Active"
        });
        Assert.That(bus.VehicleKind, Is.EqualTo(BusVehicleKind.SpecialNeeds));

        int routeId;
        using (var db = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "SN AM",
                Date = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                Session = RouteSession.AM,
                AMVehicleId = bus.BusId
            };
            db.Routes.Add(route);
            await db.SaveChangesAsync();
            routeId = route.RouteId;
            db.Students.AddRange(
                Rider("A", routeId, chair: true),
                Rider("B", routeId, chair: true),
                Rider("C", routeId, chair: false));
            await db.SaveChangesAsync();
        }

        var load = await buses.GetSessionLoadAsync(bus.BusId, routeId, RouteTimeSlot.AM);

        Assert.That(load.IsSuccess, Is.True, load.Error);
        Assert.That(load.Value!.SeatedUsed, Is.EqualTo(3));
        Assert.That(load.Value.WheelchairUsed, Is.EqualTo(2));
        Assert.That(load.Value.OverflowWarning, Does.Contain("Seating overflow"));
        Assert.That(load.Value.OverflowWarning, Does.Contain("Wheelchair overflow"));
    }

    [Test]
    public async Task ConfirmTrip_BlocksPlannedHeadcount_AndLeavesHomeRoute()
    {
        var (buses, factory) = Create();
        var bus = await buses.AddBusAsync(NewBus("5", "VIN-5C", "LIC-5C", seats: 1));
        var trips = new TripEventService(factory);
        int routeId;
        int tripId;
        using (var db = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "Home AM",
                Date = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                AMVehicleId = bus.BusId,
                BusNumber = "5"
            };
            var dest = new Destination
            {
                Name = "Gym",
                Address = "1 Main",
                City = "Wiley",
                State = "CO",
                ZipCode = "81092",
                Latitude = 38.1m,
                Longitude = -102.7m,
                DestinationType = DestinationTypes.TripDestination
            };
            var driver = new Driver { DriverName = "Pat", DriversLicenceType = "B" };
            db.Routes.Add(route);
            db.Destinations.Add(dest);
            db.Drivers.Add(driver);
            await db.SaveChangesAsync();
            routeId = route.RouteId;
            db.Students.Add(Rider("One", routeId, chair: false));
            db.Students.Add(Rider("Two", routeId, chair: false));
            var trip = new TripEvent
            {
                TripDate = new DateTime(2026, 9, 22),
                PickupTime = TimeSpan.FromHours(8),
                ReturnClockTime = TimeSpan.FromHours(18),
                DestinationName = dest.Name,
                DestinationLocationId = dest.DestinationId,
                DriverId = driver.DriverId,
                VehicleId = bus.BusId,
                RouteId = routeId,
                PlannedHeadcount = 10,
                Status = TripStatus.Assigned
            };
            db.TripEvents.Add(trip);
            await db.SaveChangesAsync();
            tripId = trip.TripEventId;
        }

        var confirmed = await trips.ConfirmTripAsync(tripId);

        Assert.That(confirmed.IsFailure, Is.True);
        Assert.That(confirmed.Error, Does.Contain("10 planned"));
        Assert.That(confirmed.Error, Does.Not.Contain("riding"));
        using var check = factory.CreateDbContext();
        Assert.That(await check.Routes.Where(r => r.RouteId == routeId).Select(r => r.AMVehicleId).SingleAsync(), Is.EqualTo(bus.BusId));
        Assert.That(await check.TripEvents.Where(t => t.TripEventId == tripId).Select(t => t.VehicleId).SingleAsync(), Is.EqualTo(bus.BusId));
        Assert.That(await check.TripEvents.Where(t => t.TripEventId == tripId).Select(t => t.Status).SingleAsync(), Is.EqualTo(TripStatus.Assigned));
    }

    [Test]
    public async Task ConfirmTrip_RosterOverflow_WarnsAndLeavesHomeRoute()
    {
        var (buses, factory) = Create();
        var bus = await buses.AddBusAsync(NewBus("8", "VIN-8C", "LIC-8C", seats: 2));
        var trips = new TripEventService(factory, buses: buses);
        int routeId;
        int tripId;
        using (var db = factory.CreateWriteDbContext())
        {
            var route = new Route
            {
                RouteName = "Home AM 8",
                Date = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                AMVehicleId = bus.BusId,
                BusNumber = "8"
            };
            var dest = new Destination
            {
                Name = "Field",
                Address = "2 Main",
                City = "Wiley",
                State = "CO",
                ZipCode = "81092",
                Latitude = 38.1m,
                Longitude = -102.7m,
                DestinationType = DestinationTypes.TripDestination
            };
            var driver = new Driver { DriverName = "Sam", DriversLicenceType = "B" };
            db.Routes.Add(route);
            db.Destinations.Add(dest);
            db.Drivers.Add(driver);
            await db.SaveChangesAsync();
            routeId = route.RouteId;
            db.Students.Add(Rider("One", routeId, chair: false));
            db.Students.Add(Rider("Two", routeId, chair: true));
            db.Students.Add(Rider("Three", routeId, chair: true));
            var trip = new TripEvent
            {
                TripDate = new DateTime(2026, 9, 22),
                PickupTime = TimeSpan.FromHours(8),
                ReturnClockTime = TimeSpan.FromHours(11),
                DestinationName = dest.Name,
                DestinationLocationId = dest.DestinationId,
                DriverId = driver.DriverId,
                VehicleId = bus.BusId,
                RouteId = routeId,
                PlannedHeadcount = 2,
                Status = TripStatus.Assigned
            };
            db.TripEvents.Add(trip);
            await db.SaveChangesAsync();
            tripId = trip.TripEventId;
        }

        var confirmed = await trips.ConfirmTripAsync(tripId);

        Assert.That(confirmed.IsFailure, Is.True);
        Assert.That(confirmed.Error, Does.Contain("overflow").IgnoreCase);
        using var check = factory.CreateDbContext();
        Assert.That(await check.Routes.Where(r => r.RouteId == routeId).Select(r => r.AMVehicleId).SingleAsync(), Is.EqualTo(bus.BusId));
        Assert.That(await check.TripEvents.Where(t => t.TripEventId == tripId).Select(t => t.Status).SingleAsync(), Is.EqualTo(TripStatus.Assigned));
    }

    [Test]
    public void IsAvailable_IncludesInService_AndExcludesOutOfService()
    {
        Assert.That(new Bus { Status = "InService" }.IsAvailable, Is.True);
        Assert.That(new Bus { Status = "Out of Service" }.IsAvailable, Is.False);
        Assert.That(new Bus { Status = "Active" }.IsAvailable, Is.True);
    }

    private static (BusService Buses, TestDbContextFactory Factory) Create()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(options);
        var cache = new BusCachingService(new MemoryCache(new MemoryCacheOptions()));
        return (new BusService(factory, cache), factory);
    }

    private static Bus NewBus(string number, string vin, string plate, int seats = 40) =>
        new()
        {
            BusNumber = number,
            Year = 2020,
            Make = "Blue Bird",
            Model = "Vision",
            SeatingCapacity = seats,
            VINNumber = vin,
            LicenseNumber = plate,
            Status = "Active",
            FleetType = "Regular"
        };

    private static Student Rider(string name, int routeId, bool chair) =>
        new()
        {
            StudentName = name,
            Grade = "4",
            School = "Test",
            ParentGuardian = "P",
            EmergencyPhone = "555-0100",
            Active = true,
            RidesAm = true,
            RidesPm = true,
            AmRouteId = routeId,
            RequiresWheelchair = chair
        };
}
