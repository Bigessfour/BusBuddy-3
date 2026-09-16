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
    public async Task UpdateSchool_PersistsNameAddressGpsAndTimes()
    {
        var factory = CreateFactory();
        var sut = new DestinationService(factory);
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15));

        var updated = await sut.UpdateSchoolAsync(
            school.DestinationId,
            "Oakridge Elementary",
            "200 Park",
            "Wiley",
            "co",
            "81092",
            TimeSpan.FromHours(7.5),
            TimeSpan.FromHours(16),
            latitude: 38.0872m,
            longitude: -102.6208m);

        Assert.That(updated.Name, Is.EqualTo("Oakridge Elementary"));
        Assert.That(updated.Address, Is.EqualTo("200 Park"));
        Assert.That(updated.City, Is.EqualTo("Wiley"));
        Assert.That(updated.State, Is.EqualTo("CO"));
        Assert.That(updated.ZipCode, Is.EqualTo("81092"));
        Assert.That(updated.StartTime, Is.EqualTo(TimeSpan.FromHours(7.5)));
        Assert.That(updated.DismissalTime, Is.EqualTo(TimeSpan.FromHours(16)));
        Assert.That(updated.Latitude, Is.EqualTo(38.0872m));
        Assert.That(updated.Longitude, Is.EqualTo(-102.6208m));
    }

    [Test]
    public async Task UpdateSchool_AllowsKeepingTheSameName()
    {
        var sut = new DestinationService(CreateFactory());
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15));

        var updated = await sut.UpdateSchoolAsync(
            school.DestinationId,
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(16));

        Assert.That(updated.DismissalTime, Is.EqualTo(TimeSpan.FromHours(16)));
    }

    [Test]
    public async Task DeleteSchool_RemovesUnusedCampus()
    {
        var sut = new DestinationService(CreateFactory());
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15));

        var result = await sut.DeleteSchoolAsync(school.DestinationId);

        Assert.That(result, Is.EqualTo(SchoolDeleteResult.Deleted));
        Assert.That(await sut.GetByIdAsync(school.DestinationId), Is.Null);
        Assert.That(await sut.GetActiveSchoolsAsync(), Is.Empty);
    }

    [Test]
    public async Task DeleteSchool_RetiresWhenStudentsStillAssigned()
    {
        var factory = CreateFactory();
        var sut = new DestinationService(factory);
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15));

        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Students.Add(new Student
            {
                StudentName = "TEST_STUDENT_01",
                StudentNumber = "TEST-0001",
                DestinationId = school.DestinationId,
                SchoolYear = "2026-2027",
            });
            await ctx.SaveChangesAsync();
        }

        var result = await sut.DeleteSchoolAsync(school.DestinationId);

        Assert.That(result, Is.EqualTo(SchoolDeleteResult.Retired));
        var loaded = await sut.GetByIdAsync(school.DestinationId);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.IsActive, Is.False);
        Assert.That(await sut.GetActiveSchoolsAsync(), Is.Empty);
    }

    [Test]
    public async Task DeleteSchool_RetiresWhenPublishedRouteStillNamesCampus()
    {
        var factory = CreateFactory();
        var sut = new DestinationService(factory);
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15));

        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Routes.Add(new Route
            {
                RouteName = "Oakridge AM",
                School = school.Name,
                Date = DateTime.UtcNow.Date,
                IsActive = true
            });
            await ctx.SaveChangesAsync();
        }

        var result = await sut.DeleteSchoolAsync(school.DestinationId);

        Assert.That(result, Is.EqualTo(SchoolDeleteResult.Retired));
        var loaded = await sut.GetByIdAsync(school.DestinationId);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.IsActive, Is.False);
    }

    [Test]
    public async Task GetRosterSchools_IncludesInactiveCampusStudentsStillReference()
    {
        var factory = CreateFactory();
        var sut = new DestinationService(factory);
        var school = await sut.AddSchoolAsync(
            "Oakridge School",
            "100 Main",
            "Oakridge",
            "CO",
            "80000",
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(15));

        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Students.Add(new Student
            {
                StudentName = "TEST_STUDENT_02",
                StudentNumber = "TEST-0002",
                DestinationId = school.DestinationId,
                SchoolYear = "2026-2027",
            });
            await ctx.SaveChangesAsync();
        }

        await sut.DeleteSchoolAsync(school.DestinationId);
        var roster = await sut.GetRosterSchoolsAsync();
        Assert.That(roster.Select(s => s.DestinationId), Does.Contain(school.DestinationId));
        Assert.That(await sut.GetActiveSchoolsAsync(), Is.Empty);
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
