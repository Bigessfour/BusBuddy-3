using System;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class LocationContractTests
{
    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext()
        {
            var ctx = new BusBuddyDbContext(_options);
            ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            return ctx;
        }
    }

    private static TestDbContextFactory CreateFactory()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        return new TestDbContextFactory(
            new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"Loc_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
    }

    [Test]
    public void LocationTypes_AreTheSevenSpecTypes_AndTripIsNotAType()
    {
        Assert.That(LocationTypes.All, Is.EquivalentTo(new[]
        {
            LocationTypes.School,
            LocationTypes.PickupStop,
            LocationTypes.StudentHome,
            LocationTypes.Depot,
            LocationTypes.Maintenance,
            LocationTypes.Fuel,
            LocationTypes.TripDestination
        }));
        Assert.That(LocationTypes.All, Does.Not.Contain("Trip"));
        Assert.That(LocationTypes.FromDestinationType(DestinationTypes.FieldTrip), Is.EqualTo(LocationTypes.TripDestination));
        Assert.That(LocationTypes.FromDestinationType(DestinationTypes.School), Is.EqualTo(LocationTypes.School));
        Assert.That(LocationTypes.IsSchoolYearStable(DestinationTypes.School), Is.True);
        Assert.That(LocationTypes.IsSchoolYearStable(DestinationTypes.TripDestination), Is.False);
    }

    [Test]
    public void UnresolvedBoardNames_AreNotPlaces()
    {
        Assert.That(LocationTypes.IsUnresolvedPlaceName("Denver - See Trip Notes"), Is.True);
        Assert.That(LocationTypes.IsUnresolvedPlaceName("TBD"), Is.True);
        Assert.That(LocationTypes.IsUnresolvedPlaceName("Strasburg HS"), Is.False);
        Assert.That(LocationTypes.IsUnresolvedPlaceName("6634 Country Road HH"), Is.False);

        var trip = new TripEvent
        {
            DestinationName = "Denver - See Trip Notes",
            PickupTime = TimeSpan.FromHours(11),
            PlannedHeadcount = 45
        };
        Assert.That(trip.HasPlace, Is.False);
        Assert.That(TripEvent.InferBoardStatus(trip), Is.EqualTo(TripStatus.MissingInfo));
    }

    [Test]
    public void LocationCoordinate_RejectsFallbackPoints()
    {
        Assert.That(LocationCoordinate.IsValidated(0m, 0m), Is.False);
        Assert.That(LocationCoordinate.IsValidated(
            (decimal)MapDefaults.UnconfiguredLatitude,
            (decimal)MapDefaults.UnconfiguredLongitude), Is.False);
        Assert.That(LocationCoordinate.IsValidated(38.0872m, -102.6208m), Is.True);
        Assert.That(LocationTypes.ValidationStatus(false), Is.EqualTo("needs validation"));
        Assert.That(LocationCoordinate.IsPlotPrecision("PREMISE"), Is.True);
        Assert.That(LocationCoordinate.IsPlotPrecision("SUB_PREMISE"), Is.True);
        Assert.That(LocationCoordinate.IsPlotPrecision("PREMISE_PROXIMITY"), Is.True);
        Assert.That(LocationCoordinate.IsPlotPrecision("ROOFTOP"), Is.True);
        Assert.That(LocationCoordinate.IsPlotPrecision("RANGE_INTERPOLATED"), Is.False);
        Assert.That(LocationCoordinate.IsPlotPrecision("OTHER"), Is.False);
        Assert.That(LocationCoordinate.IsPlotPrecision("ROUTE"), Is.False);
        Assert.That(LocationCoordinate.IsPlotPrecision("APPROXIMATE"), Is.False);
        Assert.That(LocationCoordinate.IsPlotPrecision("GEOMETRIC_CENTER"), Is.False);
        Assert.That(LocationCoordinate.IsPlotPrecision(null), Is.False);
    }

    [Test]
    public void DistrictDepot_RejectsUnvalidatedCoordinates()
    {
        Assert.That(DistrictDepot.IsConfigured(new BusBuddy.Core.Configuration.RoutingDistrictSettings
        {
            DepotLatitude = 0,
            DepotLongitude = 0
        }), Is.False);
        Assert.That(DistrictDepot.IsConfigured(new BusBuddy.Core.Configuration.RoutingDistrictSettings
        {
            DepotLatitude = MapDefaults.UnconfiguredLatitude,
            DepotLongitude = MapDefaults.UnconfiguredLongitude
        }), Is.False);
        Assert.That(DistrictDepot.IsConfigured(new BusBuddy.Core.Configuration.RoutingDistrictSettings
        {
            DepotLatitude = 38.0866,
            DepotLongitude = -102.6201
        }), Is.True);
    }

    [Test]
    public void StudentHome_IsNotACatalogStop()
    {
        var homeOnly = new Student { Latitude = 38.08m, Longitude = -102.62m };
        Assert.That(homeOnly.PickupMode, Is.EqualTo(LocationTypes.PickupModeHome));
        Assert.That(homeOnly.LocationType, Is.EqualTo(LocationTypes.StudentHome));
        Assert.That(PickupStopTypes.All, Does.Not.Contain(PickupStopTypes.RuralHome));

        var catalog = new Student { PickupStopId = 7 };
        Assert.That(catalog.PickupMode, Is.EqualTo(LocationTypes.PickupModeCatalogStop));
    }

    [Test]
    public void StudentPlot_SkipsUnvalidatedHome()
    {
        var student = new Student { Latitude = 0m, Longitude = 0m, HomeAddress = "1 Main" };
        Assert.That(StudentPlotLocation.PinsFromStored(student, null), Is.Empty);

        var centroid = new Student
        {
            Latitude = (decimal)MapDefaults.UnconfiguredLatitude,
            Longitude = (decimal)MapDefaults.UnconfiguredLongitude
        };
        Assert.That(StudentPlotLocation.PinsFromStored(centroid, null), Is.Empty);
    }

    [Test]
    public async Task AddSchool_DropsUnvalidatedGps()
    {
        var sut = new DestinationService(CreateFactory());
        var school = await sut.AddSchoolAsync(
            "Wiley School",
            "1105 Parkview",
            "Wiley",
            "CO",
            "81092",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15),
            latitude: 0m,
            longitude: 0m);

        Assert.That(school.Latitude, Is.Null);
        Assert.That(school.HasValidatedCoordinates, Is.False);
        Assert.That(school.CoordinateStatus, Is.EqualTo("needs validation"));
        Assert.That(school.LocationType, Is.EqualTo(LocationTypes.School));
    }

    [Test]
    public async Task AddTripDestination_RequiresValidatedCoords_AndRejectsSeeTripNotes()
    {
        var sut = new DestinationService(CreateFactory());
        Assert.ThrowsAsync<ArgumentException>((Func<Task>)(() => sut.AddTripDestinationAsync(
            "Denver - See Trip Notes", "x", "Denver", "CO", "80202", 39.7m, -104.9m)));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(() => sut.AddTripDestinationAsync(
            "Strasburg HS", "1 Main", "Strasburg", "CO", "80136", 0m, 0m)));

        var place = await sut.AddTripDestinationAsync(
            "Strasburg HS", "1 Main", "Strasburg", "CO", "80136", 39.74m, -104.32m);
        Assert.That(place.DestinationType, Is.EqualTo(DestinationTypes.TripDestination));
        Assert.That(place.LocationType, Is.EqualTo(LocationTypes.TripDestination));
        Assert.That(place.SchoolYearStable, Is.False);
        Assert.That(place.HasValidatedCoordinates, Is.True);

        var found = await sut.FindValidatedPlaceByNameAsync("Strasburg HS");
        Assert.That(found, Is.Not.Null);
        Assert.That(await sut.FindValidatedPlaceByNameAsync("See Trip Notes"), Is.Null);
    }

    [Test]
    public void AddPickupStop_RejectsFallbackCoordinates()
    {
        var sut = new PickupStopService(CreateFactory());
        Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(() =>
            sut.AddStopAsync("Oak", null, 0m, 0m)));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(() =>
            sut.AddStopAsync(
                "Oak",
                null,
                (decimal)MapDefaults.UnconfiguredLatitude,
                (decimal)MapDefaults.UnconfiguredLongitude)));
    }

    [Test]
    public async Task Import_AttachesValidatedSchool_NotAStringOnRoute()
    {
        var factory = CreateFactory();
        await using (var db = factory.CreateWriteDbContext())
        {
            db.Destinations.Add(new Destination
            {
                Name = "Strasburg HS",
                Address = "1 Main",
                City = "Strasburg",
                State = "CO",
                ZipCode = "80136",
                DestinationType = DestinationTypes.School,
                Latitude = 39.74m,
                Longitude = -104.32m,
                IsActive = true
            });
            db.Buses.Add(new Bus
            {
                BusNumber = "25", Year = 2020, Make = "IC", Model = "CE",
                SeatingCapacity = 38, VINNumber = "VIN25", LicenseNumber = "L25", Status = "Active"
            });
            db.Drivers.Add(new Driver { DriverName = "Elby Sneller", DriversLicenceType = "B" });
            await db.SaveChangesAsync();
        }

        var csv = """
            Date,School,Team / School,Location,Departs,Returns,Ticket #,PAX,Rode,Miles,Driver,Bus #,,
            "Sat, 5 Sep ",HS,Volleyball,Strasburg HS,6:00 AM,11:00 PM,319098871,25,,338,Elby Sneller,25,,Sep 2026
            "Thu, 10 Sep ",HS,Soccer,Denver - See Trip Notes,11:00 AM,10:00 PM,319098879,45,,417,,25,,Sep 2026
            """;
        var result = await new TripEventService(factory).ImportBoardCsvAsync(csv);
        Assert.That(result.Created, Is.EqualTo(2));

        var trips = (await new TripEventService(factory).GetAllTripsAsync()).ToList();
        var strasburg = trips.Single(t => t.ExternalTicketNo == "319098871");
        Assert.That(strasburg.RouteId, Is.Null);
        Assert.That(strasburg.DestinationName, Is.EqualTo("Strasburg HS"));
        Assert.That(strasburg.DestinationLocationId, Is.Not.Null);
        Assert.That(strasburg.HasValidatedDestination, Is.True);

        var notes = trips.Single(t => t.ExternalTicketNo == "319098879");
        Assert.That(notes.DestinationName, Is.EqualTo("Denver - See Trip Notes"));
        Assert.That(notes.DestinationLocationId, Is.Null);
        Assert.That(notes.Status, Is.EqualTo(TripStatus.MissingInfo));
    }
}
