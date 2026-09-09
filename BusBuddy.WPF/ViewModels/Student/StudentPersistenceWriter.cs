using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Which write path stored the record. Chosen once, before any writing starts.</summary>
public enum StudentPersistencePath
{
    /// <summary>Normal flow — <see cref="IStudentService"/> applies service rules and audit behaviour.</summary>
    Service,

    /// <summary>
    /// Direct EF write, used only when service validation is deliberately bypassed, or when no
    /// <see cref="IStudentService"/> is registered at all.
    /// </summary>
    DirectEf,
}

/// <summary>
/// The only place the student form writes a student row.
/// <para>
/// There are two write paths on purpose. <see cref="IStudentService"/> is the normal one and is
/// always registered by <c>App.ConfigureServices</c>. The direct-EF path exists because the bulk
/// entry bypass is defined as "skip service validation", which by definition cannot go through the
/// validating service; it is also the last resort when DI never came up (design-time, unit tests).
/// Keeping both here means the save flow reads as one decision instead of two interleaved branches.
/// </para>
/// </summary>
public sealed class StudentPersistenceWriter
{
    private static readonly ILogger Logger = Log.ForContext<StudentPersistenceWriter>();

    private readonly BusBuddyDbContext _context;
    private readonly IStudentService? _studentService;
    private readonly Func<bool> _isEditMode;
    private readonly Func<bool> _bypassesServiceValidation;

    public StudentPersistenceWriter(
        BusBuddyDbContext context,
        IStudentService? studentService,
        Func<bool> isEditMode,
        Func<bool> bypassesServiceValidation)
    {
        _context = context;
        _studentService = studentService;
        _isEditMode = isEditMode;
        _bypassesServiceValidation = bypassesServiceValidation;
    }

    /// <summary>
    /// Writes the record and returns the path taken plus the persisted instance, which differs from
    /// the input on add because the service returns the row carrying the new key.
    /// </summary>
    public async Task<(StudentPersistencePath Path, StudentModel Persisted)> WriteAsync(StudentModel student)
    {
        var service = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (service is null)
        {
            Logger.Warning("IStudentService unavailable — saving student via direct EF (no service validation)");
        }

        if (service is not null && !_bypassesServiceValidation())
        {
            return (StudentPersistencePath.Service, await WriteViaServiceAsync(service, student).ConfigureAwait(true));
        }

        return (StudentPersistencePath.DirectEf, await WriteViaDbContextAsync(student).ConfigureAwait(true));
    }

    /// <summary>Let open list views refresh immediately. A failed broadcast must not fail the save.</summary>
    public static void BroadcastSaved(StudentModel student)
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new StudentSavedMessage(student));
        }
        catch (Exception messengerEx)
        {
            Logger.Warning(
                messengerEx,
                "StudentSavedMessage broadcast failed for StudentId={StudentId}; open lists may show stale data",
                student.StudentId);
        }
    }

    private async Task<StudentModel> WriteViaServiceAsync(IStudentService service, StudentModel student)
    {
        if (!_isEditMode())
        {
            return await service.AddStudentAsync(student);
        }

        if (!await service.UpdateStudentAsync(student))
        {
            throw new InvalidOperationException("Update operation reported no changes.");
        }

        return student;
    }

    private async Task<StudentModel> WriteViaDbContextAsync(StudentModel student)
    {
        if (_isEditMode())
        {
            _context.Students.Update(student);
        }
        else
        {
            _context.Students.Add(student);
        }

        await _context.SaveChangesAsync();
        return student;
    }
}
