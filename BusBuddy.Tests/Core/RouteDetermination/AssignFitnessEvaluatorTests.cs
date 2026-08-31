using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core.RouteDetermination;

[TestFixture]
[Category("Unit")]
public class AssignFitnessEvaluatorTests
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

    private static DbContextOptions<BusBuddyDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"AssignFitness_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<(TestDbContextFactory Factory, int StudentId, int RouteId)> SeedAsync(
        int seatingCapacity,
        int alreadyAssigned,
        bool distantStudent = false)
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        var options = CreateOptions();
        var factory = new TestDbContextFactory(options);

        await using (var ctx = factory.CreateWriteDbContext())
        {
            var bus = new Bus
            {
                BusNumber = "B1",
                SeatingCapacity = seatingCapacity,
                Status = "Active",
                Year = 2020,
                Make = "IC",
                Model = "CE",
                VINNumber = "1HGBH41JXMN109186",
                LicenseNumber = "TEST1",
                CreatedDate = DateTime.UtcNow
            };
            ctx.Buses.Add(bus);
            await ctx.SaveChangesAsync();

            var route = new Route
            {
                RouteName = "Test-Route",
                Date = DateTime.Today,
                IsActive = true,
                AMVehicleId = bus.BusId
            };
            ctx.Routes.Add(route);

            var school = new Destination
            {
                Name = "Test School",
                Address = "1 Main",
                City = "Oakridge",
                State = "CO",
                ZipCode = "81092",
                DestinationType = DestinationTypes.School,
                Latitude = 38.15m,
                Longitude = -102.70m,
                StartTime = TimeSpan.FromHours(8),
                IsActive = true
            };
            ctx.Destinations.Add(school);
            await ctx.SaveChangesAsync();

            for (var i = 0; i < alreadyAssigned; i++)
            {
                ctx.Students.Add(new Student
                {
                    StudentName = $"Peer {i}",
                    StudentNumber = $"P{i}",
                    Grade = "5",
                    HomeAddress = "Addr",
                    City = "Oakridge",
                    State = "CO",
                    Zip = "81092",
                    DestinationId = school.DestinationId,
                    Latitude = 38.150m + i * 0.0001m,
                    Longitude = -102.700m,
                    AMRoute = route.RouteName
                });
            }

            var subject = new Student
            {
                StudentName = "Subject",
                StudentNumber = "S1",
                Grade = "5",
                HomeAddress = "Addr",
                City = "Oakridge",
                State = "CO",
                Zip = "81092",
                DestinationId = school.DestinationId,
                Latitude = distantStudent ? 38.40m : 38.151m,
                Longitude = -102.700m
            };
            ctx.Students.Add(subject);
            await ctx.SaveChangesAsync();

            return (factory, subject.StudentId, route.RouteId);
        }
    }

    [Test]
    public async Task Evaluate_SeatingOverload_BlocksWithoutOverride()
    {
        var (factory, studentId, routeId) = await SeedAsync(seatingCapacity: 2, alreadyAssigned: 2);
        var evaluator = new AssignFitnessEvaluator(
            factory,
            Options.Create(new RoutingDistrictSettings { AllowSeatingOverride = true }));

        var result = await evaluator.EvaluateAsync(studentId, routeId, RouteTimeSlotKind.AM);

        Assert.That(result.Allowed, Is.False);
        Assert.That(result.Severity, Is.EqualTo(AssignFitnessSeverity.Block));
        Assert.That(result.SuggestNewRoute, Is.True);
        Assert.That(result.Reasons.Any(r => r.Contains("Seating", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task Evaluate_SeatingOverload_AllowsWithOverride()
    {
        var (factory, studentId, routeId) = await SeedAsync(seatingCapacity: 2, alreadyAssigned: 2);
        var evaluator = new AssignFitnessEvaluator(
            factory,
            Options.Create(new RoutingDistrictSettings { AllowSeatingOverride = true }));

        var result = await evaluator.EvaluateAsync(studentId, routeId, RouteTimeSlotKind.AM, overrideSeating: true);

        Assert.That(result.Allowed, Is.True);
        Assert.That(result.Severity, Is.EqualTo(AssignFitnessSeverity.Warn));
        Assert.That(result.Reasons.Any(r => r.Contains("override", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task Evaluate_GeoOutlier_WarnsAndAllows()
    {
        var (factory, studentId, routeId) = await SeedAsync(
            seatingCapacity: 72, alreadyAssigned: 3, distantStudent: true);
        var evaluator = new AssignFitnessEvaluator(
            factory,
            Options.Create(new RoutingDistrictSettings
            {
                MaxPickupGapMinutes = 5,
                AverageSpeedMph = 25,
                MaxRideMinutes = 120
            }));

        var result = await evaluator.EvaluateAsync(studentId, routeId, RouteTimeSlotKind.AM);

        Assert.That(result.Allowed, Is.True);
        Assert.That(result.Severity, Is.EqualTo(AssignFitnessSeverity.Warn));
        Assert.That(result.Reasons.Any(r => r.Contains("outlier", StringComparison.OrdinalIgnoreCase)), Is.True);
    }
}
