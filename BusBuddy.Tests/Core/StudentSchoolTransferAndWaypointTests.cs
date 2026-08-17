using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class StudentSchoolTransferAndWaypointTests
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
            .UseInMemoryDatabase($"TransferWp_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    [Test]
    public async Task AssignTransfer_RequiresPickupDropoffAndTimes()
    {
        var factory = new TestDbContextFactory(CreateOptions());
        var service = new StudentSchoolTransferService(factory);
        var transfer = new StudentSchoolTransfer
        {
            StudentId = 1,
            FromDestinationId = 1,
            ToDestinationId = 2
        };

        try
        {
            await service.AssignTransferAsync(transfer);
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            Assert.Pass();
        }
    }

    [Test]
    public async Task AssignTransfer_AndRebuild_IncludesTransferStopPairs()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        await using (var seed = factory.CreateDbContext())
        {
            seed.Destinations.AddRange(
                new Destination
                {
                    DestinationId = 1,
                    Name = "Neighborhood School",
                    Address = "100 Main",
                    City = "Wiley",
                    State = "CO",
                    ZipCode = "81092",
                    DestinationType = DestinationTypes.School,
                    Latitude = 38.15m,
                    Longitude = -102.72m,
                    IsActive = true
                },
                new Destination
                {
                    DestinationId = 2,
                    Name = "Receiving School",
                    Address = "200 Oak",
                    City = "Lamar",
                    State = "CO",
                    ZipCode = "81052",
                    DestinationType = DestinationTypes.School,
                    Latitude = 38.09m,
                    Longitude = -102.62m,
                    IsActive = true
                });
            seed.Students.Add(new Student
            {
                StudentName = "Transfer Kid",
                Active = true,
                Latitude = 38.16m,
                Longitude = -102.71m,
                AMRoute = "Route A",
                CreatedDate = DateTime.UtcNow
            });
            seed.Routes.Add(new Route
            {
                RouteName = "Route A",
                Date = DateTime.Today,
                IsActive = true,
                School = "Receiving School"
            });
            await seed.SaveChangesAsync();
        }

        int studentId;
        int routeId;
        await using (var ctx = factory.CreateDbContext())
        {
            studentId = ctx.Students.Single().StudentId;
            routeId = ctx.Routes.Single().RouteId;
        }

        var rebuild = new RouteWaypointRebuildService(factory);
        var transferService = new StudentSchoolTransferService(factory, rebuild);

        await transferService.AssignTransferAsync(new StudentSchoolTransfer
        {
            StudentId = studentId,
            FromDestinationId = 1,
            ToDestinationId = 2,
            PickupAddress = "100 Main, Wiley, CO 81092",
            DropoffAddress = "200 Oak, Lamar, CO 81052",
            PickupTime = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(15)),
            DropoffTime = TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(45)),
            EffectiveDate = DateTime.Today
        });

        var json = await rebuild.RebuildAndPersistAsync(routeId);
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        var points = RouteWaypointSerializer.Parse(json);
        Assert.That(points.Count, Is.GreaterThanOrEqualTo(3), "home + pickup + dropoff (+ school)");
        Assert.That(points.Any(p => Math.Abs(p.Latitude - 38.15) < 0.001), Is.True);
        Assert.That(points.Any(p => Math.Abs(p.Latitude - 38.09) < 0.001), Is.True);
    }
}
