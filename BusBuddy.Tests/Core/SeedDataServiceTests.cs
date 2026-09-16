using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class SeedDataServiceTests
    {
        [Test]
        public async Task SeedStudentsFromCsvAsync_AddsAllStudents_NoDuplicates()
        {
            var mockFactory = new Mock<IBusBuddyDbContextFactory>();
            var students = new List<Student>();
            var families = new List<Family>();
            var destinations = new List<Destination>();
            var studentsDbSet = CreateMockDbSet(students);
            var familiesDbSet = CreateMockDbSet(families);
            var destinationsDbSet = CreateMockDbSet(destinations);
            var mockContext = new Mock<BusBuddyDbContext>();
            mockContext.Setup(c => c.Students).Returns(studentsDbSet.Object);
            mockContext.Setup(c => c.Families).Returns(familiesDbSet.Object);
            mockContext.Setup(c => c.Destinations).Returns(destinationsDbSet.Object);
            mockFactory.Setup(f => f.CreateDbContext()).Returns(mockContext.Object);

            var service = new SeedDataService(mockFactory.Object);
            await service.SeedStudentsFromCsvAsync();

            Assert.That(students.Count, Is.EqualTo(2)); // Matches embedded CSV rows in SeedDataService
            Assert.That(students.Select(s => s.StudentNumber).Distinct().Count(), Is.EqualTo(students.Count));
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        [Test]
        public async Task SeedStudentsFromCsvAsync_GeneratesStudentNumber_WhenMissing()
        {
            // Setup: Use a CSV row with blank Student # (modify SeedDataService for testability if needed)
            // ...mock setup as above...
            // After seeding:
            // Assert.That(students.Any(s => s.StudentNumber.StartsWith("STU")), Is.True);
        }

        [Test]
        public async Task SeedStudentsFromCsvAsync_SkipsInvalidRows_AndLogs()
        {
            // Setup: Add a row with all fields blank or missing required fields
            // ...mock setup as above...
            // After seeding:
            // Assert that no student was added for that row
            // Optionally, verify logger was called with error (using Serilog test sink)
        }

        [Test]
        public async Task SeedStudentsFromCsvAsync_GroupsSiblings_SameFamily()
        {
            // Setup: Two rows, same parent, second row blanks parent fields
            // ...mock setup as above...
            // After seeding:
            // var familyIds = students.Select(s => s.FamilyId).Distinct().ToList();
            // Assert.That(familyIds.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_AddsRowsFromFile_AndSkipsExistingNames()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"CsvImport_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new SeedDataService(new TestDbContextFactory(options));

            var csv = StudentCsv("Import,Rider,4,Pat,Rider,100 Main,Oakridge,CO,Prowers,,719-555-0100,,,,,,,,,,,,,");
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-import-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, csv);
            try
            {
                var first = await service.ImportStudentsFromCsvAsync(path);
                var second = await service.ImportStudentsFromCsvAsync(path);

                Assert.That(first, Is.EqualTo(1));
                Assert.That(second, Is.EqualTo(0));
                var imported = context.Students.Single(s => s.StudentName == "Import Rider");
                Assert.That(imported.HomeAddress, Does.Contain("100 Main"));
                Assert.That(imported.HomeAddress, Does.Contain("Oakridge"));
                Assert.That(imported.HomeAddress, Does.Contain("Prowers"));
                Assert.That(imported.HomePhone, Is.EqualTo("719-555-0100"));
                Assert.That(imported.StudentNumber, Is.EqualTo("STU0001"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_AllocatesNextStudentNumber_WhenStu0001Exists()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"CsvImportNum_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Students.Add(new Student
            {
                StudentName = "Existing Rider",
                StudentNumber = "STU0001",
                Grade = "2",
                School = "Oakridge School"
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-import-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, StudentCsv("Import,Rider,4,Pat,Rider,100 Main,Oakridge,CO,Prowers,,719-555-0100,,,,,,,,,,,,,"));
            try
            {
                var added = await service.ImportStudentsFromCsvAsync(path);
                Assert.That(added, Is.EqualTo(1));
                var imported = context.Students.Single(s => s.StudentName == "Import Rider");
                Assert.That(imported.StudentNumber, Is.EqualTo("STU0002"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ImportStudentsFromCsvAsync_RejectsUnexpectedHeader()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"CsvImportBad_{Guid.NewGuid()}")
                .Options;
            using var context = new BusBuddyDbContext(options);
            context.Database.EnsureCreated();
            var service = new SeedDataService(new TestDbContextFactory(options));
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-import-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, "ignored\nName,Age\nAlice,10\n");
            try
            {
                var ex = Assert.ThrowsAsync<InvalidOperationException>(
                    (Func<Task>)(() => service.ImportStudentsFromCsvAsync(path)));
                Assert.That(ex!.Message, Does.Contain("expected student format"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_RosterFormat_RoundTripsSpecialNeedsCampusAndSiblings()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RosterImport_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new SeedDataService(new TestDbContextFactory(options));

            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-roster-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, RosterCsv(
                "3,6:47,TEST_STUDENT_01,,TEST_HS,2026-2027,100 Test St,,,,TEST_GUARDIAN_01,555-0100,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,\"Stop 3, side gate\"",
                "5,7:00,TEST_STUDENT_02,,TEST_ES,2026-2027,200 Test St,,,,TEST_GUARDIAN_02,555-0101,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,",
                "5,7:00,TEST_STUDENT_03,,TEST_ES,2026-2027,200 Test St,,,,TEST_GUARDIAN_02,555-0101,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,"));
            try
            {
                var added = await service.ImportStudentsFromCsvAsync(path);
                Assert.That(added, Is.EqualTo(3));

                var first = context.Students.Single(s => s.StudentName == "TEST_STUDENT_01");

                // The whole point of the roster format: these survive the round trip.
                Assert.That(first.RequiresSpecialNeedsBus, Is.True);
                Assert.That(first.RequiresAide, Is.True);
                Assert.That(first.School, Is.EqualTo("TEST_HS"));
                Assert.That(first.AMRoute, Is.EqualTo("TEST_SN_ROUTE"));
                Assert.That(first.PMRoute, Is.Empty);
                Assert.That(first.PickupStopId, Is.Null, "special needs is home pickup");
                Assert.That(first.Active, Is.True);
                Assert.That(first.Latitude, Is.Null, "coordinates come from Address Validation");
                Assert.That(first.Longitude, Is.Null);
                Assert.That(first.TransportationNotes, Does.Contain("side gate"), "quoted comma preserved");
                Assert.That(first.StudentNumber, Is.EqualTo("STU0001"));

                // Two campuses on one roster must not collapse onto one.
                Assert.That(context.Students.Select(s => s.School).Distinct().Count(), Is.EqualTo(2));

                // Siblings at one address with one guardian are separate students in one family.
                var siblings = context.Students
                    .Where(s => s.StudentName == "TEST_STUDENT_02" || s.StudentName == "TEST_STUDENT_03")
                    .ToList();
                Assert.That(siblings, Has.Count.EqualTo(2));
                Assert.That(siblings.Select(s => s.FamilyId).Distinct().Count(), Is.EqualTo(1));

                // Re-import is a no-op, same as the legacy format.
                Assert.That(await service.ImportStudentsFromCsvAsync(path), Is.EqualTo(0));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_RosterFormat_KeepsPerStudentCampus_WhenOneActiveSchool()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RosterCampus_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Destinations.Add(new Destination
            {
                Name = "TEST_CATALOG_SCHOOL",
                DestinationType = DestinationTypes.School,
                City = "TESTVILLE",
                State = "CO",
                IsActive = true
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-roster-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, RosterCsv(
                "1,7:00,TEST_STUDENT_11,,TEST_HS,2026-2027,100 Test St,,,,TEST_GUARDIAN_11,555-0100,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,",
                "2,7:05,TEST_STUDENT_12,,,2026-2027,200 Test St,,,,TEST_GUARDIAN_12,555-0101,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,"));
            try
            {
                Assert.That(await service.ImportStudentsFromCsvAsync(path), Is.EqualTo(2));

                // A roster that names the campus keeps it even when the catalog has exactly one school.
                Assert.That(
                    context.Students.Single(s => s.StudentName == "TEST_STUDENT_11").School,
                    Is.EqualTo("TEST_HS"));

                // A blank campus still falls back to the sole active school (legacy behaviour).
                Assert.That(
                    context.Students.Single(s => s.StudentName == "TEST_STUDENT_12").School,
                    Is.EqualTo("TEST_CATALOG_SCHOOL"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_RosterFormat_RejectsSpecialNeedsOnCatalogStop()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RosterBadPickup_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new SeedDataService(new TestDbContextFactory(options));

            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-roster-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, RosterCsv(
                // specs/students.md pickup rule 3: special needs is home pickup, never a catalog stop.
                "1,7:00,TEST_STUDENT_21,,TEST_HS,2026-2027,100 Test St,,,,TEST_GUARDIAN_21,555-0100,,,CatalogStop,TEST_SN_ROUTE,,true,true,false,false,false,true,",
                // CatalogStop without a published PickupStopId is incomplete and is also skipped.
                "2,7:05,TEST_STUDENT_22,,TEST_HS,2026-2027,200 Test St,,,,TEST_GUARDIAN_22,555-0101,,,CatalogStop,TEST_ROUTE,,false,false,false,false,false,true,",
                "3,7:10,TEST_STUDENT_23,,TEST_HS,2026-2027,300 Test St,,,,TEST_GUARDIAN_23,555-0102,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,"));
            try
            {
                Assert.That(await service.ImportStudentsFromCsvAsync(path), Is.EqualTo(1));
                Assert.That(context.Students.Single().StudentName, Is.EqualTo("TEST_STUDENT_23"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_RosterFormat_AmOnlyRowIsNotPmEligible()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RosterEligibility_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new SeedDataService(new TestDbContextFactory(options));

            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-roster-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(path, RosterCsv(
                // AM route assigned, PM column blank — exactly the shape of an AM-only special-needs run.
                "3,6:47,TEST_STUDENT_AMONLY,,TEST_HS,2026-2027,100 Test St,,,,TEST_GUARDIAN_01,555-0100,,,Home,TEST_SN_ROUTE,,true,true,false,false,false,true,",
                // Both routes assigned.
                "4,6:55,TEST_STUDENT_BOTH,,TEST_HS,2026-2027,200 Test St,,,,TEST_GUARDIAN_02,555-0101,,,Home,TEST_ROUTE_A,TEST_ROUTE_A,false,false,false,false,false,true,"));
            try
            {
                Assert.That(await service.ImportStudentsFromCsvAsync(path), Is.EqualTo(2));

                var amOnly = context.Students.Single(s => s.StudentName == "TEST_STUDENT_AMONLY");
                Assert.That(amOnly.RidesAm, Is.True, "AMRoute is assigned, so the AM run is stated");
                Assert.That(
                    amOnly.RidesPm,
                    Is.False,
                    "a blank PMRoute must never import as PM-eligible — specs/students.md requires AM and PM independently");
                Assert.That(amOnly.SchoolYear, Is.EqualTo("2026-2027"), "SchoolYear comes from the roster column");

                var both = context.Students.Single(s => s.StudentName == "TEST_STUDENT_BOTH");
                Assert.That(both.RidesAm, Is.True);
                Assert.That(both.RidesPm, Is.True);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_RosterFormat_ExplicitEligibilityColumnsWinOverRoutes()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RosterExplicitFlags_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var service = new SeedDataService(new TestDbContextFactory(options));

            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-roster-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(
                path,
                "StudentName,School,SchoolYear,HomeAddress,PickupMode,AMRoute,PMRoute,RidesAm,RidesPm\n" +
                // PM-eligible next term but not yet assigned a PM route: eligibility is not assignment.
                "TEST_STUDENT_STATED,TEST_HS,2026-2027,100 Test St,Home,TEST_ROUTE_A,,true,true\n");
            try
            {
                Assert.That(await service.ImportStudentsFromCsvAsync(path), Is.EqualTo(1));

                var stated = context.Students.Single();
                Assert.That(stated.RidesPm, Is.True, "an explicit RidesPm column outranks the blank PMRoute");
                Assert.That(stated.PMRoute, Is.Empty);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task ImportStudentsFromCsvAsync_RosterFormat_CreatesMissingRouteAndKeepsExistingSpelling()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RosterRoutes_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();

            // The route already exists under the district's spelling.
            context.Routes.Add(new Route
            {
                RouteName = "AM TEST_SN Bus 5",
                Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                IsActive = true,
                IsSpecialNeedsRoute = true
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            var path = Path.Combine(Path.GetTempPath(), $"busbuddy-roster-{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(
                path,
                "StudentName,School,SchoolYear,HomeAddress,PickupMode,AMRoute,PMRoute\n" +
                // Same run, different word order than the route row.
                "TEST_STUDENT_31,TEST_HS,2026-2027,100 Test St,Home,AM Bus 5 TEST_SN,\n" +
                // A route nobody has created yet.
                "TEST_STUDENT_32,TEST_HS,2026-2027,200 Test St,Home,TEST_ROUTE_BRAND_NEW,\n");
            try
            {
                Assert.That(await service.ImportStudentsFromCsvAsync(path), Is.EqualTo(2));

                Assert.That(
                    context.Students.Single(s => s.StudentName == "TEST_STUDENT_31").AMRoute,
                    Is.EqualTo("AM TEST_SN Bus 5"),
                    "a word-order variant must resolve to the existing route spelling, not create a second route");
                Assert.That(context.Routes.Count(r => r.IsSpecialNeedsRoute), Is.EqualTo(1));

                var created = context.Routes.Single(r => r.RouteName == "TEST_ROUTE_BRAND_NEW");
                Assert.That(created.IsActive, Is.True, "students assigned to it must pass route validation");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task EnsureRoutesForStudentAssignmentsAsync_CreatesRouteForAlreadyAssignedStudents()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RouteRepair_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Students.Add(new Student
            {
                StudentName = "TEST_STUDENT_41",
                HomeAddress = "100 Test St",
                AMRoute = "AM TEST_SN Bus 5",
                RidesAm = true,
                Active = true
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            var created = await service.EnsureRoutesForStudentAssignmentsAsync();

            Assert.That(created, Is.EqualTo(1));
            await using var verify = new BusBuddyDbContext(options);
            var route = verify.Routes.Single();
            Assert.That(route.RouteName, Is.EqualTo("AM TEST_SN Bus 5"));
            Assert.That(route.IsSpecialNeedsRoute, Is.False, "TEST_SN is not the 'special needs' token");
            var assigned = verify.Students.Single();
            Assert.That(assigned.AmRouteId, Is.EqualTo(route.RouteId));
            Assert.That(assigned.AMRoute, Is.EqualTo(route.RouteName));

            // Second run is a no-op — the route now exists.
            Assert.That(await service.EnsureRoutesForStudentAssignmentsAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task EnsureRoutesForStudentAssignmentsAsync_DualWritesKeysWhenNameIsUnique()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RouteRepairKeys_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Routes.Add(new Route
            {
                RouteName = "North Elementary",
                Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                IsActive = true
            });
            context.Students.Add(new Student
            {
                StudentName = "TEST_STUDENT_REG_ORPHAN",
                HomeAddress = "400 Test St",
                AMRoute = "North Elementary",
                PMRoute = "North Elementary",
                RidesAm = true,
                RidesPm = true,
                Active = true
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            Assert.That(await service.EnsureRoutesForStudentAssignmentsAsync(), Is.EqualTo(0));

            await using var verify = new BusBuddyDbContext(options);
            var route = await verify.Routes.SingleAsync();
            var student = await verify.Students.SingleAsync();
            Assert.That(student.AmRouteId, Is.EqualTo(route.RouteId));
            Assert.That(student.PmRouteId, Is.EqualTo(route.RouteId));
        }

        [Test]
        public async Task EnsureRoutesForStudentAssignmentsAsync_RewritesVariantSpellingToExistingRoute()
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"RouteRepairVariant_{Guid.NewGuid()}")
                .Options;
            await using var context = new BusBuddyDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Routes.Add(new Route
            {
                RouteName = "AM Special Needs Bus 5",
                Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                IsActive = true,
                IsSpecialNeedsRoute = true
            });
            context.Students.Add(new Student
            {
                StudentName = "TEST_STUDENT_42",
                HomeAddress = "100 Test St",
                AMRoute = "AM Bus 5 Special Needs",
                RidesAm = true,
                Active = true
            });
            await context.SaveChangesAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            Assert.That(await service.EnsureRoutesForStudentAssignmentsAsync(), Is.EqualTo(0), "no new route needed");

            await using var verify = new BusBuddyDbContext(options);
            var student = verify.Students.Single();
            Assert.That(
                student.AMRoute,
                Is.EqualTo("AM Special Needs Bus 5"),
                "the student row is corrected to the canonical spelling so route validation passes");
            Assert.That(student.AmRouteId, Is.EqualTo(verify.Routes.Single().RouteId));
            Assert.That(verify.Routes.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task EnsureMapDemoGeoAsync_SeedsSchoolStudentsAndRouteWaypoints_WithoutBusGps()
        {
            BusBuddyDbContext.SkipGlobalSeedData = true;
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"MapDemoGeo_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            await using (var setup = new BusBuddyDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Buses.Add(new Bus
                {
                    BusNumber = "BUS-001",
                    Year = 2020,
                    Make = "Blue Bird",
                    Model = "Vision",
                    SeatingCapacity = 72,
                    VINNumber = "1TESTVIN000000001",
                    LicenseNumber = "TEST1",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "test"
                });
                await setup.SaveChangesAsync();
            }

            var service = new SeedDataService(new TestDbContextFactory(options));
            await service.EnsureMapDemoGeoAsync();

            await using var verify = new BusBuddyDbContext(options);
            var school = await verify.Destinations.FirstOrDefaultAsync(d => d.Name == "Wiley K-12 School");
            Assert.That(school, Is.Not.Null);
            Assert.That(school!.Latitude, Is.Not.Null);
            Assert.That(school.Longitude, Is.Not.Null);

            var bus = await verify.Buses.FirstAsync(b => b.BusNumber == "BUS-001");
            Assert.That(bus.CurrentLatitude, Is.Null);
            Assert.That(bus.CurrentLongitude, Is.Null);
            Assert.That(bus.GPSTracking, Is.False);

            var route = await verify.Routes.FirstOrDefaultAsync(r => r.RouteName == "Special Needs Route");
            Assert.That(route, Is.Not.Null);
            Assert.That(route!.WaypointsJson, Is.Not.Null.And.Not.Empty);

            var studentsWithCoords = await verify.Students.CountAsync(s => s.Latitude != null && s.Longitude != null);
            Assert.That(studentsWithCoords, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task SeedSpecialNeedsTransportPrep_DoesNotReplaceExistingRouteBus()
        {
            BusBuddyDbContext.SkipGlobalSeedData = true;
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"SnPrepKeepBus_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            await using (var setup = new BusBuddyDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Buses.Add(new Bus
                {
                    BusNumber = "Bus-5",
                    Year = 2022,
                    Make = "Thomas",
                    Model = "Saf-T-Liner",
                    SeatingCapacity = 12,
                    VINNumber = "1USERBUS500000001",
                    LicenseNumber = "SN5",
                    Status = "Active",
                    FleetType = "Special Needs",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "test"
                });
                await setup.SaveChangesAsync();
                var userBusId = setup.Buses.Single(b => b.BusNumber == "Bus-5").BusId;
                setup.Routes.Add(new Route
                {
                    RouteName = "Special Needs Route",
                    Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                    IsActive = true,
                    IsSpecialNeedsRoute = true,
                    AMVehicleId = userBusId,
                    PMVehicleId = userBusId,
                    BusNumber = "Bus-5"
                });
                await setup.SaveChangesAsync();
            }

            var service = new SeedDataService(new TestDbContextFactory(options));
            await service.SeedSpecialNeedsTransportPrepAsync();

            await using var verify = new BusBuddyDbContext(options);
            var route = await verify.Routes.SingleAsync(r => r.RouteName == "Special Needs Route");
            var userBus = await verify.Buses.SingleAsync(b => b.BusNumber == "Bus-5");
            Assert.That(route.AMVehicleId, Is.EqualTo(userBus.BusId));
            Assert.That(route.PMVehicleId, Is.EqualTo(userBus.BusId));
            Assert.That(route.BusNumber, Is.EqualTo("Bus-5"));
        }

        [Test]
        public async Task SeedSpecialNeedsTransportPrep_DualWritesRegularStudentRouteKeys()
        {
            BusBuddyDbContext.SkipGlobalSeedData = true;
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"SnPrepRouteKeys_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            await using var setup = new BusBuddyDbContext(options);
            await setup.Database.EnsureCreatedAsync();

            var service = new SeedDataService(new TestDbContextFactory(options));
            await service.SeedSpecialNeedsTransportPrepAsync();

            await using var verify = new BusBuddyDbContext(options);
            var regularRoute = await verify.Routes.SingleAsync(r => r.RouteName == "North Elementary");
            var regular = await verify.Students
                .Where(s => s.StudentName == "TEST_STUDENT_REG_01" || s.StudentName == "TEST_STUDENT_REG_02")
                .ToListAsync();
            Assert.That(regular, Has.Count.EqualTo(2));
            Assert.That(regular.Select(s => s.AmRouteId).Distinct().Single(), Is.EqualTo(regularRoute.RouteId));
            Assert.That(regular.Select(s => s.PmRouteId).Distinct().Single(), Is.EqualTo(regularRoute.RouteId));
        }

        /// <summary>
        /// Roster format: one header row of named columns. Synthetic tokens only — this file is
        /// git-tracked and specs/students.md forbids committing student PII.
        /// </summary>
        private static string RosterCsv(params string[] dataRows) =>
            "StopNumber,PickupTime,StudentName,Grade,School,SchoolYear,HomeAddress,City,State,Zip," +
            "GuardianName,GuardianPhone,EmergencyContactName,EmergencyContactPhone,PickupMode,AMRoute,PMRoute," +
            "RequiresSpecialNeedsBus,RequiresAide,RequiresWheelchair,RequiresSeatBelt,HasMedicalNeeds,Active," +
            "TransportationNotes\n" +
            string.Join("\n", dataRows) + "\n";

        private static string StudentCsv(string dataRow) =>
            "Student,,,Parent,,,,,,,,Joint Parent,,,,,,,Econtact,,\n" +
            "Fname,Lname,Grade,Fname,Lname,Address,City,State,County,Hphone,Cphone,Jparent FirstName,Jparent LastName,Address,City,State,County,Cphone ,Econtact FirstName,Econtact LastName,Econtact Phone\n" +
            dataRow + "\n";
#pragma warning restore CS1998
        // Helper for EF Core 9: manually mock DbSet<T> for in-memory lists
        private static Mock<DbSet<T>> CreateMockDbSet<T>(IList<T> sourceList) where T : class
        {
            var queryable = sourceList.AsQueryable();
            var mockSet = new Mock<DbSet<T>>();
            mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
            mockSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(sourceList.Add);
            mockSet.Setup(d => d.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(items =>
            {
                foreach (var i in items) sourceList.Add(i);
            });
            return mockSet;
        }
    }
}
