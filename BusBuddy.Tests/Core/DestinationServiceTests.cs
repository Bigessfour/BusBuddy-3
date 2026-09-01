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
public class DestinationServiceTests
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
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"Destinations_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    [Test]
    public async Task GetActiveSchools_EmptyWhenNoneCataloged()
    {
        var sut = new DestinationService(CreateFactory());
        var schools = await sut.GetActiveSchoolsAsync();
        Assert.That(schools, Is.Empty);
    }

    [Test]
    public async Task UpdateSchoolTimes_PersistsStartAndDismissal()
    {
        var factory = CreateFactory();
        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Destinations.Add(new Destination
            {
                Name = "Oakridge School",
                Address = "100 Main",
                City = "Oakridge",
                State = "CO",
                ZipCode = "80000",
                DestinationType = DestinationTypes.School,
                IsActive = true
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new DestinationService(factory);
        var school = (await sut.GetActiveSchoolsAsync())[0];

        var ok = await sut.UpdateSchoolTimesAsync(
            school.DestinationId,
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15.5));

        Assert.That(ok, Is.True);
        var updated = await sut.GetByIdAsync(school.DestinationId);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.StartTime, Is.EqualTo(TimeSpan.FromHours(8)));
        Assert.That(updated.DismissalTime, Is.EqualTo(TimeSpan.FromHours(15.5)));
    }

    [Test]
    public async Task AddSchool_PersistsCatalogRowWithBellTimes()
    {
        var factory = CreateFactory();
        var sut = new DestinationService(factory);

        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "co",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15.5));

        Assert.That(school.DestinationId, Is.GreaterThan(0));
        Assert.That(school.State, Is.EqualTo("CO"));
        Assert.That(school.DestinationType, Is.EqualTo(DestinationTypes.School));
        Assert.That(school.StartTime, Is.EqualTo(TimeSpan.FromHours(8)));
        Assert.That(school.DismissalTime, Is.EqualTo(TimeSpan.FromHours(15.5)));

        var listed = await sut.GetActiveSchoolsAsync();
        Assert.That(listed.Select(s => s.Name), Does.Contain("Oakridge School"));
    }

    [Test]
    public async Task AddSchool_PersistsOptionalGps()
    {
        var sut = new DestinationService(CreateFactory());
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15),
            latitude: 38.1234m,
            longitude: -102.5678m);

        var loaded = await sut.GetByIdAsync(school.DestinationId);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Latitude, Is.EqualTo(38.1234m));
        Assert.That(loaded.Longitude, Is.EqualTo(-102.5678m));
    }

    [Test]
    public void AddSchool_DuplicateName_Throws()
    {
        var factory = CreateFactory();
        var sut = new DestinationService(factory);
        Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () =>
        {
            await sut.AddSchoolAsync("Oakridge School", "1 Main", "Oakridge", "CO", "80000",
                TimeSpan.FromHours(8), TimeSpan.FromHours(15));
            await sut.AddSchoolAsync("Oakridge School", "2 Main", "Oakridge", "CO", "80000",
                TimeSpan.FromHours(8), TimeSpan.FromHours(15));
        }));
    }

    [Test]
    public void AddSchool_DismissalBeforeStart_Throws()
    {
        var sut = new DestinationService(CreateFactory());
        Assert.ThrowsAsync<ArgumentException>((Func<Task>)(() => sut.AddSchoolAsync(
            "Oakridge School", "1 Main", "Oakridge", "CO", "80000",
            TimeSpan.FromHours(15), TimeSpan.FromHours(8))));
    }

    [Test]
    public async Task UpdateSchoolTimes_UnknownId_ReturnsFalse()
    {
        var sut = new DestinationService(CreateFactory());
        var ok = await sut.UpdateSchoolTimesAsync(999, TimeSpan.FromHours(8), TimeSpan.FromHours(15));
        Assert.That(ok, Is.False);
    }

    [Test]
    public async Task GetActiveDestinations_ExcludesInactiveAndDeleted()
    {
        var factory = CreateFactory();
        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Destinations.Add(new Destination
            {
                Name = "Inactive School",
                Address = "2 Main",
                City = "Oakridge",
                State = "CO",
                ZipCode = "81092",
                DestinationType = DestinationTypes.School,
                IsActive = false
            });
            ctx.Destinations.Add(new Destination
            {
                Name = "Deleted School",
                Address = "3 Main",
                City = "Oakridge",
                State = "CO",
                ZipCode = "81092",
                DestinationType = DestinationTypes.School,
                IsActive = true,
                IsDeleted = true
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new DestinationService(factory);
        var schools = await sut.GetActiveDestinationsAsync(DestinationTypes.School);

        Assert.That(schools.Select(s => s.Name), Does.Not.Contain("Inactive School"));
        Assert.That(schools.Select(s => s.Name), Does.Not.Contain("Deleted School"));
    }
}
