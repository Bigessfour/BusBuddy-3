using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.Trips;
using BusBuddy.Tests.WPF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class TripBoardImportTests
{
    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;
        public BusBuddyDbContext CreateDbContext() => new(_options);
        public BusBuddyDbContext CreateWriteDbContext() => new(_options);
    }

    private static DbContextOptions<BusBuddyDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"TripBoard_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private const string Header =
        "Date,School,Team / School,Location,Departs,Returns,Ticket #,PAX,Rode,Miles,Driver,Bus #,,";

    private const string FleetHeader =
        """
         Sep ,,Jeff Alexander,0,Bus,Seats,Status,,Bus,Seats,Status,,,
        ,,Elby Sneller,5,22,48,Out of Service,,55,14,,,,
        ,,Rod Jagers,6,24,57,Out of Service,,57,14,,,,
        Color Code Key,,,,,,,,,,,,,
        """;

    [Test]
    public void Parser_MapsBoardColumnsAndKeepsLeaveTimeNotes()
    {
        var csv = $"""
            {Header}
            "Tue, 8 Sep ",HS,Football Game - JV,James Irwin Charter HS,12:00 PM,11:00 PM,319098876,35,,317,Steve McKitrick,25,Can we adjust leave time to 1:30.  The game time got moved back - CM,Sep 2026
            """;

        var parsed = TripBoardCsvParser.Parse(csv);
        Assert.That(parsed.Rows, Has.Count.EqualTo(1));
        var row = parsed.Rows[0];
        Assert.That(row.ExternalTicketNo, Is.EqualTo("319098876"));
        Assert.That(row.TripDate, Is.EqualTo(new DateTime(2026, 9, 8)));
        Assert.That(row.RequestingSchool, Is.EqualTo("HS"));
        Assert.That(row.GroupOrActivity, Is.EqualTo("Football Game - JV"));
        Assert.That(row.DestinationName, Is.EqualTo("James Irwin Charter HS"));
        Assert.That(row.PickupTime, Is.EqualTo(TimeSpan.FromHours(12)));
        Assert.That(row.ReturnClockTime, Is.EqualTo(new TimeSpan(23, 0, 0)));
        Assert.That(row.PlannedHeadcount, Is.EqualTo(35));
        Assert.That(row.PlannedMiles, Is.EqualTo(317m));
        Assert.That(row.DriverName, Is.EqualTo("Steve McKitrick"));
        Assert.That(row.BusNumber, Is.EqualTo("25"));
        Assert.That(row.Notes, Does.Contain("adjust leave time to 1:30"));
        Assert.That(row.PickupTime, Is.Not.EqualTo(new TimeSpan(13, 30, 0)));
    }

    [Test]
    public void Parser_PlayoffWithoutPlaceOrTime_IsMissingInfoInput()
    {
        var csv = $"""
            {Header}
            "Tue, 27 Oct ",HS,Soccer - V- 1st Round Playoffs,,,,319101249,45,,,,," - Possible Overnight, If we Travel. Details Pending",Oct 2026
            """;

        var row = TripBoardCsvParser.Parse(csv).Rows[0];
        Assert.That(row.ExternalTicketNo, Is.EqualTo("319101249"));
        Assert.That(row.PlannedHeadcount, Is.EqualTo(45));
        Assert.That(row.DestinationName, Is.Null);
        Assert.That(row.PickupTime, Is.Null);
        Assert.That(row.IsOvernightPending, Is.True);
        Assert.That(row.TripDate, Is.EqualTo(new DateTime(2026, 10, 27)));
    }

    [Test]
    public void Parser_Day2WithoutTicket_LinksToPriorTicket()
    {
        var csv = $"""
            {Header}
            "Thu, 10 Sep ",HS,Soccer - Boys - V,Denver - See Trip Notes,11:00 AM,10:00 PM,319098879,45,,417,,22,Day 1 at DLMK HS,Sep 2026
            "Fri, 11 Sep ",,,,,,,,,,,,Day 2 at DSST-CV,
            """;

        var parsed = TripBoardCsvParser.Parse(csv);
        Assert.That(parsed.Rows, Has.Count.EqualTo(2));
        var day2 = parsed.Rows[1];
        Assert.That(day2.IsDay2WithoutTicket, Is.True);
        Assert.That(day2.LinkedTicketNo, Is.EqualTo("319098879"));
        Assert.That(day2.ExternalTicketNo, Is.EqualTo("319098879-DAY2"));
        Assert.That(day2.TripDate, Is.EqualTo(new DateTime(2026, 9, 11)));
    }

    [Test]
    public void Parser_MultiAssetAndOutOfServiceHeader()
    {
        var csv = $"""
            {FleetHeader}
            {Header}
            "Thu, 1 Oct ",WA,Magic of Books Assembly,Lamar High School,8:00 AM,9:45 AM,319101941,220,,3,,, - all buses and SPED,Oct 2026
            """;

        var parsed = TripBoardCsvParser.Parse(csv);
        Assert.That(parsed.OutOfServiceBusNumbers, Does.Contain("22"));
        Assert.That(parsed.OutOfServiceBusNumbers, Does.Contain("24"));
        var row = parsed.Rows[0];
        Assert.That(row.IsMultiAsset, Is.True);
        Assert.That(row.BusNumber, Is.Null);
    }

    [Test]
    public void Parser_FieldTripDestinationFromNotes_AndNextMorningReturn()
    {
        var csv = $"""
            {Header}
            "Wed, 9 Sep ",HS,Field Trip for Mr. Flint,,10:00 AM,11:15 AM,319100103,20,,,,,- Destination: 6634 Country Road HH .5,Sep 2026
            "Fri, 11 Sep ",HS,Football - V,Woodland Park HS,1:30 PM,1:00 AM,319098881,40,,354,Rod Jagers,24,,Sep 2026
            """;

        var parsed = TripBoardCsvParser.Parse(csv);
        Assert.That(parsed.Rows[0].DestinationName, Is.EqualTo("6634 Country Road HH"));
        Assert.That(parsed.Rows[1].ReturnIsNextDay, Is.True);
        Assert.That(parsed.Rows[1].ReturnClockTime, Is.EqualTo(new TimeSpan(1, 0, 0)));
    }

    [Test]
    public async Task Import_UpsertsByTicket_MissingInfo_OosWarning_NoRouteMerge()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        await SeedFleetAsync(factory);
        var service = new TripEventService(factory);

        var csv = $"""
            {FleetHeader}
            {Header}
            "Sat, 5 Sep ",HS,Volleyball Tournament - Girls - JV,Strasburg HS,6:00 AM,11:00 PM,319098871,25,,338,Elby Sneller,25,,Sep 2026
            "Tue, 27 Oct ",HS,Soccer - V- 1st Round Playoffs,,,,319101249,45,,,,," - Possible Overnight, If we Travel. Details Pending",Oct 2026
            "Fri, 11 Sep ",HS,Football - V,Woodland Park HS,1:30 PM,1:00 AM,319098881,40,,354,Rod Jagers,24,,Sep 2026
            "Thu, 1 Oct ",WA,Magic of Books Assembly,Lamar High School,8:00 AM,9:45 AM,319101941,220,,3,,, - all buses and SPED,Oct 2026
            "Tue, 8 Sep ",HS,Football Game - JV,James Irwin Charter HS,12:00 PM,11:00 PM,319098876,35,,317,Steve McKitrick,25,Can we adjust leave time to 1:30.  The game time got moved back - CM,Sep 2026
            "Thu, 10 Sep ",HS,Soccer - Boys - V,Denver - See Trip Notes,11:00 AM,10:00 PM,319098879,45,,417,,25,Day 1 at DLMK HS,Sep 2026
            "Fri, 11 Sep ",,,,,,,,,,,,Day 2 at DSST-CV,
            "Thu, 24 Sep ",MS,Cross Country Meet,Royal Gorge Park,10:00 AM,9:00 PM,319100536,27,,344,Steve McKitrick,25, - Both Teams Ride Together,Sep 2026
            "Thu, 24 Sep ",HS,Cross Country Meet - Boys - V,Royal Gorge Park,11:30 AM,10:00 PM,319098894,27,,344,,,,Sep 2026
            """;

        var first = await service.ImportBoardCsvAsync(csv);
        Assert.That(first.Created, Is.EqualTo(9));
        var second = await service.ImportBoardCsvAsync(csv);
        Assert.That(second.Updated, Is.EqualTo(9));
        Assert.That(second.Created, Is.EqualTo(0));

        var trips = (await service.GetAllTripsAsync()).ToList();
        Assert.That(trips.All(t => t.RouteId is null), Is.True);

        var playoff = trips.Single(t => t.ExternalTicketNo == "319101249");
        Assert.That(playoff.Status, Is.EqualTo(TripStatus.MissingInfo));
        Assert.That(playoff.PlannedHeadcount, Is.EqualTo(45));
        Assert.That(playoff.DestinationName, Is.Null);
        Assert.That(playoff.IsOvernightPending, Is.True);

        var assigned = trips.Single(t => t.ExternalTicketNo == "319098871");
        Assert.That(assigned.Status, Is.EqualTo(TripStatus.Assigned));
        Assert.That(assigned.VehicleId, Is.Not.Null);
        Assert.That(assigned.DriverId, Is.Not.Null);
        Assert.That(assigned.OriginName, Is.EqualTo("HS"));

        var oos = trips.Single(t => t.ExternalTicketNo == "319098881");
        Assert.That(oos.VehicleId, Is.Null);
        Assert.That(first.Warnings.Any(w => w.Contains("Out of Service", StringComparison.Ordinal)), Is.True);

        var multi = trips.Single(t => t.ExternalTicketNo == "319101941");
        Assert.That(multi.IsMultiAsset, Is.True);
        Assert.That(multi.VehicleId, Is.Null);

        var leaveNote = trips.Single(t => t.ExternalTicketNo == "319098876");
        Assert.That(leaveNote.PickupTime, Is.EqualTo(TimeSpan.FromHours(12)));
        Assert.That(leaveNote.TripNotes, Does.Contain("adjust leave time to 1:30"));

        var day2 = trips.Single(t => t.ExternalTicketNo == "319098879-DAY2");
        var day1 = trips.Single(t => t.ExternalTicketNo == "319098879");
        Assert.That(day2.LinkedTripId, Is.EqualTo(day1.TripEventId));
        Assert.That(day1.Status, Is.EqualTo(TripStatus.MissingInfo));
        Assert.That(day1.DestinationLocationId, Is.Null);

        var bothA = trips.Single(t => t.ExternalTicketNo == "319100536");
        var bothB = trips.Single(t => t.ExternalTicketNo == "319098894");
        Assert.That(bothA.LinkedTripId == bothB.TripEventId || bothB.LinkedTripId == bothA.TripEventId, Is.True);
    }

    [Test]
    public async Task AddTrip_AllowsMissingInfoWithoutDestination()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        var service = new TripEventService(factory);
        var trip = new TripEvent
        {
            ExternalTicketNo = "319101249",
            TripDate = new DateTime(2026, 10, 27),
            PlannedHeadcount = 45,
            RequestingSchool = "HS",
            GroupOrActivity = "Playoffs",
            Status = TripStatus.MissingInfo,
            POCName = string.Empty
        };

        await service.AddTripAsync(trip);
        var saved = await service.GetTripByIdAsync(trip.TripEventId);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.DestinationName, Is.Null);
        Assert.That(saved.Status, Is.EqualTo(TripStatus.MissingInfo));
        Assert.That(saved.RouteId, Is.Null);
    }

    [Test]
    public async Task Confirm_RequiresValidatedDestinationTimesDriverAndBus()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        await SeedFleetAsync(factory);
        var service = new TripEventService(factory);

        var incomplete = new TripEvent
        {
            ExternalTicketNo = "no-dest",
            TripDate = new DateTime(2026, 9, 5),
            PickupTime = TimeSpan.FromHours(6),
            ReturnClockTime = TimeSpan.FromHours(23),
            PlannedHeadcount = 25,
            Status = TripStatus.Draft
        };
        await service.AddTripAsync(incomplete);
        var denied = await service.ConfirmTripAsync(incomplete.TripEventId);
        Assert.That(denied.IsFailure, Is.True);

        using (var db = factory.CreateWriteDbContext())
        {
            var dest = ValidatedDestination();
            db.Destinations.Add(dest);
            await db.SaveChangesAsync();

            var driver = db.Drivers.First();
            var bus = db.Buses.First(b => b.BusNumber == "25");
            var ready = new TripEvent
            {
                ExternalTicketNo = "ready",
                TripDate = new DateTime(2026, 9, 5),
                PickupTime = TimeSpan.FromHours(6),
                ReturnClockTime = TimeSpan.FromHours(23),
                DestinationName = dest.Name,
                DestinationLocationId = dest.DestinationId,
                DriverId = driver.DriverId,
                VehicleId = bus.BusId,
                Status = TripStatus.Assigned
            };
            db.TripEvents.Add(ready);
            await db.SaveChangesAsync();

            var ok = await service.ConfirmTripAsync(ready.TripEventId);
            Assert.That(ok.IsSuccess, Is.True, ok.Error);
        }
    }

    [Test]
    public async Task RefreshPathMiles_OnlyAfterOriginAndDestinationValidated()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        var routing = new Mock<IRoutingService>();
        routing.Setup(r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrivePathResult
            {
                EncodedPolyline = "abc",
                DistanceMeters = 16093
            });

        var service = new TripEventService(factory, routing.Object);
        using var db = factory.CreateWriteDbContext();
        var origin = ValidatedDestination("HS", 38.09m, -102.62m);
        var dest = ValidatedDestination("Strasburg HS", 39.7m, -104.3m);
        var unvalidated = new Destination
        {
            Name = "Guess",
            Address = "x",
            City = "x",
            State = "CO",
            ZipCode = "81052",
            Latitude = 0m,
            Longitude = 0m,
            DestinationType = DestinationTypes.TripDestination
        };
        db.Destinations.AddRange(origin, dest, unvalidated);
        await db.SaveChangesAsync();

        var skipped = new TripEvent
        {
            ExternalTicketNo = "skip",
            TripDate = DateTime.Today,
            OriginLocationId = origin.DestinationId,
            DestinationLocationId = unvalidated.DestinationId
        };
        db.TripEvents.Add(skipped);
        var ready = new TripEvent
        {
            ExternalTicketNo = "path",
            TripDate = DateTime.Today,
            OriginLocationId = origin.DestinationId,
            DestinationLocationId = dest.DestinationId
        };
        db.TripEvents.Add(ready);
        await db.SaveChangesAsync();

        await service.RefreshPathMilesAsync(skipped.TripEventId);
        routing.Verify(
            r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await service.RefreshPathMilesAsync(ready.TripEventId);
        routing.Verify(
            r => r.ComputeDrivePathAsync(
                It.IsAny<(double, double)>(),
                It.IsAny<(double, double)>(),
                It.IsAny<IReadOnlyList<(double, double)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var stored = await service.GetTripByIdAsync(ready.TripEventId);
        Assert.That(stored!.PathMiles, Is.EqualTo(10.00m).Within(0.05m));
        Assert.That(stored.PlannedMiles, Is.Null);
    }

    [Test]
    public async Task Import_ConfirmedUnchangedStaysConfirmed_ThenMaterialChangeBecomesChanged()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        await SeedFleetAsync(factory);
        var service = new TripEventService(factory);

        using (var db = factory.CreateWriteDbContext())
        {
            db.Destinations.Add(ValidatedDestination("Strasburg HS"));
            await db.SaveChangesAsync();
        }

        var csv = $"""
            {Header}
            "Sat, 5 Sep ",HS,Volleyball Tournament - Girls - JV,Strasburg HS,6:00 AM,11:00 PM,319098871,25,,338,Elby Sneller,25,,Sep 2026
            """;

        await service.ImportBoardCsvAsync(csv);
        var trip = (await service.GetAllTripsAsync()).Single(t => t.ExternalTicketNo == "319098871");
        var confirmed = await service.ConfirmTripAsync(trip.TripEventId);
        Assert.That(confirmed.IsSuccess, Is.True, confirmed.Error);

        await service.ImportBoardCsvAsync(csv);
        trip = (await service.GetAllTripsAsync()).Single(t => t.ExternalTicketNo == "319098871");
        Assert.That(trip.Status, Is.EqualTo(TripStatus.Confirmed));

        var moved = $"""
            {Header}
            "Sat, 5 Sep ",HS,Volleyball Tournament - Girls - JV,Woodland Park HS,6:00 AM,11:00 PM,319098871,25,,338,Elby Sneller,25,,Sep 2026
            """;
        await service.ImportBoardCsvAsync(moved);
        trip = (await service.GetAllTripsAsync()).Single(t => t.ExternalTicketNo == "319098871");
        Assert.That(trip.Status, Is.EqualTo(TripStatus.Changed));
        Assert.That(trip.DestinationName, Is.EqualTo("Woodland Park HS"));
        Assert.That(trip.DestinationLocationId, Is.Null);
    }

    [Test]
    public async Task Import_ClearsDestinationLocationWhenCatalogNoLongerMatches()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        await SeedFleetAsync(factory);
        var service = new TripEventService(factory);
        using (var db = factory.CreateWriteDbContext())
        {
            db.Destinations.Add(ValidatedDestination("Strasburg HS"));
            await db.SaveChangesAsync();
        }

        var csv = $"""
            {Header}
            "Sat, 5 Sep ",HS,Volleyball Tournament - Girls - JV,Strasburg HS,6:00 AM,11:00 PM,319098871,25,,338,Elby Sneller,25,,Sep 2026
            """;
        await service.ImportBoardCsvAsync(csv);
        var trip = (await service.GetAllTripsAsync()).Single(t => t.ExternalTicketNo == "319098871");
        Assert.That(trip.DestinationLocationId, Is.Not.Null);

        var seeNotes = $"""
            {Header}
            "Sat, 5 Sep ",HS,Volleyball Tournament - Girls - JV,Denver - See Trip Notes,6:00 AM,11:00 PM,319098871,25,,338,Elby Sneller,25,,Sep 2026
            """;
        await service.ImportBoardCsvAsync(seeNotes);
        trip = (await service.GetAllTripsAsync()).Single(t => t.ExternalTicketNo == "319098871");
        Assert.That(trip.DestinationLocationId, Is.Null);
        Assert.That(trip.Status, Is.EqualTo(TripStatus.MissingInfo));
    }

    [Test]
    public void TripEvent_DoesNotCloneRouteOrAddIsTrip()
    {
        var tripSource = CoreSourceFile.Read("Models/Trips/TripEvent.cs");
        Assert.That(tripSource, Does.Not.Contain("IsTrip"));
        Assert.That(tripSource, Does.Contain("never add IsTrip"));

        var routeSource = CoreSourceFile.Read("Models/Route.cs");
        Assert.That(routeSource, Does.Not.Contain("IsTrip"));
    }

    private static async Task SeedFleetAsync(IBusBuddyDbContextFactory factory)
    {
        using var db = factory.CreateWriteDbContext();
        db.Buses.AddRange(
            new Bus { BusNumber = "25", Year = 2020, Make = "IC", Model = "CE", SeatingCapacity = 38, VINNumber = "VIN25", LicenseNumber = "L25", Status = "Active" },
            new Bus { BusNumber = "23", Year = 2020, Make = "IC", Model = "CE", SeatingCapacity = 42, VINNumber = "VIN23", LicenseNumber = "L23", Status = "Active" },
            new Bus { BusNumber = "24", Year = 2020, Make = "IC", Model = "CE", SeatingCapacity = 57, VINNumber = "VIN24", LicenseNumber = "L24", Status = "Active" },
            new Bus { BusNumber = "22", Year = 2018, Make = "IC", Model = "CE", SeatingCapacity = 48, VINNumber = "VIN22", LicenseNumber = "L22", Status = "Active" },
            new Bus { BusNumber = "Transit 1", Year = 2019, Make = "Ford", Model = "Transit", SeatingCapacity = 14, VINNumber = "VINT1", LicenseNumber = "LT1", Status = "Active" },
            new Bus { BusNumber = "EXP", Year = 2017, Make = "Chevy", Model = "Express", SeatingCapacity = 7, VINNumber = "VINEXP", LicenseNumber = "LEXP", Status = "Active" });
        db.Drivers.AddRange(
            new Driver { DriverName = "Elby Sneller", DriversLicenceType = "B" },
            new Driver { DriverName = "Steve McKitrick", DriversLicenceType = "B" },
            new Driver { DriverName = "Rod Jagers", DriversLicenceType = "B" },
            new Driver { DriverName = "Tim Weeks", DriversLicenceType = "B" });
        await db.SaveChangesAsync();
    }

    private static Destination ValidatedDestination(
        string name = "Strasburg HS",
        decimal lat = 39.74m,
        decimal lon = -104.32m) =>
        new()
        {
            Name = name,
            Address = "1 Main",
            City = "Strasburg",
            State = "CO",
            ZipCode = "80136",
            Latitude = lat,
            Longitude = lon,
            DestinationType = DestinationTypes.TripDestination
        };
}
