using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Load, archive, and inline-save students for the grid.</summary>
public sealed class StudentsListCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsListCoordinator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IStudentService? _studentService;

    /// <summary>
    /// Field values as last loaded, keyed by StudentId. Inline save diffs against this so a grid
    /// commit writes only the rows a clerk actually touched instead of re-validating and re-geocoding
    /// the whole roster.
    /// </summary>
    private readonly Dictionary<int, string> _loadedRowState = new();

    public StudentsListCoordinator(IBusBuddyDbContextFactory contextFactory, IStudentService? studentService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _studentService = studentService;
    }

    public async Task<IReadOnlyList<StudentModel>> LoadStudentsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        LogConnectionDiagnostics(context);

        IReadOnlyList<StudentModel> loaded;
        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is not null)
        {
            var students = await studentService.GetAllStudentsAsync().ConfigureAwait(true);
            Logger.Information("Loaded {StudentCount} students ViaService=true", students.Count);
            loaded = students.OrderBy(s => s.StudentName).ToList();
        }
        else
        {
            Logger.Warning("IStudentService unavailable — loading students via direct EF");
            loaded = await context.Students
                .Include(s => s.Destination)
                .Include(s => s.PickupStop)
                .OrderBy(s => s.StudentName)
                .ToListAsync()
                .ConfigureAwait(true);
            Logger.Information("Loaded {StudentCount} students ViaService=false", loaded.Count);
        }

        CaptureRowState(loaded);
        return loaded;
    }

    /// <summary>
    /// Archives a student who may return. specs/students.md: Archive when the child may come back;
    /// delete (with a logged reason) when they have left or the row was a mistake.
    /// </summary>
    public async Task<bool> ArchiveStudentAsync(StudentModel student, bool archive = true)
    {
        ArgumentNullException.ThrowIfNull(student);

        Logger.Information(
            "Setting student active status StudentId={StudentId} Archive={Archive}",
            student.StudentId,
            archive);

        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is not null)
        {
            var changed = archive
                ? await studentService.ArchiveStudentAsync(student.StudentId).ConfigureAwait(true)
                : await studentService.RestoreStudentAsync(student.StudentId).ConfigureAwait(true);

            if (!changed)
            {
                Logger.Warning(
                    "Active status unchanged for StudentId={StudentId} (already {State})",
                    student.StudentId,
                    archive ? "archived" : "active");
            }
            else
            {
                student.Active = !archive;
            }

            return changed;
        }

        using var writeContext = _contextFactory.CreateWriteDbContext();
        // AsTracking: BusBuddyDbContext defaults to NoTracking, so a Find-then-mutate saves nothing.
        var tracked = await writeContext.Students
            .AsTracking()
            .FirstOrDefaultAsync(s => s.StudentId == student.StudentId)
            .ConfigureAwait(true);
        if (tracked is null)
        {
            Logger.Warning("Student {StudentId} not found for archive", student.StudentId);
            return false;
        }

        tracked.Active = !archive;
        tracked.UpdatedDate = DateTime.UtcNow;
        await writeContext.SaveChangesAsync().ConfigureAwait(true);
        student.Active = !archive;
        return true;
    }

    /// <summary>
    /// Permanently removes a student after a clerk-chosen reason. specs/students.md: Mistake, Moved,
    /// or Not attending. The service writes the deletion log.
    /// </summary>
    public async Task<bool> DeleteStudentAsync(StudentModel student, StudentDeletionReason reason, string? notes)
    {
        ArgumentNullException.ThrowIfNull(student);

        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is null)
        {
            Logger.Warning("IStudentService unavailable — cannot delete StudentId={StudentId}", student.StudentId);
            return false;
        }

        Logger.Information(
            "Deleting student record StudentId={StudentId} Reason={Reason}",
            student.StudentId,
            reason);
        return await studentService.DeleteStudentAsync(student.StudentId, reason, notes).ConfigureAwait(true);
    }

    public async Task<(int Saved, IReadOnlyList<string> Errors)> SaveInlineGridEditsAsync(
        IEnumerable<StudentModel> students,
        IReadOnlyList<Destination> schoolCatalog)
    {
        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        var persistencePath = studentService is not null ? "IStudentService" : "DirectEf";
        var studentList = students.ToList();
        Logger.Information(
            "Saving inline grid edits for {StudentCount} students via {PersistencePath}",
            studentList.Count,
            persistencePath);

        if (studentService is null)
        {
            Logger.Warning("IStudentService unavailable — inline grid save uses direct EF per row");
        }

        var saved = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var student in studentList)
        {
            student.HomePhone = StudentPhoneNormalizer.Normalize(student.HomePhone);
            student.CellPhone = StudentPhoneNormalizer.Normalize(student.CellPhone);
            student.EmergencyPhone = StudentPhoneNormalizer.Normalize(student.EmergencyPhone);
            StudentSchoolLinker.SyncDestinationFromSchoolName(student, schoolCatalog);
            StudentRecordNormalizer.NormalizeForPersistence(student);

            // Normalization runs first so a purely cosmetic edit (phone punctuation, whitespace)
            // normalizes back to the loaded value and is correctly treated as clean.
            if (!IsRowDirty(student))
            {
                skipped++;
                continue;
            }

            // specs/students.md: lat/lng come from Address Validation. A hand-typed address in the grid
            // has not been validated, so drop stale coordinates — the row saves but reports as
            // incomplete and stays off the map until validation runs.
            if (HasAddressChanged(student) && student.HasValidatedHomeCoordinates)
            {
                Logger.Warning(
                    "Home address edited inline for StudentId={StudentId}; clearing coordinates until Address Validation runs",
                    student.StudentId);
                student.Latitude = null;
                student.Longitude = null;
                student.PlaceId = null;
            }

            try
            {
                if (studentService is not null)
                {
                    if (!await studentService.UpdateStudentAsync(student).ConfigureAwait(true))
                    {
                        Logger.Debug("No changes persisted for student {StudentId}", student.StudentId);
                    }
                }
                else
                {
                    using var context = _contextFactory.CreateWriteDbContext();
                    context.Students.Update(student);
                    await context.SaveChangesAsync().ConfigureAwait(true);
                }

                saved++;
                _loadedRowState[student.StudentId] = BuildRowState(student);

                if (student.HasUnvalidatedHomeAddress)
                {
                    Logger.Warning(
                        "StudentId={StudentId} saved with an unvalidated home address — {IntakeStatus}",
                        student.StudentId,
                        student.IntakeStatus);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Inline save failed for student {StudentId}", student.StudentId);
                errors.Add($"{student.StudentName}: {ex.Message}");
            }
        }

        Logger.Information(
            "Inline grid edits saved: {SavedCount} ok, {SkippedCount} unchanged, {ErrorCount} failed",
            saved,
            skipped,
            errors.Count);
        return (saved, errors);
    }

    /// <summary>
    /// Records the loaded value of every inline-editable field so edits can be diffed. Any caller that
    /// fills the grid from a source other than <see cref="LoadStudentsAsync"/> must call this, otherwise
    /// those rows have no baseline and <see cref="SaveInlineGridEditsAsync"/> will skip them.
    /// </summary>
    public void CaptureRowState(IEnumerable<StudentModel> students)
    {
        _loadedRowState.Clear();
        foreach (var student in students)
        {
            _loadedRowState[student.StudentId] = BuildRowState(student);
        }
    }

    /// <summary>
    /// True when a row differs from the baseline captured when it was loaded. An unsaved row added
    /// through the grid's new-row placeholder has no baseline by definition and always counts as dirty.
    /// </summary>
    private bool IsRowDirty(StudentModel student) =>
        student.StudentId == 0
        || (_loadedRowState.TryGetValue(student.StudentId, out var loaded)
            && !string.Equals(loaded, BuildRowState(student), StringComparison.Ordinal));

    /// <summary>
    /// True only when the address differs from a captured baseline. This fails closed on purpose: with
    /// no baseline there is no evidence the clerk edited the address, and guessing wrong discards
    /// validated coordinates that specs/students.md says clerks must not have to re-enter.
    /// </summary>
    private bool HasAddressChanged(StudentModel student) =>
        _loadedRowState.TryGetValue(student.StudentId, out var loaded)
        && !string.Equals(
            AddressSegment(loaded),
            AddressSegment(BuildRowState(student)),
            StringComparison.Ordinal);

    private static string AddressSegment(string rowState)
    {
        var parts = rowState.Split(RowStateSeparator);
        return parts.Length > AddressFieldCount
            ? string.Join(RowStateSeparator, parts.Take(AddressFieldCount))
            : rowState;
    }

    private const char RowStateSeparator = '\u001f';

    /// <summary>Number of leading fields in <see cref="BuildRowState"/> that make up the home address.</summary>
    private const int AddressFieldCount = 4;

    /// <summary>
    /// Snapshot of the fields the grid can edit. Address parts come first so
    /// <see cref="HasAddressChanged"/> can compare just that prefix.
    /// </summary>
    private static string BuildRowState(StudentModel student) => string.Join(
        RowStateSeparator,
        student.HomeAddress,
        student.City,
        student.State,
        student.Zip,
        student.StudentName,
        student.StudentNumber,
        student.Grade,
        student.School,
        student.DestinationId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        student.PickupStopId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        student.AMRoute,
        student.PMRoute,
        student.RidesAm,
        student.RidesPm,
        student.SchoolYear,
        student.HomePhone,
        student.CellPhone,
        student.EmergencyPhone,
        student.ParentGuardian,
        student.Active,
        student.RequiresSpecialNeedsBus,
        student.Latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        student.Longitude?.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void LogConnectionDiagnostics(BusBuddyDbContext context)
    {
        try
        {
            var provider = context.Database.ProviderName;
            var rawConn = context.Database.GetConnectionString();
            var masked = rawConn ?? "(null)";
            if (!string.IsNullOrEmpty(masked))
            {
                masked = System.Text.RegularExpressions.Regex.Replace(
                    masked,
                    "(?i)(Password|Pwd)=([^;]+)",
                    "$1=***");
            }

            Logger.Debug("EF Provider: {Provider}; Connection: {Connection}", provider, masked);
            if (!string.IsNullOrEmpty(rawConn) && rawConn.Contains("${", StringComparison.Ordinal))
            {
                Logger.Warning("Connection string contains unresolved placeholders. Check appsettings and environment variables.");
            }

            Logger.Information("Students list using connection: {ConnectionString}", rawConn ?? "(null)");
        }
        catch
        {
            // Non-fatal diagnostics.
        }
    }
}
