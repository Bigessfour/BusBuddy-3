using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DriverTrainingServiceTests
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
            .UseInMemoryDatabase($"DriverTraining_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    [Test]
    public async Task EnsureMatrixChecklist_SeedsStandardCdeRows()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        await using (var seed = factory.CreateDbContext())
        {
            seed.Drivers.Add(new Driver
            {
                DriverName = "Test Driver",
                DriversLicenceType = "CDL",
                Status = "Active",
                CreatedDate = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var service = new DriverTrainingService(factory);
        int driverId;
        await using (var ctx = factory.CreateDbContext())
        {
            driverId = ctx.Drivers.Single().DriverId;
        }

        var records = await service.EnsureMatrixChecklistAsync(driverId);
        var expectedRequired = CdeDriverTrainingCodes.Catalog.Count(c => c.OftenApplicable);

        Assert.That(records.Count, Is.EqualTo(expectedRequired));
        Assert.That(records.Any(r => r.RequirementCode == CdeDriverTrainingCodes.FirstAidCpr), Is.True);
        Assert.That(records.Any(r => r.RequirementCode == CdeDriverTrainingCodes.EldtPreservice), Is.True);
        Assert.That(records.Any(r => r.RequirementCode == CdeDriverTrainingCodes.CsrsTraining), Is.False);

        var again = await service.EnsureMatrixChecklistAsync(driverId);
        Assert.That(again.Count, Is.EqualTo(expectedRequired), "second ensure should be idempotent");
    }

    [Test]
    public async Task UpsertCompletion_SetsExpiryAndRefreshesTrainingComplete()
    {
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);
        await using (var seed = factory.CreateDbContext())
        {
            seed.Drivers.Add(new Driver
            {
                DriverName = "Trainee",
                DriversLicenceType = "CDL",
                Status = "Active",
                TrainingComplete = false,
                CreatedDate = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var service = new DriverTrainingService(factory);
        int driverId;
        await using (var ctx = factory.CreateDbContext())
        {
            driverId = ctx.Drivers.Single().DriverId;
        }

        await service.EnsureMatrixChecklistAsync(driverId);
        var completed = DateTime.Today;
        await service.UpsertCompletionAsync(
            driverId,
            CdeDriverTrainingCodes.FirstAidCpr,
            completed);

        var records = await service.GetRecordsForDriverAsync(driverId);
        var firstAid = records.Single(r => r.RequirementCode == CdeDriverTrainingCodes.FirstAidCpr);
        Assert.That(firstAid.CompletedDate?.Date, Is.EqualTo(completed));
        Assert.That(firstAid.ExpiryDate?.Date, Is.EqualTo(completed.AddMonths(24)));

        // Incomplete until all required rows completed
        var flag = await service.RefreshTrainingCompleteFlagAsync(driverId);
        Assert.That(flag, Is.False);
    }
}
