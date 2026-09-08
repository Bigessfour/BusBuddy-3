using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using System.IO;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using System.Linq; // Added for FirstOrDefault in seeding path resolution
using BusBuddy.Core.Services.Interfaces;

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

    public StudentService(IBusBuddyDbContextFactory contextFactory, IGeocodingService? geocodingService = null)
    {
        _contextFactory = contextFactory;
        _geocodingService = geocodingService; // may be null in tests without DI
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

    public async Task<List<Student>> GetStudentsByRouteAsync(string routeName)
    {
        try
        {
            Logger.Information("Retrieving students on route: {RouteName}", routeName);
            var (context, dispose) = GetReadContext();
            try
            {
                return await context.Students
                    .AsNoTracking()
                    .Where(s => s.AMRoute == routeName || s.PMRoute == routeName)
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

    public async Task<List<Student>> GetStudentsForRouteAsync(BusBuddyDbContext context, int routeId)
    {
        try
        {
            Logger.Information("Retrieving students for route ID: {RouteId}", routeId);
            // Find route name for the given routeId
            var route = await context.Routes.FindAsync(routeId);
            if (route == null || string.IsNullOrEmpty(route.RouteName))
            {
                return new List<Student>();
            }

            var routeName = route.RouteName;
            return await context.Students
                .Where(s => s.AMRoute == routeName || s.PMRoute == routeName)
                .OrderBy(s => s.StudentName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving students for route ID: {RouteId}", routeId);
            return new List<Student>();
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

            row.UpdatedDate = DateTime.UtcNow;
            var saved = await context.SaveChangesAsync().ConfigureAwait(false);
            Logger.Information(
                "Home geocode persisted StudentId={StudentId} HasCoords={HasCoords}",
                studentId,
                latitude.HasValue && longitude.HasValue);
            return saved > 0;
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
    /// Archives a student — the spec-sanctioned way to end service. specs/students.md: "MUST NOT
    /// delete a student to end service. Archive or set inactive so history and route versions remain."
    /// </summary>
    public Task<bool> ArchiveStudentAsync(int studentId) =>
        UpdateStudentActiveStatusAsync(studentId, isActive: false);

    /// <summary>Returns an archived student to active service.</summary>
    public Task<bool> RestoreStudentAsync(int studentId) =>
        UpdateStudentActiveStatusAsync(studentId, isActive: true);

    /// <summary>
    /// Permanently removes a student row. Not part of the clerk flow — ending service is
    /// <see cref="ArchiveStudentAsync"/>. This exists only for data-entry mistakes (a row created in
    /// error that has no history) and refuses to run for anything else.
    /// </summary>
    /// <param name="studentId">Student to purge.</param>
    /// <param name="reason">Operator-supplied justification; recorded in the log. Required.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the student is still active, or has schedule/transfer history. Archive instead.
    /// </exception>
    public async Task<bool> PurgeStudentRecordAsync(int studentId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A purge reason is required.", nameof(reason));
        }

        try
        {
            var (context, dispose) = GetWriteContext();
            try
            {
                var student = await context.Students.FindAsync(studentId);
                if (student == null)
                {
                    Logger.Warning("Student with ID {StudentId} not found for purge", studentId);
                    return false;
                }

                // Gate 1: an active student is in service. Ending service is an archive, never a delete.
                if (student.Active)
                {
                    throw new InvalidOperationException(
                        $"Student {studentId} is active. Archive the student instead of deleting the record.");
                }

                // Gate 2: history must outlive the row. specs/students.md requires history and route
                // versions to remain, so a student that has any is not purgeable.
                var scheduleCount = await context.StudentSchedules.CountAsync(x => x.StudentId == studentId);
                var transferCount = await context.StudentSchoolTransfers.CountAsync(x => x.StudentId == studentId);
                if (scheduleCount > 0 || transferCount > 0)
                {
                    throw new InvalidOperationException(
                        $"Student {studentId} has {scheduleCount} schedule and {transferCount} transfer history rows. " +
                        "Archived students with history cannot be purged.");
                }

                Logger.Warning(
                    "Purging student record {StudentId} — reason: {Reason}", studentId, reason);

                context.Students.Remove(student);
                var result = await context.SaveChangesAsync();
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
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error purging student record {StudentId}", studentId);
            throw;
        }
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

    public async Task<bool> AssignStudentToRouteAsync(int studentId, string? amRoute, string? pmRoute)
    {
        try
        {
            Logger.Information("Assigning student {StudentId} to routes - AM: {AMRoute}, PM: {PMRoute}",
                studentId, amRoute, pmRoute);
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
                student.AMRoute = amRoute;
                student.PMRoute = pmRoute;
                // Explicitly mark as modified to ensure changes are persisted even if detection is off
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
                Logger.Information("Successfully assigned routes for student: {StudentName}", studentName ?? "(unknown)");
            }

            return success;
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
            Logger.Information("Exporting students to CSV format");

            var students = await GetAllStudentsAsync();
            var csv = new StringBuilder();

            // CSV Header
            csv.AppendLine("Student ID,Student Number,Student Name,Grade,School,Home Address,City,State,ZIP," +
                          "Home Phone,Parent/Guardian,Emergency Phone,AM Route,PM Route,Bus Stop," +
                          "Medical Notes,Transportation Notes,Active,Enrollment Date");

            // CSV Data
            foreach (var student in students)
            {
                csv.AppendLine($"{student.StudentId}," +
                              $"\"{student.StudentNumber ?? ""}\"," +
                              $"\"{student.StudentName}\"," +
                              $"\"{student.Grade ?? ""}\"," +
                              $"\"{student.School ?? ""}\"," +
                              $"\"{student.HomeAddress ?? ""}\"," +
                              $"\"{student.City ?? ""}\"," +
                              $"\"{student.State ?? ""}\"," +
                              $"\"{student.Zip ?? ""}\"," +
                              $"\"{student.HomePhone ?? ""}\"," +
                              $"\"{student.ParentGuardian ?? ""}\"," +
                              $"\"{student.EmergencyPhone ?? ""}\"," +
                              $"\"{student.AMRoute ?? ""}\"," +
                              $"\"{student.PMRoute ?? ""}\"," +
                              $"\"{student.BusStop ?? ""}\"," +
                              $"\"{student.MedicalNotes ?? ""}\"," +
                              $"\"{student.TransportationNotes ?? ""}\"," +
                              $"{student.Active}," +
                              $"{student.EnrollmentDate?.ToString("yyyy-MM-dd") ?? ""}");
            }

            Logger.Information("Successfully exported {Count} students to CSV", students.Count);
            return csv.ToString();
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
    /// <summary>
    /// Provides detailed diagnostic information about a student record
    /// Only available in DEBUG builds
    /// </summary>
    public async Task<Dictionary<string, object>> GetStudentDiagnosticsAsync(int studentId)
    {
        try
        {
            Logger.Debug("Retrieving diagnostic information for student {StudentId}", studentId);

            using var context = _contextFactory.CreateDbContext();
            var student = await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                Logger.Warning("Student with ID {StudentId} not found for diagnostics", studentId);
                return new Dictionary<string, object> { { "Error", "Student not found" } };
            }

            // Create a comprehensive diagnostic report
            var diagnostics = new Dictionary<string, object>
            {
                { "StudentId", student.StudentId },
                { "StudentName", student.StudentName },
                { "RecordCreationTime", student.CreatedDate },
                { "LastUpdateTime", student.UpdatedDate ?? DateTime.MinValue },
                { "RecordAgeInDays", (DateTime.UtcNow - student.CreatedDate).TotalDays },
                { "RecordCompleteness", CalculateRecordCompleteness(student) },
                { "HasRequiredFields", !string.IsNullOrEmpty(student.ParentGuardian) &&
                                      !string.IsNullOrEmpty(student.EmergencyPhone) &&
                                      !string.IsNullOrEmpty(student.HomeAddress) &&
                                      !string.IsNullOrEmpty(student.Grade) },
                { "HasRouteAssignment", !string.IsNullOrEmpty(student.AMRoute) || !string.IsNullOrEmpty(student.PMRoute) },
                { "HasBusStopAssignment", !string.IsNullOrEmpty(student.BusStop) },
                { "HasMedicalNotes", !string.IsNullOrEmpty(student.MedicalNotes) },
                { "HasSpecialNeeds", student.SpecialNeeds },
                { "HasTransportationNotes", !string.IsNullOrEmpty(student.TransportationNotes) },
                { "IsActive", student.Active },
                { "ModelState", SerializeStudentForDiagnostics(student) }
            };

            // Add related data counts
            try
            {
                // Just check if there are any related entries in other tables
                // This would need to be adjusted based on your actual data model
                diagnostics.Add("HasRelatedRecords", false);
            }
            catch (Exception ex)
            {
                diagnostics.Add("RelatedDataCountError", ex.Message);
            }

            return diagnostics;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error generating diagnostics for student {StudentId}", studentId);
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }

    /// <summary>
    /// Calculates the completeness percentage of a student record
    /// Only available in DEBUG builds
    /// </summary>
    private double CalculateRecordCompleteness(Student student)
    {
        var requiredFields = new[]
        {
            student.StudentName,
            student.Grade,
            student.School,
            student.HomeAddress,
            student.City,
            student.State,
            student.Zip,
            student.HomePhone,
            student.ParentGuardian,
            student.EmergencyPhone
        };

        var optionalFields = new[]
        {
            student.StudentNumber,
            student.AMRoute,
            student.PMRoute,
            student.BusStop,
            student.MedicalNotes,
            student.TransportationNotes,
            student.DateOfBirth.HasValue ? "HasValue" : null,
            student.Gender,
            student.PickupAddress,
            student.DropoffAddress,
            student.SpecialAccommodations,
            student.Allergies,
            student.Medications,
            student.DoctorName,
            student.DoctorPhone,
            student.AlternativeContact,
            student.AlternativePhone
        };

        // Calculate completeness (required fields have more weight)
        var requiredFieldsCount = requiredFields.Length;
        var filledRequiredFieldsCount = requiredFields.Count(f => !string.IsNullOrWhiteSpace(f));

        var optionalFieldsCount = optionalFields.Length;
        var filledOptionalFieldsCount = optionalFields.Count(f => !string.IsNullOrWhiteSpace(f));

        var requiredCompleteness = filledRequiredFieldsCount / (double)requiredFieldsCount;
        var optionalCompleteness = filledOptionalFieldsCount / (double)optionalFieldsCount;

        // Weight required fields as 70% of total score, optional as 30%
        return (requiredCompleteness * 0.7) + (optionalCompleteness * 0.3);
    }

    /// <summary>
    /// Serializes a student object for diagnostic viewing
    /// Only available in DEBUG builds
    /// </summary>
    private object SerializeStudentForDiagnostics(Student student)
    {
        return new
        {
            // Basic Info
            student.StudentId,
            student.StudentName,
            student.StudentNumber,
            student.Grade,
            student.School,

            // Contact Info
            Address = new
            {
                student.HomeAddress,
                student.City,
                student.State,
                student.Zip
            },
            Contact = new
            {
                student.HomePhone,
                student.ParentGuardian,
                student.EmergencyPhone
            },
            EmergencyContacts = new
            {
                student.AlternativeContact,
                student.AlternativePhone,
                student.DoctorName,
                student.DoctorPhone
            },

            // Transportation Info
            TransportationDetails = new
            {
                student.AMRoute,
                student.PMRoute,
                student.BusStop,
                student.PickupAddress,
                student.DropoffAddress,
                student.TransportationNotes
            },

            // Medical Info
            MedicalDetails = new
            {
                student.MedicalNotes,
                student.SpecialNeeds,
                student.SpecialAccommodations,
                student.Allergies,
                student.Medications
            },

            // Status Info
            StatusInfo = new
            {
                student.Active,
                student.EnrollmentDate,
                student.CreatedDate,
                student.UpdatedDate,
                student.CreatedBy,
                student.UpdatedBy
            }
        };
    }

    /// <summary>
    /// Provides student data operation metrics for system diagnostics
    /// Only available in DEBUG builds
    /// </summary>
    public async Task<Dictionary<string, object>> GetStudentOperationMetricsAsync()
    {
        try
        {
            Logger.Debug("Retrieving student operation metrics");

            var metrics = new Dictionary<string, object>();
            using var context = _contextFactory.CreateDbContext();

            // Student Record Metrics
            metrics["TotalStudentCount"] = await context.Students.CountAsync();
            metrics["ActiveStudentCount"] = await context.Students.CountAsync(s => s.Active);
            metrics["InactiveStudentCount"] = await context.Students.CountAsync(s => !s.Active);
            metrics["StudentsWithRoutes"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute));
            metrics["StudentsWithoutRoutes"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));
            metrics["StudentsWithBusStops"] = await context.Students.CountAsync(s => !string.IsNullOrEmpty(s.BusStop));
            metrics["StudentsWithoutBusStops"] = await context.Students.CountAsync(s => string.IsNullOrEmpty(s.BusStop));
            metrics["StudentsWithSpecialNeeds"] = await context.Students.CountAsync(s => s.RequiresSpecialNeedsBus || !string.IsNullOrEmpty(s.SpecialNeeds));

            // Database Performance Metrics
            var sw = new System.Diagnostics.Stopwatch();

            sw.Start();
            await context.Students.AsNoTracking().ToListAsync();
            sw.Stop();
            metrics["AllStudentsQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Students.AsNoTracking().Where(s => s.Active).ToListAsync();
            sw.Stop();
            metrics["ActiveStudentsQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Students.AsNoTracking().Where(s => !string.IsNullOrEmpty(s.AMRoute)).ToListAsync();
            sw.Stop();
            metrics["StudentsWithAMRouteQueryTimeMs"] = sw.ElapsedMilliseconds;

            // Record Completeness Distribution
            var students = await context.Students.AsNoTracking().ToListAsync();
            var completenessScores = new List<double>();

            foreach (var student in students)
            {
                completenessScores.Add(CalculateRecordCompleteness(student));
            }

            metrics["AverageRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Average() : 0;
            metrics["MaxRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Max() : 0;
            metrics["MinRecordCompleteness"] = completenessScores.Count > 0 ? completenessScores.Min() : 0;

            // Group completeness into ranges
            var completenessDistribution = new Dictionary<string, int>
            {
                { "0-25%", completenessScores.Count(s => s >= 0 && s < 0.25) },
                { "25-50%", completenessScores.Count(s => s >= 0.25 && s < 0.5) },
                { "50-75%", completenessScores.Count(s => s >= 0.5 && s < 0.75) },
                { "75-100%", completenessScores.Count(s => s >= 0.75 && s <= 1.0) }
            };

            metrics["CompletenessDistribution"] = completenessDistribution;

            return metrics;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error generating student operation metrics");
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }
#endif

    #endregion
}
