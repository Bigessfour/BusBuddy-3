using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Serilog;
using Serilog.Context;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Archive/restore and logged delete for the students grid. specs/students.md: Archive when the
/// child may return; delete after the clerk picks Mistake, Moved, or Not attending.
/// </summary>
public sealed class StudentsArchiveCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsArchiveCoordinator>();

    private readonly StudentsListCoordinator _list;

    public StudentsArchiveCoordinator(StudentsListCoordinator list)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
    }

    /// <summary>Asks the clerk to confirm before service ends or resumes.</summary>
    public static bool Confirm(StudentModel student, bool archiving)
    {
        var prompt = archiving
            ? $"Archive {student.StudentName}? The record is kept; restore if they return."
            : $"Restore {student.StudentName} to active service?";

        var confirm = MessageBox.Show(
            prompt,
            archiving ? "Confirm archive" : "Confirm restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return confirm == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Opens the reason dialog. Returns null when the clerk cancels. There is no default reason.
    /// </summary>
    public static StudentDeletionRequest? PromptDeletionReason(StudentModel student)
    {
        var vm = new StudentDeletionReasonDialogViewModel(student);
        var dialog = new BusBuddy.WPF.Views.Student.StudentDeletionReasonDialog(vm);
        DialogOwner.Assign(dialog);
        return dialog.ShowDialog() == true ? vm.Result : null;
    }

    /// <summary>
    /// Permanently removes the student after a clerk-chosen reason. Returns true when the grid
    /// should drop the row.
    /// </summary>
    public async Task<bool> ApplyDeleteAsync(StudentModel student, StudentDeletionRequest request)
    {
        using (LogContext.PushProperty("Operation", "DeleteStudent"))
        using (LogContext.PushProperty("StudentId", student.StudentId))
        using (LogContext.PushProperty("Reason", request.Reason))
        {
            try
            {
                Logger.Information(
                    "Deleting student record StudentId={StudentId} Reason={Reason}",
                    student.StudentId,
                    request.Reason);

                var removed = await _list.DeleteStudentAsync(
                    student,
                    request.Reason,
                    request.Notes).ConfigureAwait(true);
                if (removed)
                {
                    return true;
                }

                MessageBox.Show(
                    "Could not delete the student — no row was removed.",
                    "Delete failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(
                    Logger, ex, "Error deleting StudentId={StudentId}", student.StudentId);
                MessageBox.Show(
                    $"Could not delete the student: {ex.Message}",
                    "Delete failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }

    /// <summary>
    /// Flips the student's active status. Returns true when the grid should refresh; failures are
    /// surfaced to the clerk here and reported as false.
    /// </summary>
    public async Task<bool> ApplyAsync(StudentModel student, bool archive)
    {
        using (LogContext.PushProperty("Operation", archive ? "ArchiveStudent" : "RestoreStudent"))
        using (LogContext.PushProperty("StudentId", student.StudentId))
        {
            try
            {
                Logger.Information(
                    "Setting active status StudentId={StudentId} Name={StudentName} Archive={Archive}",
                    student.StudentId,
                    student.StudentName,
                    archive);

                var changed = await _list.ArchiveStudentAsync(student, archive).ConfigureAwait(true);
                if (changed)
                {
                    return true;
                }

                Logger.Warning("Active status unchanged for StudentId={StudentId}", student.StudentId);
                MessageBox.Show(
                    "Could not change the student's status — no row was updated.",
                    archive ? "Archive failed" : "Restore failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(
                    Logger, ex, "Error changing active status StudentId={StudentId}", student.StudentId);
                MessageBox.Show(
                    $"Could not change the student's status: {ex.Message}",
                    archive ? "Archive failed" : "Restore failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}
