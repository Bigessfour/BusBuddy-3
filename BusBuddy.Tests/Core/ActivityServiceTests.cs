using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class ActivityServiceTests
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

    [Test]
    public void Constructor_TakesFactory_NotDbContext()
    {
        var ctor = typeof(ActivityService).GetConstructors().Single();
        var types = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.That(types, Does.Contain(typeof(IBusBuddyDbContextFactory)));
        Assert.That(types, Does.Not.Contain(typeof(BusBuddyDbContext)));
        Assert.That(types, Does.Contain(typeof(PdfReportService)));
    }

    [Test]
    public async Task CreateThenGetAll_UsesFactoryContextPerCall()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"Act_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var seed = new BusBuddyDbContext(options))
        {
            seed.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            seed.Buses.Add(new Bus
            {
                BusId = 1,
                BusNumber = "5",
                Year = 2020,
                Make = "Blue Bird",
                Model = "Vision",
                SeatingCapacity = 72,
                Status = "Active"
            });
            seed.Drivers.Add(new Driver
            {
                DriverId = 1,
                DriverName = "Pat Lee",
                DriversLicenceType = "Standard",
                Status = "Active",
                TrainingComplete = true,
                LicenseExpiryDate = DateTime.Today.AddDays(90)
            });
            await seed.SaveChangesAsync();
        }

        var sut = new ActivityService(new TestDbContextFactory(options), new PdfReportService());
        await sut.CreateActivityAsync(new Activity
        {
            ActivityType = "Field Trip",
            Description = "Museum",
            Destination = "Lamar",
            Date = DateTime.UtcNow.Date,
            RequestedBy = "Office",
            Status = "Scheduled",
            AssignedVehicleId = 1,
            DriverId = 1
        });

        var all = (await sut.GetAllActivitiesAsync()).ToList();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Destination, Is.EqualTo("Lamar"));
    }
}
