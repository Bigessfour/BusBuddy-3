using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using System.IO;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Globalization;
using System.Linq; // Added for FirstOrDefault in seeding path resolution
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service implementation for managing student transportation records
/// Provides CRUD operations and business logic for student management
/// </summary>
public class StudentService : IStudentService
{
    private static readonly ILogger Logger = Log.ForContext<StudentService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IGeocodingService? _geocodingService; // optional geocoder
    private readonly IRouteService _routeService;

    // Centralized, flexible US phone validation:
    // Accepts optional +1 country code, spaces/dots/dashes, optional parentheses around area code, and optional extensions.
    // Examples: 1234567890, 123-456-7890, (123) 456-7890, +1 (123) 456-7890 ext. 123
    private static readonly System.Text.RegularExpressions.Regex PhoneRegex =
        new System.Text.RegularExpressions.Regex(
            @"^(?:\+?1\s*(?:[.\-\s]*)?)?(?:\(\s*([2-9]\d{2})\s*\)|([2-9]\d{2}))\s*(?:[.\-\s]*)?([2-9]\d{2})\s*(?:[.\-\s]*)?(\d{4})(?:\s*(?:#|x\.?|ext\.?|extension)\s*\d+)?$",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool IsValidPhone(string? phone)
    {
        // Treat null/empty as "not provided" — valid for optional fields
        if (string.IsNullOrWhiteSpace(phone)) return true;

        // Normalize to digits-only to tolerate masks like (123) 456-7890, 123-456-7890, 123.456.7890, etc.
        // Accept 10 digits, or 11 digits when prefixed with country code '1'.
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
        {
            return true;
        }
        if (digits.Length == 11 && digits[0] == '1')
        {
            return true;
        }

        // Fallback to permissive documented pattern (kept for backward compatibility)
        return PhoneRegex.IsMatch(phone);
    }

    // Allow relaxing phone validation so format issues do not block saves.
    // Controls: BUSBUDDY_SKIP_PHONE_VALIDATION=1|true (skip), BUSBUDDY_PHONE_VALIDATION_MODE=warn|off|strict
    private static bool PhoneValidationOff()
    {
        var val = Environment.GetEnvironmentVariable("BUSBUDDY_SKIP_PHONE_VALIDATION");
        if (!string.IsNullOrEmpty(val) && (val.Equals("1", StringComparison.OrdinalIgnoreCase) || val.Equals("true", StringComparison.OrdinalIgnoreCase)))
            return true;
        var mode = Environment.GetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE");
        return !string.IsNullOrEmpty(mode) && mode.Equals("off", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PhoneValidationWarnOnly()
    {
        // Default to 'warn' so phone format issues do not block saves.
        // Supported modes via BUSBUDDY_PHONE_VALIDATION_MODE: off | warn (default) | strict
        var mode = Environment.GetEnvironmentVariable("BUSBUDDY_PHONE_VALIDATION_MODE");
        if (string.IsNullOrEmpty(mode)) return true; // default: warn-only
        return mode.Equals("warn", StringComparison.OrdinalIgnoreCase);
    }

    public StudentService(
        IBusBuddyDbContextFactory contextFactory,
        IGeocodingService? geocodingService = null,
        IRouteService? routeService = null)
    {
        _contextFactory = contextFactory;
        _geocodingService = geocodingService; // may be null in tests without DI
        // No null persist fallback: leftover name-assign always goes through RouteService.
        _routeService = routeService ?? new RouteService(contextFactory);
    }

    // Context helpers: only dispose when using the concrete runtime factory
    // This prevents disposing shared in-memory contexts used by tests.
    private (BusBuddyDbContext Ctx, bool Dispose) GetReadContext()
    {
        var ctx = _contextFactory.CreateDbContext();
        var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
        return (ctx, shouldDispose);
    }

    private (BusBuddyDbContext Ctx, bool Dispose) GetWriteContext()
    {
        var ctx = _contextFactory.CreateWriteDbContext();
        var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
        return (ctx, shouldDispose);
    }

    #region Read Operations

    /// <summary>
    /// Roster read. Includes Destination and PickupStop so <see cref="Student.Destination"/> is
    /// populated — the legacy <see cref="Student.School"/> string mirror is only a display fallback
    /// (specs/students.md: DestinationId is the school of record).
    /// </summary>
    public async Task<List<Student>> GetAllStudentsAsync()
    {
        try
        {
            Logger.Information("Retrieving all students from database");
            var (context, dispose) = GetReadContext();
            try
            {
                var students = await context.Students
                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                    .Include(s => s.Destination)
                    .Include(s => s.PickupStop)
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();

                StudentSchoolLinker.HydrateSchoolNames(students);

                // Diagnostics: verify commonly used fields are materialized
                try
                {
                    var total = students.Count;
                    int nullAddress = students.Count(s => string.IsNullOrWhiteSpace(s.HomeAddress));
                    int nullSchool = students.Count(s => string.IsNullOrWhiteSpace(s.School));
                    int nullRoutes = students.Count(s => string.IsNullOrWhiteSpace(s.AMRoute) && string.IsNullOrWhiteSpace(s.PMRoute));
                    int nullPhones = students.Count(s => string.IsNullOrWhiteSpace(s.HomePhone) && string.IsNullOrWhiteSpace(s.EmergencyPhone));

                    var sample = students.FirstOrDefault();
                    Logger.Information(
                        "Students loaded: {Total}. Nulls — Address: {NullAddress}/{Total}, School: {NullSchool}/{Total}, Routes(AM+PM both null): {NullRoutes}/{Total}, Phones(Home+Emergency both null): {NullPhones}/{Total}. Sample: Id={Id}, Name={Name}, School={SampleSchool}, AM={AM}, PM={PM}",
                        total, nullAddress, total, nullSchool, total, nullRoutes, total, nullPhones, total,
                        sample?.StudentId, sample?.StudentName, sample?.School, sample?.AMRoute, sample?.PMRoute);
                }
                catch (Exception diagEx)
                {
                    Logger.Debug(diagEx, "Student field diagnostics logging failed");
                }

                return students;
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving all students");
            throw;
        }
    }

    public async Task<Student?> GetStudentByIdAsync(int studentId)
    {
        try
        {
            Logger.Information("Retrieving student with ID: {StudentId}", studentId);
            var (context, dispose) = GetReadContext();
            try
            {
                var student = await context.Students
                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                    .Include(s => s.Destination)
                    .Include(s => s.PickupStop)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);

                if (student != null)
                {
                    StudentSchoolLinker.HydrateSchoolName(student);
                }

                return student;
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving student with ID: {StudentId}", studentId);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsByGradeAsync(string grade)
    {
        try
        {
            Logger.Information("Retrieving students in grade: {Grade}", grade);
            var (context, dispose) = GetReadContext();
            try
            {
                return await context.Students
                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                    .Where(s => s.Grade == grade)
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving students by grade: {Grade}", grade);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsByRouteAsync(int routeId)
    {
        try
        {
            if (routeId <= 0)
            {
                return [];
            }

            Logger.Information("Retrieving students on route {RouteId}", routeId);
            var (context, dispose) = GetReadContext();
            try
            {
                var route = await context.Routes.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RouteId == routeId);
                if (route is null)
                {
                    return [];
                }

                return await context.Students
                    .AsNoTracking()
                    .WhereOnRoute(route)
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving students by route: {RouteId}", routeId);
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsByRouteAsync(string routeName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(routeName))
            {
                return [];
            }

            Logger.Information("Retrieving students on route: {RouteName}", routeName);
            int? uniqueId;
            var (context, dispose) = GetReadContext();
            try
            {
                var catalog = await context.Routes.AsNoTracking()
                    .Select(r => new { r.RouteId, r.RouteName })
                    .ToListAsync();
                uniqueId = StudentRouteAssignment.UniqueIdForName(
                    catalog.Select(r => (r.RouteId, (string?)r.RouteName)),
                    routeName);
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }

            // 0 or 2+ matches: a shared name is not a key (two dated "North Elementary" runs).
            if (uniqueId is null)
            {
                return [];
            }

            return await GetStudentsByRouteAsync(uniqueId.Value);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving students by route: {RouteName}", routeName);
            throw;
        }
    }

    public async Task<List<Student>> GetActiveStudentsAsync()
    {
        try
        {
            Logger.Information("Retrieving active students");
            var (context, dispose) = GetReadContext();
            try
            {
                return await context.Students
                    .AsNoTracking()
                    .Include(s => s.Destination)
                    .Include(s => s.PickupStop)
                    .Where(s => s.Active)
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving active students");
            throw;
        }
    }

    public async Task<List<Student>> GetStudentsBySchoolAsync(string school)
    {
        try
        {
            Logger.Information("Retrieving students from school: {School}", school);
            var (context, dispose) = GetReadContext();
            try
            {
                // Match on the Destination FK first (source of truth) and fall back to the legacy
                // School string so rows written before DestinationId existed still resolve.
                return await context.Students
                    .AsNoTracking()
                    .Include(s => s.Destination)
                    .Where(s => (s.Destination != null && s.Destination.Name == school)
                                || (s.DestinationId == null && s.School == school))
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving students by school: {School}", school);
            throw;
        }
    }

    public async Task<List<Student>> SearchStudentsAsync(string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(searchTerm);

        try
        {
            Logger.Information("Searching students with term: {SearchTerm}", searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllStudentsAsync();
            }

            // LOWER() on both sides keeps the match case-insensitive on Npgsql, whose LIKE is
            // case-sensitive, without resorting to the Postgres-only ILIKE.
            var pattern = $"%{searchTerm.ToLowerInvariant()}%";
            var (context, dispose) = GetReadContext();
            try
            {
                // string.Contains(term, StringComparison) has no SQL translation — EF throws on Npgsql.
                // Use LIKE on the server and fall back to in-process matching for the InMemory provider,
                // which does not implement EF.Functions.Like. Mirrors DriverService.SearchDriversAsync.
                var isInMemory = context.Database.ProviderName != null &&
                                 context.Database.ProviderName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

                if (isInMemory)
                {
                    var all = await context.Students
                        .AsNoTracking()
                        .Include(s => s.Destination)
                        .Include(s => s.PickupStop)
                        .ToListAsync();

                    return all
                        .Where(s =>
                            (!string.IsNullOrEmpty(s.StudentName) && s.StudentName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(s.StudentNumber) && s.StudentNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(s => s.StudentName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                // CA1311: ToLowerInvariant has no SQL translation; ToLower() is the form EF maps to
                // the database LOWER() function, which is what runs here.
#pragma warning disable CA1311
                return await context.Students
                    .AsNoTracking()
                    .Include(s => s.Destination)
                    .Include(s => s.PickupStop)
                    .Where(s => EF.Functions.Like(s.StudentName.ToLower(), pattern) ||
                               (s.StudentNumber != null && EF.Functions.Like(s.StudentNumber.ToLower(), pattern)))
#pragma warning restore CA1311
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error searching students with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    #endregion

    #region Write Operations

    public async Task<Student> AddStudentAsync(Student student)
    {
        try
        {
            Logger.Information("Adding new student: {StudentName}", student.StudentName);

            StudentRecordNormalizer.NormalizeForPersistence(student);

            // Validate student data
            var validationErrors = await ValidateStudentAsync(student);
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Student validation failed: {string.Join(", ", validationErrors)}");
            }

            // Set default values
            if (student.EnrollmentDate == null)
            {
                student.EnrollmentDate = DateTime.UtcNow.Date;
            }

            // Geocode on add when coordinates are not provided and a geocoder is available
            if (_geocodingService != null && (!student.Latitude.HasValue || !student.Longitude.HasValue))
            {
                try
                {
                    var geo = await _geocodingService.GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip);
                    if (geo.HasValue)
                    {
                        student.Latitude = (decimal)geo.Value.latitude;
                        student.Longitude = (decimal)geo.Value.longitude;
                    }
                }
                catch (Exception geoEx)
                {
                    Logger.Debug(geoEx, "Geocoding failed for student {Name}; proceeding without coordinates", student.StudentName);
                }
            }

            var (context, dispose) = GetWriteContext();
            try
            {
                context.Students.Add(student);
                student.AmRouteId = await ResolveRouteIdByNameAsync(context, student.AMRoute);
                student.PmRouteId = await ResolveRouteIdByNameAsync(context, student.PMRoute);
                await context.SaveChangesAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }

            Logger.Information("Successfully added student with ID: {StudentId}", student.StudentId);
            return student;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error adding student: {StudentName}", student.StudentName);
            throw;
        }
    }

    public async Task<bool> UpdateStudentAsync(Student student)
    {
        try
        {
            Logger.Information("Updating student with ID: {StudentId}", student.StudentId);

            StudentRecordNormalizer.NormalizeForPersistence(student);

            // Validate student data
            var validationErrors = await ValidateStudentAsync(student);
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Student validation failed: {string.Join(", ", validationErrors)}");
            }

            // Geocode if coordinates missing and address present
            if (_geocodingService != null && (!student.Latitude.HasValue || !student.Longitude.HasValue))
            {
                try
                {
                    var geo = await _geocodingService.GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip);
                    if (geo.HasValue)
                    {
                        student.Latitude = (decimal)geo.Value.latitude;
                        student.Longitude = (decimal)geo.Value.longitude;
                    }
                }
                catch (Exception geoEx)
                {
                    Logger.Debug(geoEx, "Geocoding failed for student {Id}; proceeding without coordinates", student.StudentId);
                }
            }

            var (context, dispose) = GetWriteContext();
            int result;
            try
            {
                context.Students.Update(student);
                student.AmRouteId = await ResolveRouteIdByNameAsync(context, student.AMRoute);
                student.PmRouteId = await ResolveRouteIdByNameAsync(context, student.PMRoute);
                result = await context.SaveChangesAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }

            var success = result > 0;
            if (success)
            {
                Logger.Information("Successfully updated student: {StudentName}", student.StudentName);
            }
            else
            {
                Logger.Warning("No changes were made when updating student: {StudentId}", student.StudentId);
            }

            return success;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error updating student with ID: {StudentId}", student.StudentId);
            throw;
        }
    }

    public async Task<bool> UpdateHomeGeocodeAsync(
        int studentId,
        decimal? latitude,
        decimal? longitude,
        string? placeId)
    {
        if (studentId <= 0)
        {
            return false;
        }

        var (context, dispose) = GetWriteContext();
        try
        {
            var row = await context.Students
                .AsTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId)
                .ConfigureAwait(false);
            if (row is null)
            {
                return false;
            }

            row.Latitude = latitude;
            row.Longitude = longitude;
            if (!string.IsNullOrWhiteSpace(placeId))
            {
                row.PlaceId = placeId;
            }
            else if (!LocationCoordinate.IsValidated(latitude, longitude))
            {
                row.PlaceId = null;
            }

            row.UpdatedDate = DateTime.UtcNow;
            if (LocationCoordinate.IsValidated(latitude, longitude))
            {
                await SyncPublishedHomeStopsAsync(context, row, studentId).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
            Logger.Information(
                "Home geocode persisted StudentId={StudentId} HasCoords={HasCoords}",
                studentId,
                latitude.HasValue && longitude.HasValue);
            // Zero rows changed still means the student exists (confirm-without-nudge).
            return true;
        }
        finally
        {
            if (dispose)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task SyncPublishedHomeStopsAsync(
        BusBuddyDbContext context,
        Student student,
        int studentId)
    {
        var routeIds = new[] { student.AmRouteId, student.PmRouteId }
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (routeIds.Count == 0)
        {
            return;
        }

        var stops = await context.RouteStops
            .AsTracking()
            .Where(s => routeIds.Contains(s.RouteId))
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var stop in stops)
        {
            if (!NotesNameStudent(stop.Notes, studentId))
            {
                continue;
            }

            stop.Latitude = student.Latitude;
            stop.Longitude = student.Longitude;
            stop.UpdatedDate = DateTime.UtcNow;
        }
    }

    internal static bool NotesNameStudent(string? notes, int studentId)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        if (notes.Equals($"StudentId={studentId}", StringComparison.Ordinal))
        {
            return true;
        }

        const string prefix = "StudentIds=";
        if (!notes.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return notes[prefix.Length..]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(studentId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Archives a student who may return. The row stays on the roster with Active=false.
    /// </summary>
    public Task<bool> ArchiveStudentAsync(int studentId) =>
        UpdateStudentActiveStatusAsync(studentId, isActive: false);

    /// <summary>Returns an archived student to active service.</summary>
    public Task<bool> RestoreStudentAsync(int studentId) =>
        UpdateStudentActiveStatusAsync(studentId, isActive: true);

    /// <summary>
    /// Permanently removes a student after a clerk-chosen reason. specs/students.md: Mistake, Moved,
    /// or Not attending. Related assignment rows are removed explicitly (FKs stay Restrict). The
    /// deletion log and Serilog entry do not include name, address, or guardian data.
    /// </summary>
    public async Task<bool> DeleteStudentAsync(int studentId, StudentDeletionReason reason, string? notes = null)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "A deletion reason is required.");
        }

        try
        {
            var (context, dispose) = GetWriteContext();
            try
            {
                var student = await context.Students.FindAsync(studentId);
                if (student == null)
                {
                    Logger.Warning("Student with ID {StudentId} not found for deletion", studentId);
                    return false;
                }

                var schedules = await context.StudentSchedules
                    .Where(x => x.StudentId == studentId)
                    .ToListAsync();
                var transfers = await context.StudentSchoolTransfers
                    .Where(x => x.StudentId == studentId)
                    .ToListAsync();
                var exceptions = await context.RouteRiderExceptions
                    .Where(x => x.StudentId == studentId)
                    .ToListAsync();

                var log = new StudentDeletionLog
                {
                    StudentId = student.StudentId,
                    StudentNumber = student.StudentNumber,
                    SchoolYear = student.SchoolYear,
                    Reason = reason.ToString(),
                    Notes = TruncateDeletionNotes(notes),
                    WasActive = student.Active,
                    ScheduleCount = schedules.Count,
                    TransferCount = transfers.Count,
                    RiderExceptionCount = exceptions.Count,
                    DeletedUtc = DateTime.UtcNow,
                };

                context.StudentSchedules.RemoveRange(schedules);
                context.StudentSchoolTransfers.RemoveRange(transfers);
                context.RouteRiderExceptions.RemoveRange(exceptions);
                context.StudentDeletionLogs.Add(log);
                context.Students.Remove(student);

                var result = await context.SaveChangesAsync();
                if (result > 0)
                {
                    WriteStudentDeletionLog(log);
                }

                return result > 0;
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error deleting student record {StudentId}", studentId);
            throw;
        }
    }

    /// <summary>
    /// Structured deletion log. StudentId and StudentNumber only — no name, address, or guardian.
    /// </summary>
    internal static void WriteStudentDeletionLog(StudentDeletionLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Logger.Warning(
            "Student deleted StudentId={StudentId} StudentNumber={StudentNumber} Reason={Reason} Notes={Notes} WasActive={WasActive} Schedules={ScheduleCount} Transfers={TransferCount} RiderExceptions={RiderExceptionCount}",
            entry.StudentId,
            entry.StudentNumber,
            entry.Reason,
            entry.Notes,
            entry.WasActive,
            entry.ScheduleCount,
            entry.TransferCount,
            entry.RiderExceptionCount);
    }

    private static string? TruncateDeletionNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var trimmed = notes.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }

    #endregion

    #region Validation and Business Logic

    public async Task<List<string>> ValidateStudentAsync(Student student)
    {
        var errors = new List<string>();

        try
        {
            // Required field validation
            if (string.IsNullOrWhiteSpace(student.StudentName))
            {
                errors.Add("Student name is required");
            }

            // Create a context that will be disposed at the end of this method,
            // since the validation results are returned as a new list (not dependent on the context)
            var (context, dispose) = GetReadContext();
            try
            {

                // Student number uniqueness check (if provided)
                if (!string.IsNullOrWhiteSpace(student.StudentNumber))
                {
                    var existingStudent = await context.Students
                        .Where(s => s.StudentNumber == student.StudentNumber && s.StudentId != student.StudentId)
                        .FirstOrDefaultAsync();

                    if (existingStudent != null)
                    {
                        errors.Add($"Student number '{student.StudentNumber}' is already in use");
                    }
                }

                // Grade validation
                if (!string.IsNullOrWhiteSpace(student.Grade))
                {
                    if (!StudentGradeCatalog.IsValid(student.Grade))
                    {
                        errors.Add("Invalid grade level");
                    }
                }

                // Phone number format validation — configurable
                if (!string.IsNullOrWhiteSpace(student.HomePhone) && !IsValidPhone(student.HomePhone))
                {
                    if (PhoneValidationOff() || PhoneValidationWarnOnly())
                    {
                        Logger.Warning("Home phone did not match validation but proceeding due to mode — Phone={Phone}", student.HomePhone);
                    }
                    else
                    {
                        errors.Add("Invalid home phone number format");
                    }
                }

                if (!string.IsNullOrWhiteSpace(student.EmergencyPhone) && !IsValidPhone(student.EmergencyPhone))
                {
                    if (PhoneValidationOff() || PhoneValidationWarnOnly())
                    {
                        Logger.Warning("Emergency phone did not match validation but proceeding due to mode — Phone={Phone}", student.EmergencyPhone);
                    }
                    else
                    {
                        errors.Add("Invalid emergency phone number format");
                    }
                }

                // State validation
                if (!string.IsNullOrWhiteSpace(student.State))
                {
                    if (student.State.Length != 2)
                    {
                        errors.Add("State must be a 2-letter abbreviation");
                    }
                }

                // ZIP code validation
                if (!string.IsNullOrWhiteSpace(student.Zip))
                {
                    var zipPattern = @"^\d{5}(-\d{4})?$";
                    if (!System.Text.RegularExpressions.Regex.IsMatch(student.Zip, zipPattern))
                    {
                        errors.Add("Invalid ZIP code format");
                    }
                }

                // Route validation (if routes exist in database)
                if (!string.IsNullOrWhiteSpace(student.AMRoute))
                {
                    try
                    {
                        var amRouteExists = await context.Routes.AnyAsync(r => r.RouteName == student.AMRoute);
                        if (!amRouteExists)
                        {
                            errors.Add($"AM Route '{student.AMRoute}' does not exist");
                        }
                    }
                    catch (Exception ex)
                    {
                        DatabaseUserMessage.LogFailure(Logger, ex, "Error validating AM route: {AMRoute}", student.AMRoute);
                        errors.Add($"AM Route '{student.AMRoute}' does not exist");
                    }
                }

                if (!string.IsNullOrWhiteSpace(student.PMRoute))
                {
                    try
                    {
                        var pmRouteExists = await context.Routes.AnyAsync(r => r.RouteName == student.PMRoute);
                        if (!pmRouteExists)
                        {
                            errors.Add($"PM Route '{student.PMRoute}' does not exist");
                        }
                    }
                    catch (Exception ex)
                    {
                        DatabaseUserMessage.LogFailure(Logger, ex, "Error validating PM route: {PMRoute}", student.PMRoute);
                        errors.Add($"PM Route '{student.PMRoute}' does not exist");
                    }
                }
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error during basic student validation");
            errors.Add("Validation error occurred");
        }

        return errors;
    }

    #endregion

    #region Statistics and Reporting

    public async Task<Dictionary<string, int>> GetStudentStatisticsAsync()
    {
        try
        {
            Logger.Information("Calculating student statistics");

            var (context, dispose) = GetReadContext();
            try
            {
                var stats = new Dictionary<string, int>
                {
                    ["TotalStudents"] = await context.Students.CountAsync(),
                    ["ActiveStudents"] = await context.Students.CountAsync(s => s.Active),
                    ["InactiveStudents"] = await context.Students.CountAsync(s => !s.Active),
                    ["StudentsWithRoutes"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute)),
                    ["StudentsWithoutRoutes"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute))
                };

                // Grade level counts
                var gradeCounts = await context.Students
                    .Where(s => !string.IsNullOrEmpty(s.Grade))
                    .GroupBy(s => s.Grade)
                    .Select(g => new { Grade = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var gradeCount in gradeCounts)
                {
                    stats[$"Grade_{gradeCount.Grade}"] = gradeCount.Count;
                }

                return stats;
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error calculating student statistics");
            throw;
        }
    }

    /// <summary>
    /// Active Home-pickup students with validated coordinates inside a walk radius.
    /// Hints a shared catalog stop; does not change PickupStopId. Logs counts only.
    /// </summary>
    public async Task<IReadOnlyList<Student>> GetNearbyHomePickupStudentsAsync(
        double latitude,
        double longitude,
        double maxMeters,
        int? excludeStudentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!LocationCoordinate.IsValidated(latitude, longitude) || maxMeters <= 0)
        {
            return Array.Empty<Student>();
        }

        var (context, dispose) = GetReadContext();
        try
        {
            var rows = await context.Students
                .AsNoTracking()
                .Where(s => s.Active && s.PickupStopId == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var nearby = new List<Student>();
            foreach (var row in rows)
            {
                if (excludeStudentId is int skip && row.StudentId == skip)
                {
                    continue;
                }

                if (StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(row))
                {
                    continue;
                }

                if (!LocationCoordinate.IsValidated(row.Latitude, row.Longitude))
                {
                    continue;
                }

                var meters = RoutePacker.HaversineMiles(
                    latitude,
                    longitude,
                    (double)row.Latitude!,
                    (double)row.Longitude!) * 1609.344;
                if (meters <= maxMeters)
                {
                    nearby.Add(row);
                }
            }

            Logger.Information(
                "Nearby home pickups Count={Count} RadiusM={RadiusM} ExcludeStudentId={ExcludeStudentId}",
                nearby.Count,
                maxMeters,
                excludeStudentId);
            return nearby;
        }
        finally
        {
            if (dispose)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The "incomplete records" surface required by specs/students.md ("Unvalidated addresses show as
    /// incomplete"). Covers both missing intake fields and an address that has never produced
    /// coordinates — the latter is what keeps a student off the map.
    /// </summary>
    public async Task<List<Student>> GetStudentsWithMissingInfoAsync()
    {
        try
        {
            Logger.Information("Finding students with missing required information");

            var (context, dispose) = GetReadContext();
            try
            {
                // Mirrors Student.HasValidatedHomeCoordinates / LocationCoordinate.IsValidated in SQL:
                // null, 0,0, and the US-centroid placeholder (39.8283, -98.5795 ± 0.01) are all "no pin".
                const decimal zeroEpsilon = 0.000001m;
                const decimal centroidLatLow = (decimal)LocationCoordinate.UsCentroidLatitude - 0.01m;
                const decimal centroidLatHigh = (decimal)LocationCoordinate.UsCentroidLatitude + 0.01m;
                const decimal centroidLonLow = (decimal)LocationCoordinate.UsCentroidLongitude - 0.01m;
                const decimal centroidLonHigh = (decimal)LocationCoordinate.UsCentroidLongitude + 0.01m;

                return await context.Students
                    .AsNoTracking()
                    .Include(s => s.Destination)
                    // Mirrors Student.IsIntakeIncomplete in SQL (that property is [NotMapped]).
                    // Keep the two in step: a record the grid labels incomplete must be reachable here.
                    .Where(s => s.Active &&
                               (string.IsNullOrEmpty(s.ParentGuardian) ||
                                string.IsNullOrEmpty(s.EmergencyPhone) ||
                                string.IsNullOrEmpty(s.HomeAddress) ||
                                string.IsNullOrEmpty(s.Grade) ||
                                string.IsNullOrEmpty(s.SchoolYear) ||
                                s.DestinationId == null ||
                                (!s.RidesAm && !s.RidesPm) ||
                                (s.PickupStopId == null &&
                                 (s.Latitude == null ||
                                  s.Longitude == null ||
                                  s.Latitude < -90m || s.Latitude > 90m ||
                                  s.Longitude < -180m || s.Longitude > 180m ||
                                  (s.Latitude > -zeroEpsilon && s.Latitude < zeroEpsilon &&
                                   s.Longitude > -zeroEpsilon && s.Longitude < zeroEpsilon) ||
                                  (s.Latitude > centroidLatLow && s.Latitude < centroidLatHigh &&
                                   s.Longitude > centroidLonLow && s.Longitude < centroidLonHigh)))))
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error finding students with missing information");
            throw;
        }
    }

    #endregion

    #region Route Assignment

    /// <summary>
    /// Resolves a route name to its id for <see cref="Student.AmRouteId"/> / <see cref="Student.PmRouteId"/>.
    /// Returns null when the name is blank, matches no route, or matches more than one — the caller stores
    /// the name either way, so an unresolved key degrades to the pre-key behaviour rather than mis-assigning
    /// the rider.
    /// </summary>
    private static async Task<int?> ResolveRouteIdByNameAsync(BusBuddyDbContext context, string? routeName)
    {
        if (string.IsNullOrWhiteSpace(routeName))
        {
            return null;
        }

        var nameLower = routeName.Trim().ToLowerInvariant();

        // CA1311/CA1862: ToLowerInvariant and StringComparison overloads have no SQL translation;
        // ToLower() is the form EF maps to the database LOWER() function, which is what runs here.
#pragma warning disable CA1311, CA1862
        var matches = await context.Routes
            .Where(r => r.RouteName.ToLower() == nameLower)
            .Select(r => r.RouteId)
            .Take(2)
            .ToListAsync();
#pragma warning restore CA1311, CA1862

        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<bool> AssignStudentToRouteAsync(int studentId, string? amRoute, string? pmRoute)
    {
        try
        {
            Logger.Information("Assigning student {StudentId} to routes - AM: {AMRoute}, PM: {PMRoute}",
                studentId, amRoute, pmRoute);

            int? amId = null;
            int? pmId = null;
            var (context, dispose) = GetReadContext();
            try
            {
                if (!string.IsNullOrWhiteSpace(amRoute))
                {
                    amId = await ResolveRouteIdByNameAsync(context, amRoute);
                    if (amId is null)
                    {
                        Logger.Warning(
                            "AM route name {AMRoute} is missing or not unique — fail closed, no name-only write",
                            amRoute);
                        return false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(pmRoute))
                {
                    pmId = await ResolveRouteIdByNameAsync(context, pmRoute);
                    if (pmId is null)
                    {
                        Logger.Warning(
                            "PM route name {PMRoute} is missing or not unique — fail closed, no name-only write",
                            pmRoute);
                        return false;
                    }
                }
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }

            if (amId is int amRouteId)
            {
                var amResult = await _routeService.AssignStudentToRouteAsync(
                    studentId, amRouteId, RouteTimeSlot.AM);
                if (!amResult.IsSuccess)
                {
                    Logger.Warning(
                        "RouteService AM assign failed for student {StudentId}: {Error}",
                        studentId, amResult.Error);
                    return false;
                }
            }

            if (pmId is int pmRouteId)
            {
                var pmResult = await _routeService.AssignStudentToRouteAsync(
                    studentId, pmRouteId, RouteTimeSlot.PM);
                if (!pmResult.IsSuccess)
                {
                    Logger.Warning(
                        "RouteService PM assign failed for student {StudentId}: {Error}",
                        studentId, pmResult.Error);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning routes for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> UpdateStudentActiveStatusAsync(int studentId, bool isActive)
    {
        try
        {
            Logger.Information("Updating active status for student {StudentId} to {IsActive}", studentId, isActive);

            var (context, dispose) = GetWriteContext();
            bool success;
            string? studentName = null;
            try
            {
                // BusBuddyDbContext defaults to NoTracking, so a Find-then-mutate would silently save
                // nothing unless the caller happened to hand us a TrackAll context. Ask for tracking.
                var student = await context.Students
                    .AsTracking()
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);
                if (student == null)
                {
                    Logger.Warning("Student with ID {StudentId} not found", studentId);
                    return false;
                }

                studentName = student.StudentName;
                student.Active = isActive;
                student.UpdatedDate = DateTime.UtcNow;
                var result = await context.SaveChangesAsync();
                success = result > 0;
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }

            if (success)
            {
                Logger.Information("Successfully updated active status for student: {StudentName}", studentName ?? "(unknown)");
            }

            return success;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error updating active status for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<RouteAssignmentResult> AssignStudentsToRoutesAsync(BusBuddyDbContext context, IEnumerable<Student> students, IEnumerable<Route> routes, BusService busService)
    {
        var updatedStudents = new List<Student>();
        var newAssignments = new List<RouteAssignment>();

        foreach (var student in students)
        {
            // Example: If address contains "east of Hwy 287", assign East Route
            var route = routes.FirstOrDefault(r => student.HomeAddress != null && r.Boundaries != null && IsAddressInRouteBoundary(student.HomeAddress, r.Boundaries));
            if (route == null)
            {
                // Log and skip if no route matches
                continue;
            }

            // Find bus for route
            var bus = await context.Buses.FirstOrDefaultAsync(v => v.Make == route.RouteName || v.BusNumber == route.RouteName || v.BusNumber == route.RouteName.Replace(" Route", ""));
            if (bus == null)
            {
                continue;
            }

            // Check bus capacity
            var assignedCount = await busService.GetAssignedStudentCountAsync(context, bus.BusId);
            if (assignedCount >= bus.SeatingCapacity)
            {
                continue;
            }

            // Create and persist assignment (ensure PK is generated)
            var assignment = new RouteAssignment
            {
                RouteId = route.RouteId,
                VehicleId = bus.BusId,
                AssignmentDate = System.DateTime.Today
            };
            await context.RouteAssignments.AddAsync(assignment);
            await context.SaveChangesAsync();
            newAssignments.Add(assignment);

            student.RouteAssignmentId = assignment.RouteAssignmentId;
            student.BusStop = "Assigned by address";
            context.Students.Update(student);
            await context.SaveChangesAsync();
            updatedStudents.Add(student);
        }

        return new RouteAssignmentResult
        {
            UpdatedStudents = updatedStudents,
            NewAssignments = newAssignments
        };
    }

    private bool IsAddressInRouteBoundary(string address, string boundaries)
    {
        // Simple example: match keywords (expand as needed)
        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(boundaries)) { return false; }
        address = address.ToLower(System.Globalization.CultureInfo.InvariantCulture);
        boundaries = boundaries.ToLower(System.Globalization.CultureInfo.InvariantCulture);
        if (boundaries.Contains("east") && address.Contains("east")) { return true; }
        if (boundaries.Contains("west") && address.Contains("west")) { return true; }
        if (boundaries.Contains("south") && address.Contains("south")) { return true; }
        if (boundaries.Contains("north") && address.Contains("north")) { return true; }
        // Add more logic as needed
        return false;
    }

    public class RouteAssignmentResult
    {
        public List<Student> UpdatedStudents { get; set; } = new();
        public List<RouteAssignment> NewAssignments { get; set; } = new();
    }

    #endregion

    #region Address and Contact Management

    public async Task<bool> UpdateStudentAddressAsync(int studentId, string homeAddress, string city, string state, string zip)
    {
        try
        {
            Logger.Information("Updating address information for student {StudentId}", studentId);

            // Validate address format
            var addressValidation = ValidateAddress(homeAddress, city, state, zip);
            if (!addressValidation.IsValid)
            {
                throw new ArgumentException($"Address validation failed: {addressValidation.ErrorMessage}");
            }

            var (context, dispose) = GetWriteContext();
            bool success;
            string? studentName = null;
            try
            {
                var student = await context.Students.FindAsync(studentId);
                if (student == null)
                {
                    Logger.Warning("Student with ID {StudentId} not found", studentId);
                    return false;
                }

                studentName = student.StudentName;
                student.HomeAddress = homeAddress;
                student.City = city;
                student.State = state;
                student.Zip = zip;
                // Explicit, because the context may have been created with NoTracking.
                context.Entry(student).State = EntityState.Modified;

                var result = await context.SaveChangesAsync();
                success = result > 0;
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }

            if (success)
            {
                Logger.Information("Successfully updated address for student: {StudentName}", studentName ?? "(unknown)");
            }

            return success;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error updating address for student {StudentId}", studentId);
            throw;
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidateAddress(string address, string city, string state, string zip)
    {
        // Implement basic address validation
        if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Address cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return (false, "City cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(state) || state.Length != 2)
        {
            return (false, "State must be a 2-letter abbreviation");
        }

        var zipPattern = @"^\d{5}(-\d{4})?$";
        if (string.IsNullOrWhiteSpace(zip) || !System.Text.RegularExpressions.Regex.IsMatch(zip, zipPattern))
        {
            return (false, "Invalid ZIP code format");
        }

        // In a real implementation, you might also validate against an address verification service
        // For now, we'll just return valid if basic checks pass
        return (true, null);
    }

    #endregion

    #region Export

    public async Task<string> ExportStudentsToCsvAsync()
    {
        try
        {
            var students = await GetAllStudentsAsync();
            return StudentCsvExporter.ToCsv(students);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error exporting students to CSV");
            throw;
        }
    }

    #endregion

    #region DEBUG Instrumentation

#if DEBUG
    public Task<Dictionary<string, object>> GetStudentDiagnosticsAsync(int studentId) =>
        StudentDiagnostics.GetStudentDiagnosticsAsync(_contextFactory, studentId);

    public Task<Dictionary<string, object>> GetStudentOperationMetricsAsync() =>
        StudentDiagnostics.GetStudentOperationMetricsAsync(_contextFactory);
#endif

    #endregion
}
