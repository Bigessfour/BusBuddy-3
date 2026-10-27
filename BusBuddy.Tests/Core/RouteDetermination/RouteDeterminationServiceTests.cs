using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
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
                City = "Oakridge",
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
                Name = "Oakridge School",
                Address = "510 Ward St",
                City = "Oakridge",
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
                    City = "Oakridge",
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

    [Test]
    public async Task ApplyClerkOverride_NameOnlyUniqueRoute_MovesOntoTarget()
    {
        var factory = CreateFactory();
        int fromId;
        int toId;
        int studentId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var from = new Route
            {
                RouteName = "North",
                Date = DateTime.UtcNow.Date,
                IsActive = true,
                Session = RouteSession.AM
            };
            var to = new Route
            {
                RouteName = "South",
                Date = DateTime.UtcNow.Date,
                IsActive = true,
                Session = RouteSession.AM
            };
            ctx.Routes.AddRange(from, to);
            var student = new Student
            {
                StudentName = "Ada",
                StudentNumber = "A1",
                Grade = "3",
                HomeAddress = "1 Main",
                City = "Wiley",
                State = "CO",
                Zip = "81092",
                RidesAm = true,
                AMRoute = "North"
            };
            ctx.Students.Add(student);
            await ctx.SaveChangesAsync();
            fromId = from.RouteId;
            toId = to.RouteId;
            studentId = student.StudentId;
        }

        var result = await CreateSut(factory).ApplyClerkOverrideAsync(
            studentId,
            fromRouteId: 0,
            toId,
            RouteTimeSlotKind.AM,
            "District Map");

        Assert.That(result.Success, Is.True, result.Error);
        await using (var ctx = factory.CreateDbContext())
        {
            var student = ctx.Students.Single(s => s.StudentId == studentId);
            Assert.That(student.AmRouteId, Is.EqualTo(toId));
            Assert.That(student.AMRoute, Is.EqualTo("South"));
            Assert.That(StudentRouteAssignment.MatchesAm(student, fromId, "North"), Is.False);
        }
    }

    [Test]
    public async Task GenerateAndAssign_AlreadyAssignedRoster_SucceedsWithoutNewDrafts()
    {
        var factory = CreateFactory();
        int schoolId;
        int routeCount;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var school = School("Assigned School");
            var am = NamedRoute("Assigned AM", RouteSession.AM, school.Name);
            var pm = NamedRoute("Assigned PM", RouteSession.PM, school.Name);
            ctx.Destinations.Add(school);
            ctx.Routes.AddRange(am, pm);
            await ctx.SaveChangesAsync();
            schoolId = school.DestinationId;

            ctx.Students.Add(Rider("TEST_STUDENT_ASSIGNED", schoolId, 38.150m, -102.700m, am, pm));
            await ctx.SaveChangesAsync();
            routeCount = ctx.Routes.Count();
        }

        var result = await CreateSut(factory).GenerateAndAssignAsync(
            schoolId,
            RouteTimeSlotKind.Both,
            FleetKind.HomeToSchool);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.Error, Is.Null);
        Assert.That(result.Proposals, Is.Empty);
        Assert.That(result.AssignedStudentCount, Is.EqualTo(0));
        Assert.That(
            result.Warnings.Any(w => w.Contains("already assigned", StringComparison.Ordinal)
                && w.Contains("no new draft routes", StringComparison.Ordinal)),
            Is.True);

        await using (var ctx = factory.CreateDbContext())
        {
            Assert.That(ctx.Routes.Count(), Is.EqualTo(routeCount));
            Assert.That(ctx.Routes.Any(r => r.RouteName.StartsWith("Draft-")), Is.False);
        }
    }

    [Test]
    public async Task GenerateAndAssign_UnassignedRider_StillAssignsWhenOthersAreAssigned()
    {
        var factory = CreateFactory();
        int schoolId;
        int openStudentId;
        int assignedStudentId;
        int assignedAmId;
        int assignedPmId;
        await using (var ctx = factory.CreateWriteDbContext())
        {
            var school = School("Mixed School");
            var am = NamedRoute("Keep AM", RouteSession.AM, school.Name);
            var pm = NamedRoute("Keep PM", RouteSession.PM, school.Name);
            ctx.Destinations.Add(school);
            ctx.Routes.AddRange(am, pm);
            await ctx.SaveChangesAsync();
            schoolId = school.DestinationId;

            var assigned = Rider("TEST_STUDENT_ASSIGNED", schoolId, 38.150m, -102.700m, am, pm);
            var open = Rider("TEST_STUDENT_OPEN", schoolId, 38.1502m, -102.7002m, am: null, pm: null);
            ctx.Students.AddRange(assigned, open);
            await ctx.SaveChangesAsync();
            assignedStudentId = assigned.StudentId;
            openStudentId = open.StudentId;
            assignedAmId = am.RouteId;
            assignedPmId = pm.RouteId;
        }

        var result = await CreateSut(factory).GenerateAndAssignAsync(
            schoolId,
            RouteTimeSlotKind.Both,
            FleetKind.HomeToSchool);

        Assert.That(result.Success, Is.True, result.Error);
        Assert.That(result.AssignedStudentCount, Is.EqualTo(1));
        Assert.That(result.Proposals, Is.Not.Empty);

        await using (var ctx = factory.CreateDbContext())
        {
            var assigned = ctx.Students.Single(s => s.StudentId == assignedStudentId);
            Assert.That(assigned.AmRouteId, Is.EqualTo(assignedAmId));
            Assert.That(assigned.PmRouteId, Is.EqualTo(assignedPmId));

            var open = ctx.Students.Single(s => s.StudentId == openStudentId);
            Assert.That(open.AmRouteId, Is.Not.Null);
            Assert.That(open.PmRouteId, Is.Not.Null);
            Assert.That(open.AmRouteId, Is.Not.EqualTo(assignedAmId));
            Assert.That(ctx.Routes.Any(r => r.RouteName.StartsWith("Draft-")), Is.True);
        }
    }

    private static Destination School(string name) => new()
    {
        Name = name,
        Address = "500 South 5th Street",
        City = "Lamar",
        State = "CO",
        ZipCode = "81052",
        DestinationType = DestinationTypes.School,
        Latitude = 38.0844m,
        Longitude = -102.6207m,
        StartTime = TimeSpan.FromHours(8),
        DismissalTime = TimeSpan.FromHours(15.5),
        IsActive = true
    };

    private static Route NamedRoute(string name, string session, string school) => new()
    {
        RouteName = name,
        Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
        IsActive = true,
        Session = session,
        School = school
    };

    private static Student Rider(
        string name,
        int schoolId,
        decimal lat,
        decimal lon,
        Route? am,
        Route? pm) => new()
    {
        StudentName = name,
        StudentNumber = name,
        Grade = "5",
        HomeAddress = "210 West Pine Street",
        City = "Lamar",
        State = "CO",
        Zip = "81052",
        DestinationId = schoolId,
        Latitude = lat,
        Longitude = lon,
        RidesAm = true,
        RidesPm = true,
        AmRouteId = am?.RouteId,
        PmRouteId = pm?.RouteId,
        AMRoute = am?.RouteName,
        PMRoute = pm?.RouteName
    };
}
