using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class RouteDeterminationServiceTests
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
            .UseInMemoryDatabase($"RouteDetermination_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    private static RouteDeterminationService CreateSut(IBusBuddyDbContextFactory factory) =>
        new(factory, new RouteService(factory));

    [Test]
    public async Task GenerateAndAssign_MissingSchool_Fails()
    {
        var factory = CreateFactory();
        var sut = CreateSut(factory);

        var result = await sut.GenerateAndAssignAsync(
            schoolDestinationId: 999,
            RouteTimeSlotKind.AM,
            FleetKind.HomeToSchool);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("not found"));
    }

    [Test]
    public async Task GenerateAndAssign_AmWithoutStartTime_Fails()
    {
        var factory = CreateFactory();
        await using (var ctx = factory.CreateWriteDbContext())
        {
            ctx.Destinations.Add(new Destination
            {
                Name = "No Bell School",
                Address = "1 Main",
                City = "Wiley",
                State = "CO",
                ZipCode = "81092",
                DestinationType = DestinationTypes.School,
                IsActive = true
            });
            await ctx.SaveChangesAsync();
        }

        int schoolId;
        await using (var ctx = factory.CreateDbContext())
        {
            schoolId = ctx.Destinations.Single().DestinationId;
        }

        var result = await CreateSut(factory).GenerateAndAssignAsync(
            schoolId,
            RouteTimeSlotKind.AM,
            FleetKind.HomeToSchool);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("StartTime"));
    }

    [Test]
    public async Task GenerateAndAssign_DryRun_ProducesDraftProposalsForClusteredRiders()
    {
        var factory = CreateFactory();
        int schoolId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var school = new Destination
            {
                Name = "Wiley School",
                Address = "510 Ward St",
                City = "Wiley",
                State = "CO",
                ZipCode = "81092",
                DestinationType = DestinationTypes.School,
                Latitude = 38.15m,
                Longitude = -102.70m,
                StartTime = TimeSpan.FromHours(8),
                DismissalTime = TimeSpan.FromHours(15.5),
                IsActive = true
            };
            ctx.Destinations.Add(school);

            ctx.Buses.Add(new Bus
            {
                BusNumber = "B1",
                SeatingCapacity = 72,
                Status = "Active",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                VINNumber = "1HGBH41JXMN109186",
                LicenseNumber = "TEST1",
                CreatedDate = DateTime.UtcNow
            });

            for (var i = 0; i < 3; i++)
            {
                ctx.Students.Add(new Student
                {
                    StudentName = $"Rider {i}",
                    StudentNumber = $"R{i}",
                    Grade = "5",
                    HomeAddress = "Addr",
                    City = "Wiley",
                    State = "CO",
                    Zip = "81092",
                    Latitude = 38.150m + i * 0.0002m,
                    Longitude = -102.700m
                });
            }

            await ctx.SaveChangesAsync();
            schoolId = school.DestinationId;
            foreach (var student in ctx.Students)
            {
                student.DestinationId = schoolId;
            }

            await ctx.SaveChangesAsync();
        }

        var result = await CreateSut(factory).GenerateAndAssignAsync(
            schoolId,
            RouteTimeSlotKind.AM,
            FleetKind.HomeToSchool,
            new RouteGenerationOptions { DryRun = true });

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Proposals, Is.Not.Empty);
        Assert.That(result.Proposals[0].SuggestedRouteName, Does.StartWith("Draft-"));
        Assert.That(result.AssignedStudentCount, Is.EqualTo(0), "Dry run must not persist assignments");
    }

    [Test]
    public async Task ApplyClerkOverride_BothSlot_Rejected()
    {
        var result = await CreateSut(CreateFactory()).ApplyClerkOverrideAsync(
            studentId: 1,
            fromRouteId: 1,
            toRouteId: 2,
            RouteTimeSlotKind.Both);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("AM or PM"));
    }
}
