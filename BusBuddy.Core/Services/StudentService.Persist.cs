using System.Globalization;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

public partial class StudentService
{
    #region Write Operations

    public async Task<Result<Student>> AddStudentAsync(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        try
        {
            Logger.Information("Adding new student: {StudentName}", student.StudentName);

            StudentRecordNormalizer.NormalizeForPersistence(student);

            var validationErrors = await ValidateStudentAsync(student);
            if (validationErrors.Count > 0)
            {
                return Result.Failure<Student>($"Student validation failed: {string.Join(", ", validationErrors)}");
            }

            // Set default values
            if (student.EnrollmentDate == null)
            {
                student.EnrollmentDate = DateTime.UtcNow.Date;
            }

            // Geocode on add when coordinates are not provided and a geocoder is available.
            // A clerk driveway pin already on the new row is kept.
            if (_geocodingService != null
                && !student.HomePickupClerkAdjusted
                && (!student.Latitude.HasValue || !student.Longitude.HasValue))
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
                await StudentDisplayMirror.SyncAsync(context, student).ConfigureAwait(false);
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
            return Result.Success(student);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error adding student: {StudentName}", student.StudentName);
            throw;
        }
    }

    public async Task<Result<bool>> UpdateStudentAsync(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        try
        {
            Logger.Information("Updating student with ID: {StudentId}", student.StudentId);

            StudentRecordNormalizer.NormalizeForPersistence(student);

            var (policyContext, policyDispose) = GetWriteContext();
            try
            {
                var stored = await policyContext.Students.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StudentId == student.StudentId)
                    .ConfigureAwait(false);
                HomePickupPin.ApplyOnSave(student, stored);
            }
            finally
            {
                if (policyDispose)
                {
                    await policyContext.DisposeAsync().ConfigureAwait(false);
                }
            }

            // Geocode if coordinates missing and address present. A clerk driveway pin is never a missing coordinate.
            if (_geocodingService != null
                && !student.HomePickupClerkAdjusted
                && (!student.Latitude.HasValue || !student.Longitude.HasValue))
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

            var validationErrors = await ValidateStudentAsync(student).ConfigureAwait(false);
            if (validationErrors.Count > 0)
            {
                return Result.Failure<bool>($"Student validation failed: {string.Join(", ", validationErrors)}");
            }

            var (context, dispose) = GetWriteContext();
            int result;
            try
            {
                var previous = await context.Students.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StudentId == student.StudentId)
                    .ConfigureAwait(false);
                if (previous is not null)
                {
                    ClearSlotWhenEligibilityEnds(student, previous);
                    if (!previous.RequiresSpecialNeedsBus && student.RequiresSpecialNeedsBus)
                    {
                        student.RequiresAide = true;
                    }
                }

                await StudentDisplayMirror.SyncAsync(context, student).ConfigureAwait(false);
                context.Students.Update(student);
                if (student.HasValidatedHomeCoordinates)
                {
                    await AssignedHomeStopSync.ApplyAsync(context, student).ConfigureAwait(false);
                }

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
                if (student.HasValidatedHomeCoordinates)
                {
                    await RetimeAssignedRoutesAsync(student.StudentId, student.AmRouteId, student.PmRouteId)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                Logger.Warning("No changes were made when updating student: {StudentId}", student.StudentId);
            }

            return Result.Success(success);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error updating student with ID: {StudentId}", student.StudentId);
            throw;
        }
    }

    public Task<bool> UpdateHomeGeocodeAsync(
        int studentId,
        decimal? latitude,
        decimal? longitude,
        string? placeId) =>
        WriteHomeGeocodeAsync(studentId, latitude, longitude, placeId, homePickupClerkAdjusted: null);

    public Task<bool> UpdateHomeGeocodeAsync(
        int studentId,
        decimal? latitude,
        decimal? longitude,
        string? placeId,
        bool homePickupClerkAdjusted) =>
        WriteHomeGeocodeAsync(studentId, latitude, longitude, placeId, homePickupClerkAdjusted);

    private async Task<bool> WriteHomeGeocodeAsync(
        int studentId,
        decimal? latitude,
        decimal? longitude,
        string? placeId,
        bool? homePickupClerkAdjusted)
    {
        if (studentId <= 0)
        {
            return false;
        }

        var (context, dispose) = GetWriteContext();
        int? amRouteId = null;
        int? pmRouteId = null;
        var saved = false;
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

            // Address Validation and other geocoders pass null. A clerk pin stays until the pin window
            // writes true or false, or the street address changes on a full save.
            if (row.HomePickupClerkAdjusted && homePickupClerkAdjusted is null)
            {
                Logger.Information(
                    "Clerk home pickup pin kept StudentId={StudentId}",
                    studentId);
                if (await AssignedHomeStopSync.ApplyAsync(context, row).ConfigureAwait(false))
                {
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }

                amRouteId = row.AmRouteId;
                pmRouteId = row.PmRouteId;
                saved = true;
            }
            else
            {
                row.Latitude = latitude;
                row.Longitude = longitude;
                if (homePickupClerkAdjusted is bool adjusted)
                {
                    row.HomePickupClerkAdjusted = adjusted;
                }
                else if (!LocationCoordinate.IsValidated(latitude, longitude))
                {
                    row.HomePickupClerkAdjusted = false;
                }

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
                    await AssignedHomeStopSync.ApplyAsync(context, row).ConfigureAwait(false);
                }

                await context.SaveChangesAsync().ConfigureAwait(false);
                Logger.Information(
                    "Home geocode persisted StudentId={StudentId} HasCoords={HasCoords}",
                    studentId,
                    latitude.HasValue && longitude.HasValue);
                amRouteId = row.AmRouteId;
                pmRouteId = row.PmRouteId;
                saved = true;
            }
        }
        finally
        {
            if (dispose)
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (saved)
        {
            await RetimeAssignedRoutesAsync(studentId, amRouteId, pmRouteId).ConfigureAwait(false);
        }

        return saved;
    }

    private async Task RetimeAssignedRoutesAsync(int studentId, int? amRouteId, int? pmRouteId)
    {
        if (_clocks is null)
        {
            return;
        }

        var routeIds = new[] { amRouteId, pmRouteId }
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct();
        foreach (var routeId in routeIds)
        {
            try
            {
                var timed = await _clocks.ApplyPublishedClocksAsync(routeId).ConfigureAwait(false);
                if (!timed.Success)
                {
                    Logger.Warning(
                        "Published clocks left unchanged StudentId={StudentId} RouteId={RouteId} Reason={Reason}",
                        studentId,
                        routeId,
                        timed.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    ex,
                    "Published clocks skipped StudentId={StudentId} RouteId={RouteId}",
                    studentId,
                    routeId);
            }
        }
    }

    /// <summary>
    /// A student who no longer rides a session cannot keep that session's route.
    /// Trip rows (<c>StudentSchedules</c>) stay: a trip is not the year route pairing.
    /// Same clear as <c>RouteService.RemoveStudentFromRouteAsync</c> (slot name and key only).
    /// </summary>
    private static void ClearSlotWhenEligibilityEnds(Student student, Student previous)
    {
        if (previous.RidesAm && !student.RidesAm)
        {
            Logger.Information(
                "AM eligibility ended; clearing AM route StudentId={StudentId} RouteId={RouteId}",
                student.StudentId,
                previous.AmRouteId);
            student.AMRoute = null;
            student.AmRouteId = null;
        }

        if (previous.RidesPm && !student.RidesPm)
        {
            Logger.Information(
                "PM eligibility ended; clearing PM route StudentId={StudentId} RouteId={RouteId}",
                student.StudentId,
                previous.PmRouteId);
            student.PMRoute = null;
            student.PmRouteId = null;
        }
    }

    private static async Task ClearPublishedHomeStopCoordinatesAsync(BusBuddyDbContext context, int studentId)
    {
        var stops = await context.RouteStops
            .AsTracking()
            .Where(s => s.Notes != null && (s.Notes.Contains($"StudentId={studentId}") || s.Notes.StartsWith("StudentIds=")))
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var stop in stops)
        {
            if (!NotesNameStudent(stop.Notes, studentId))
            {
                continue;
            }

            stop.Latitude = null;
            stop.Longitude = null;
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
                var removedStops = await RemoveStudentHomeStopsAsync(context, student).ConfigureAwait(false);
                context.StudentDeletionLogs.Add(log);
                context.Students.Remove(student);

                var result = await context.SaveChangesAsync();
                if (result > 0)
                {
                    WriteStudentDeletionLog(log);
                    if (removedStops > 0)
                    {
                        Logger.Information(
                            "Student delete removed home stops StudentId={StudentId} Stops={StopCount}",
                            student.StudentId,
                            removedStops);
                    }
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
    /// Drops a home stop that names only this student. A shared stop keeps the other riders.
    /// The route row stays. specs/students.md: assignment rows leave with the student.
    /// </summary>
    private static async Task<int> RemoveStudentHomeStopsAsync(BusBuddyDbContext context, Student student)
    {
        var name = student.StudentName?.Trim() ?? string.Empty;
        var idText = student.StudentId.ToString(CultureInfo.InvariantCulture);
        var candidates = await context.RouteStops
            .AsTracking()
            .Where(s =>
                s.StopName == name
                || (s.Notes != null && s.Notes.Contains(idText)))
            .ToListAsync()
            .ConfigureAwait(false);

        var removed = 0;
        foreach (var stop in candidates)
        {
            var named = RouteSummarySheetBuilder.ParseStudentIds(stop.Notes);
            var idMatch = NotesNameStudent(stop.Notes, student.StudentId);
            var nameMatch = name.Length > 0
                && string.Equals(stop.StopName?.Trim(), name, StringComparison.OrdinalIgnoreCase);
            if (!idMatch && !nameMatch)
            {
                continue;
            }

            var others = named.Where(id => id != student.StudentId).ToList();
            if (others.Count > 0)
            {
                stop.Notes = others.Count == 1
                    ? $"StudentId={others[0]}"
                    : $"StudentIds={string.Join(",", others)}";
                stop.UpdatedDate = DateTime.UtcNow;
                continue;
            }

            context.RouteStops.Remove(stop);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// Structured deletion log. StudentId and StudentNumber only — no name, address, or guardian.
    /// </summary>
    internal static void WriteStudentDeletionLog(StudentDeletionLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Logger.Information(
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

    #region Active status

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

    #endregion

    #region Address and Contact Management

    public async Task<Result<bool>> UpdateStudentAddressAsync(int studentId, string homeAddress, string city, string state, string zip)
    {
        try
        {
            Logger.Information("Updating address information for student {StudentId}", studentId);

            var addressValidation = ValidateAddress(homeAddress, city, state, zip);
            if (!addressValidation.IsValid)
            {
                return Result.Failure<bool>($"Address validation failed: {addressValidation.ErrorMessage}");
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
                    return Result.Success(false);
                }

                studentName = student.StudentName;
                var addressChanged = !string.Equals(student.HomeAddress?.Trim(), homeAddress.Trim(), StringComparison.Ordinal)
                    || !string.Equals(student.City?.Trim(), city.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(student.State?.Trim(), state.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(student.Zip?.Trim(), zip.Trim(), StringComparison.Ordinal);
                student.HomeAddress = homeAddress;
                student.City = city;
                student.State = state;
                student.Zip = zip;
                if (addressChanged)
                {
                    student.Latitude = null;
                    student.Longitude = null;
                    student.PlaceId = null;
                    await ClearPublishedHomeStopCoordinatesAsync(context, student.StudentId).ConfigureAwait(false);
                }

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

            return Result.Success(success);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error updating address for student {StudentId}", studentId);
            throw;
        }
    }

    #endregion
}
