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

/// <summary>Load, delete, and inline-save students for the grid.</summary>
public sealed class StudentsListCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsListCoordinator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IStudentService? _studentService;

    public StudentsListCoordinator(IBusBuddyDbContextFactory contextFactory, IStudentService? studentService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _studentService = studentService;
    }

    public async Task<IReadOnlyList<StudentModel>> LoadStudentsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        LogConnectionDiagnostics(context);

        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is not null)
        {
            var students = await studentService.GetAllStudentsAsync().ConfigureAwait(true);
            Logger.Information("Loaded {StudentCount} students ViaService=true", students.Count);
            return students.OrderBy(s => s.StudentName).ToList();
        }

        Logger.Warning("IStudentService unavailable — loading students via direct EF");
        var list = await context.Students
            .OrderBy(s => s.StudentName)
            .ToListAsync()
            .ConfigureAwait(true);
        Logger.Information("Loaded {StudentCount} students ViaService=false", list.Count);
        return list;
    }

    public async Task<bool> DeleteStudentAsync(StudentModel student)
    {
        Logger.Information(
            "Deleting student StudentId={StudentId} Name={StudentName}",
            student.StudentId,
            student.StudentName);

        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is not null)
        {
            var deleted = await studentService.DeleteStudentAsync(student.StudentId).ConfigureAwait(true);
            if (!deleted)
            {
                Logger.Warning("DeleteStudentAsync returned false for StudentId={StudentId}", student.StudentId);
            }

            return deleted;
        }

        using var context = _contextFactory.CreateWriteDbContext();
        context.Students.Remove(student);
        await context.SaveChangesAsync().ConfigureAwait(true);
        return true;
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
        var errors = new List<string>();

        foreach (var student in studentList)
        {
            student.HomePhone = StudentPhoneNormalizer.Normalize(student.HomePhone);
            student.CellPhone = StudentPhoneNormalizer.Normalize(student.CellPhone);
            student.EmergencyPhone = StudentPhoneNormalizer.Normalize(student.EmergencyPhone);
            StudentSchoolLinker.SyncDestinationFromSchoolName(student, schoolCatalog);
            StudentRecordNormalizer.NormalizeForPersistence(student);

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
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Inline save failed for student {StudentId}", student.StudentId);
                errors.Add($"{student.StudentName}: {ex.Message}");
            }
        }

        Logger.Information("Inline grid edits saved: {SavedCount} ok, {ErrorCount} failed", saved, errors.Count);
        return (saved, errors);
    }

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
