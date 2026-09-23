using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

public partial class StudentService
{
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
                    ["StudentsWithRoutes"] = await context.Students.CountAsync(s =>
                        s.AmRouteId != null || s.PmRouteId != null
                        || (s.AmRouteId == null && s.AMRoute != null && s.AMRoute != "")
                        || (s.PmRouteId == null && s.PMRoute != null && s.PMRoute != "")),
                    ["StudentsWithoutRoutes"] = await context.Students.CountAsync(s =>
                        s.AmRouteId == null && s.PmRouteId == null
                        && (s.AMRoute == null || s.AMRoute == "")
                        && (s.PMRoute == null || s.PMRoute == ""))
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
}
