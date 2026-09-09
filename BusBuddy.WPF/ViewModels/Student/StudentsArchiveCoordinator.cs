using System.Windows;
using BusBuddy.Core.Utilities;
using Serilog;
using Serilog.Context;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Archive/restore for the students grid. specs/students.md: "MUST NOT delete a student to end
/// service. Archive or set inactive so history and route versions remain." There is deliberately no
/// delete path here — the row stays in the collection and is distinguished by the Active column.
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
            ? $"Archive {student.StudentName}? The record and its history are kept; the student stops riding."
            : $"Restore {student.StudentName} to active service?";

        var confirm = MessageBox.Show(
            prompt,
            archiving ? "Confirm archive" : "Confirm restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return confirm == MessageBoxResult.Yes;
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
