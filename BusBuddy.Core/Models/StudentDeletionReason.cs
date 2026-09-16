namespace BusBuddy.Core.Models;

/// <summary>
/// Closed set of clerk reasons for removing a student row. specs/students.md: deletion is allowed
/// only after the clerk picks one of these — there is no default.
/// </summary>
public enum StudentDeletionReason
{
    /// <summary>The roster row was entered in error.</summary>
    Mistake = 1,

    /// <summary>The student moved out of the district.</summary>
    Moved = 2,

    /// <summary>The student is otherwise no longer attending this school.</summary>
    NotAttending = 3
}

/// <summary>Display labels for <see cref="StudentDeletionReason"/> on the clerk dialog.</summary>
public sealed record StudentDeletionReasonOption(StudentDeletionReason Reason, string Label)
{
    public static IReadOnlyList<StudentDeletionReasonOption> All { get; } =
    [
        new(StudentDeletionReason.Mistake, "Mistake — entered by error"),
        new(StudentDeletionReason.Moved, "Moved — left the district"),
        new(StudentDeletionReason.NotAttending, "Not attending — no longer at this school")
    ];
}

/// <summary>What the clerk confirmed on the delete-student dialog.</summary>
public readonly record struct StudentDeletionRequest(StudentDeletionReason Reason, string? Notes);
