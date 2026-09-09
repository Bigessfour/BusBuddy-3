using BusBuddy.Core.Services;
using BusBuddy.WPF.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Decides which students the grid shows: the quick-search + active-status predicate, the filter
/// banner text, and the incomplete-intake roster.
/// </summary>
public sealed class StudentsFilterCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsFilterCoordinator>();

    private readonly IStudentService? _studentService;

    public StudentsFilterCoordinator(IStudentService? studentService = null)
    {
        _studentService = studentService;
    }

    /// <summary>
    /// Predicate behind the grid's <see cref="System.ComponentModel.ICollectionView"/>. Archived rows
    /// stay reachable (specs/students.md archives instead of deleting), so the status filter is a
    /// three-way choice rather than a hard exclusion.
    /// </summary>
    public static bool Matches(object candidate, string quickSearchText, FilterStatus activeFilter)
    {
        if (candidate is not StudentModel s)
        {
            return false;
        }

        var activeMatches = activeFilter switch
        {
            FilterStatus.Active => s.Active,
            FilterStatus.Inactive => !s.Active,
            _ => true,
        };

        if (!activeMatches)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(quickSearchText))
        {
            return true;
        }

        var q = quickSearchText.Trim();
        // Case-insensitive contains across key fields
        return (s.StudentName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
               || (s.StudentNumber?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
               || (s.AMRoute?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
               || (s.PMRoute?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
               || (s.School?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>Banner text describing the filter currently narrowing the grid.</summary>
    public static string BuildStatusText(string quickSearchText, FilterStatus activeFilter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(quickSearchText))
        {
            parts.Add($"'{quickSearchText}'");
        }

        if (activeFilter != FilterStatus.All)
        {
            parts.Add(activeFilter == FilterStatus.Active ? "active only" : "archived only");
        }

        return parts.Count == 0 ? "" : $"Filtered: {string.Join(", ", parts)}";
    }

    /// <summary>
    /// Students whose intake is incomplete: missing required fields, no school destination, or an
    /// address that has never produced coordinates. specs/students.md: "Unvalidated addresses show as
    /// incomplete." Returns null when the student service is unavailable.
    /// </summary>
    public async Task<IReadOnlyList<StudentModel>?> LoadIncompleteAsync()
    {
        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is null)
        {
            Logger.Warning("IStudentService unavailable — cannot load incomplete records");
            return null;
        }

        return await studentService.GetStudentsWithMissingInfoAsync().ConfigureAwait(true);
    }
}
