using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Covers the specs/students.md invariants added in the Students ship-readiness pass:
    /// archive-when-they-may-return, logged delete with a required reason, explicit AM/PM eligibility
    /// + SchoolYear, and the <see cref="StudentService.SearchStudentsAsync"/> EF-translation fix.
    /// </summary>
    /// <remarks>All fixtures use obviously synthetic tokens — no student PII.</remarks>
    [TestFixture]
    [Category("Unit")]
    public class StudentArchiveAndEligibilityTests
    {
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private BusBuddyDbContext _dbContext = null!;
        private StudentService _studentService = null!;

        private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
        {
            private readonly DbContextOptions<BusBuddyDbContext> _options;

            public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;

            public BusBuddyDbContext CreateDbContext() => new(_options);

            public BusBuddyDbContext CreateWriteDbContext() => new(_options);
        }

        [SetUp]
        public void SetUp()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"StudentsArchiveDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new BusBuddyDbContext(_dbOptions);
            _studentService = new StudentService(new TestDbContextFactory(_dbOptions));
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        private async Task<Student> SeedStudentAsync(string name = "TEST_STUDENT_01", bool active = true)
        {
            var student = new Student
            {
                StudentName = name,
                StudentNumber = "TEST-0001",
                Grade = "3",
                HomeAddress = "100 Test St",
                City = "Testville",
                State = "CO",
                Zip = "81000",
                Active = active,
                SchoolYear = "2026-2027",
            };

            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            return student;
        }

        #region Item 1 — archive and logged delete

        [Test]
        public async Task ArchiveStudentAsync_ClearsActiveButKeepsRow()
        {
            var student = await SeedStudentAsync();

            var archived = await _studentService.ArchiveStudentAsync(student.StudentId);

            archived.Should().BeTrue();
            var fromDb = await _dbContext.Students.FindAsync(student.StudentId);
            fromDb.Should().NotBeNull("archiving must never remove the row");
            fromDb!.Active.Should().BeFalse();
        }

        [Test]
        public async Task RestoreStudentAsync_ReturnsStudentToActiveService()
        {
            var student = await SeedStudentAsync(active: false);

            var restored = await _studentService.RestoreStudentAsync(student.StudentId);

            restored.Should().BeTrue();
            var fromDb = await _dbContext.Students.FindAsync(student.StudentId);
            fromDb!.Active.Should().BeTrue();
        }

        [Test]
        public async Task ArchivedStudent_StaysLoadable()
        {
            var student = await SeedStudentAsync();
            await _studentService.ArchiveStudentAsync(student.StudentId);

            var all = await _studentService.GetAllStudentsAsync();
            var active = await _studentService.GetActiveStudentsAsync();

            all.Should().ContainSingle(s => s.StudentId == student.StudentId,
                "archived students must remain visible to the roster read");
            active.Should().BeEmpty("the active-only read must exclude archived students");
        }

        [Test]
        public async Task DeleteStudentAsync_RemovesAnActiveStudentAndWritesTheLog()
        {
            var student = await SeedStudentAsync();

            var deleted = await _studentService.DeleteStudentAsync(
                student.StudentId,
                StudentDeletionReason.Moved,
                "left for TEST_DISTRICT");

            deleted.Should().BeTrue();
            (await _dbContext.Students.FindAsync(student.StudentId)).Should().BeNull();
            var log = _dbContext.StudentDeletionLogs.Should().ContainSingle().Subject;
            log.StudentId.Should().Be(student.StudentId);
            log.StudentNumber.Should().Be("TEST-0001");
            log.Reason.Should().Be(nameof(StudentDeletionReason.Moved));
            log.Notes.Should().Be("left for TEST_DISTRICT");
            log.WasActive.Should().BeTrue();
            log.ScheduleCount.Should().Be(0);
            log.TransferCount.Should().Be(0);
        }

        [Test]
        public async Task DeleteStudentAsync_RemovesRelatedAssignmentRowsAndCountsThem()
        {
            var student = await SeedStudentAsync();
            _dbContext.StudentSchedules.Add(new StudentSchedule
            {
                StudentId = student.StudentId,
                AssignmentType = "Regular",
            });
            _dbContext.StudentSchoolTransfers.Add(new StudentSchoolTransfer
            {
                StudentId = student.StudentId,
                FromDestinationId = 1,
                ToDestinationId = 2,
                PickupAddress = "100 Test St",
                DropoffAddress = "200 Test St",
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var deleted = await _studentService.DeleteStudentAsync(
                student.StudentId,
                StudentDeletionReason.NotAttending);

            deleted.Should().BeTrue();
            (await _dbContext.Students.FindAsync(student.StudentId)).Should().BeNull();
            _dbContext.StudentSchedules.Should().BeEmpty();
            _dbContext.StudentSchoolTransfers.Should().BeEmpty();
            var log = _dbContext.StudentDeletionLogs.Should().ContainSingle().Subject;
            log.Reason.Should().Be(nameof(StudentDeletionReason.NotAttending));
            log.ScheduleCount.Should().Be(1);
            log.TransferCount.Should().Be(1);
        }

        [Test]
        public async Task DeleteStudentAsync_RejectsAnUndefinedReason()
        {
            var student = await SeedStudentAsync();

            var act = async () => await _studentService.DeleteStudentAsync(
                student.StudentId,
                (StudentDeletionReason)0);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
            (await _dbContext.Students.FindAsync(student.StudentId)).Should().NotBeNull();
        }

        [Test]
        public void FamilyToStudent_DeleteBehaviourIsRestrict()
        {
            // A Family delete must not cascade-delete students. Clerk deletion is explicit.
            var foreignKey = _dbContext.Model
                .FindEntityType(typeof(Student))!
                .GetForeignKeys()
                .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Family));

            foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        }

        [Test]
        public void StudentSchoolTransferToStudent_DeleteBehaviourIsRestrict()
        {
            var foreignKey = _dbContext.Model
                .FindEntityType(typeof(StudentSchoolTransfer))!
                .GetForeignKeys()
                .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Student));

            foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        }

        #endregion

        #region Item 2 — RidesAm / RidesPm / SchoolYear

        [Test]
        public void NewStudent_IsEligibleForNeitherRun_UntilStated()
        {
            var student = new Student { StudentName = "TEST_STUDENT_DEFAULTS" };

            // The C# default must match the column default in
            // 20260907150000_StudentRideEligibilityAndSchoolYear (defaultValue: false). An intake that
            // says nothing about a run must not mark a child eligible for it.
            student.RidesAm.Should().BeFalse();
            student.RidesPm.Should().BeFalse();
            StudentRideModeHelper.FromStudent(student).Should().Be(StudentRideMode.Neither);
        }

        [Test]
        public void ApplyRouteDerivedEligibility_StatesOnlyTheAssignedRun()
        {
            var amOnly = new Student { StudentName = "TEST_STUDENT_AMONLY", AMRoute = "TEST_ROUTE_A" };

            StudentRideModeHelper.ApplyRouteDerivedEligibility(amOnly).Should().BeTrue();
            amOnly.RidesAm.Should().BeTrue();
            amOnly.RidesPm.Should().BeFalse("a blank PMRoute is not a PM assignment");
        }

        [Test]
        public void ApplyRouteDerivedEligibility_LeavesAnAlreadyStatedRecordAlone()
        {
            // Eligibility is not assignment: PM-eligible with no PM route yet must survive inference.
            var stated = new Student { StudentName = "TEST_STUDENT_STATED", RidesPm = true };

            StudentRideModeHelper.ApplyRouteDerivedEligibility(stated).Should().BeFalse();
            stated.RidesPm.Should().BeTrue();
            stated.RidesAm.Should().BeFalse();
        }

        [Test]
        public async Task RideEligibilityAndSchoolYear_RoundTrip()
        {
            var student = new Student
            {
                StudentName = "TEST_STUDENT_02",
                Grade = "5",
                RidesAm = true,
                RidesPm = false,
                SchoolYear = "2026-2027",
            };

            _dbContext.Students.Add(student);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var fromDb = await _dbContext.Students.FindAsync(student.StudentId);
            fromDb!.RidesAm.Should().BeTrue();
            fromDb.RidesPm.Should().BeFalse();
            fromDb.SchoolYear.Should().Be("2026-2027");
        }

        [Test]
        [TestCase(true, true, StudentRideMode.Both)]
        [TestCase(true, false, StudentRideMode.AM)]
        [TestCase(false, true, StudentRideMode.PM)]
        [TestCase(false, false, StudentRideMode.Neither)]
        public void RideMode_ComesFromEligibilityFlags_NotRouteNames(bool ridesAm, bool ridesPm, StudentRideMode expected)
        {
            var student = new Student
            {
                StudentName = "TEST_STUDENT_03",
                RidesAm = ridesAm,
                RidesPm = ridesPm,
            };

            StudentRideModeHelper.FromStudent(student).Should().Be(expected);
            StudentRideModeHelper.FromFlags(ridesAm, ridesPm).Should().Be(expected);
        }

        [Test]
        public void RideMode_EligibilityIsIndependentOfAssignment()
        {
            // The old inference read route names, so an AM-eligible student with no route yet looked
            // identical to a child who does not ride.
            var eligibleButUnassigned = new Student
            {
                StudentName = "TEST_STUDENT_04",
                RidesAm = true,
                RidesPm = true,
                AMRoute = null,
                PMRoute = null,
            };

            StudentRideModeHelper.FromStudent(eligibleButUnassigned).Should().Be(StudentRideMode.Both);
            StudentRideModeHelper.ApplyRouteDerivedEligibility(eligibleButUnassigned)
                .Should().BeFalse("explicit eligibility must not be overwritten by route inference");
        }

        [Test]
        public void NormalizeForPersistence_DefaultsBlankSchoolYearToCurrent()
        {
            var student = new Student { StudentName = "TEST_STUDENT_05", SchoolYear = "  " };

            StudentRecordNormalizer.NormalizeForPersistence(student);

            student.SchoolYear.Should().Be(StudentRecordNormalizer.CurrentSchoolYear());
        }

        [Test]
        [TestCase(2026, 8, "2026-2027")]
        [TestCase(2027, 6, "2026-2027")]
        [TestCase(2027, 7, "2027-2028")]
        public void CurrentSchoolYear_RollsOverOnFirstOfJuly(int year, int month, string expected)
        {
            StudentRecordNormalizer.CurrentSchoolYear(new DateTime(year, month, 15)).Should().Be(expected);
        }

        [Test]
        public void SpecialNeedsStudent_IsForcedToHomePickup()
        {
            var student = new Student
            {
                StudentName = "TEST_STUDENT_06",
                RequiresSpecialNeedsBus = true,
                PickupStopId = 42,
            };

            StudentRecordNormalizer.NormalizeForPersistence(student);

            student.PickupStopId.Should().BeNull();
            student.PickupMode.Should().Be(LocationTypes.PickupModeHome);
        }

        #endregion

        #region Item 4 — incomplete records

        [Test]
        public async Task GetStudentsWithMissingInfoAsync_IncludesUnvalidatedAddresses()
        {
            var destination = new Destination { Name = "TEST_SCHOOL", Address = "1 Test Way", City = "Testville", State = "CO", ZipCode = "81000" };
            _dbContext.Destinations.Add(destination);
            await _dbContext.SaveChangesAsync();

            var complete = new Student
            {
                StudentName = "TEST_STUDENT_COMPLETE",
                Grade = "4",
                ParentGuardian = "TEST_GUARDIAN",
                EmergencyPhone = "555-000-0001",
                HomeAddress = "100 Test St",
                DestinationId = destination.DestinationId,
                Latitude = 38.0872m,
                Longitude = -102.6208m,
                SchoolYear = "2026-2027",
                RidesAm = true,
            };

            var unvalidated = new Student
            {
                StudentName = "TEST_STUDENT_UNVALIDATED",
                Grade = "4",
                ParentGuardian = "TEST_GUARDIAN",
                EmergencyPhone = "555-000-0002",
                HomeAddress = "200 Test St",
                DestinationId = destination.DestinationId,
                SchoolYear = "2026-2027",
            };

            _dbContext.Students.AddRange(complete, unvalidated);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var incomplete = await _studentService.GetStudentsWithMissingInfoAsync();

            incomplete.Select(s => s.StudentName).Should().ContainSingle()
                .Which.Should().Be("TEST_STUDENT_UNVALIDATED");
        }

        [Test]
        public async Task GetStudentsWithMissingInfoAsync_TreatsPlaceholderCoordinatesAsMissing()
        {
            var destination = new Destination { Name = "TEST_SCHOOL", Address = "1 Test Way", City = "Testville", State = "CO", ZipCode = "81000" };
            _dbContext.Destinations.Add(destination);
            await _dbContext.SaveChangesAsync();

            Student Make(string name, decimal? lat, decimal? lon, int? pickupStopId = null) => new()
            {
                StudentName = name,
                Grade = "4",
                ParentGuardian = "TEST_GUARDIAN",
                EmergencyPhone = "555-000-0003",
                HomeAddress = "300 Test St",
                DestinationId = destination.DestinationId,
                SchoolYear = "2026-2027",
                RidesAm = true,
                Latitude = lat,
                Longitude = lon,
                PickupStopId = pickupStopId,
            };

            var zeroZero = Make("TEST_STUDENT_ZERO", 0m, 0m);
            var centroid = Make(
                "TEST_STUDENT_CENTROID",
                (decimal)LocationCoordinate.UsCentroidLatitude,
                (decimal)LocationCoordinate.UsCentroidLongitude);
            var validated = Make("TEST_STUDENT_VALID", 38.0872m, -102.6208m);
            var atCatalogStop = Make("TEST_STUDENT_AT_STOP", null, null, pickupStopId: 42);

            _dbContext.Students.AddRange(zeroZero, centroid, validated, atCatalogStop);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var incomplete = await _studentService.GetStudentsWithMissingInfoAsync();
            var names = incomplete.Select(s => s.StudentName).ToList();

            // The SQL filter must agree with Student.IsIntakeIncomplete for every row.
            names.Should().BeEquivalentTo("TEST_STUDENT_ZERO", "TEST_STUDENT_CENTROID");
            zeroZero.IsIntakeIncomplete.Should().BeTrue();
            centroid.IsIntakeIncomplete.Should().BeTrue();
            validated.IsIntakeIncomplete.Should().BeFalse();
            atCatalogStop.IsIntakeIncomplete.Should().BeFalse();
        }

        [Test]
        public void UnvalidatedAddress_ReportsIncompleteAndStaysOffTheMap()
        {
            var student = new Student
            {
                StudentName = "TEST_STUDENT_07",
                HomeAddress = "300 Test St",
                SchoolYear = "2026-2027",
                DestinationId = 1,
            };

            student.HasValidatedHomeCoordinates.Should().BeFalse();
            student.HasUnvalidatedHomeAddress.Should().BeTrue();
            student.IsIntakeIncomplete.Should().BeTrue();
            student.IntakeStatus.Should().Be("Address unvalidated");
        }

        #endregion

        #region Item 7 — SearchStudentsAsync EF translation

        [Test]
        public async Task SearchStudentsAsync_MatchesNameCaseInsensitively()
        {
            await SeedStudentAsync("TEST_STUDENT_ALPHA");
            await SeedStudentAsync("TEST_STUDENT_BETA");

            var results = await _studentService.SearchStudentsAsync("alpha");

            results.Should().ContainSingle().Which.StudentName.Should().Be("TEST_STUDENT_ALPHA");
        }

        [Test]
        public async Task SearchStudentsAsync_MatchesStudentNumber()
        {
            await SeedStudentAsync("TEST_STUDENT_GAMMA");

            var results = await _studentService.SearchStudentsAsync("TEST-0001");

            results.Should().ContainSingle().Which.StudentNumber.Should().Be("TEST-0001");
        }

        [Test]
        public async Task SearchStudentsAsync_BlankTermReturnsWholeRoster()
        {
            await SeedStudentAsync("TEST_STUDENT_DELTA");

            var results = await _studentService.SearchStudentsAsync("   ");

            results.Should().HaveCount(1);
        }

        #endregion

        #region Item 6 — school source of truth

        [Test]
        public void SyncDestinationFromSchoolName_FkWinsOverTheStringMirror()
        {
            var catalog = new[]
            {
                new Destination { DestinationId = 7, Name = "TEST_SCHOOL_A" },
                new Destination { DestinationId = 8, Name = "TEST_SCHOOL_B" },
            };

            var student = new Student
            {
                StudentName = "TEST_STUDENT_08",
                DestinationId = 8,
                School = "TEST_SCHOOL_A",
            };

            StudentSchoolLinker.SyncDestinationFromSchoolName(student, catalog);

            student.DestinationId.Should().Be(8);
            student.School.Should().Be("TEST_SCHOOL_B", "the FK is the school of record");
        }

        [Test]
        public void SyncDestinationFromSchoolName_ResolvesFkFromLegacyStringWhenFkIsNull()
        {
            var catalog = new[] { new Destination { DestinationId = 7, Name = "TEST_SCHOOL_A" } };
            var student = new Student { StudentName = "TEST_STUDENT_09", School = "test_school_a" };

            StudentSchoolLinker.SyncDestinationFromSchoolName(student, catalog);

            student.DestinationId.Should().Be(7);
            student.School.Should().Be("TEST_SCHOOL_A");
        }

        [Test]
        public void SyncDestinationFromSchoolName_LeavesUnmatchedLegacyStringAlone()
        {
            var catalog = new[] { new Destination { DestinationId = 7, Name = "TEST_SCHOOL_A" } };
            var student = new Student { StudentName = "TEST_STUDENT_10", School = "TEST_SCHOOL_RETIRED" };

            StudentSchoolLinker.SyncDestinationFromSchoolName(student, catalog);

            student.DestinationId.Should().BeNull();
            student.School.Should().Be("TEST_SCHOOL_RETIRED");
        }

        [Test]
        public async Task UpdateHomeGeocodeAsync_WritesCoordinatesWithoutFullIntakeValidation()
        {
            var seeded = await SeedStudentAsync();
            seeded.Zip = "nope";
            _dbContext.Students.Update(seeded);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            var ok = await _studentService.UpdateHomeGeocodeAsync(
                seeded.StudentId,
                38.0872m,
                -102.6208m,
                "ChIJ_TEST_PLACE");

            ok.Should().BeTrue();
            var reloaded = await _dbContext.Students.AsNoTracking()
                .FirstAsync(s => s.StudentId == seeded.StudentId);
            reloaded.Latitude.Should().Be(38.0872m);
            reloaded.Longitude.Should().Be(-102.6208m);
            reloaded.PlaceId.Should().Be("ChIJ_TEST_PLACE");
            reloaded.HasValidatedHomeCoordinates.Should().BeTrue();
        }

        #endregion
    }
}
