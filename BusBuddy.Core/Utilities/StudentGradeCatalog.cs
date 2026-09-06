namespace BusBuddy.Core.Utilities;

/// <summary>
/// Canonical district grade labels shared by forms, grids, and <see cref="Services.StudentService"/> validation.
/// </summary>
public static class StudentGradeCatalog
{
    public static readonly string[] All =
    [
        "Pre-K", "K", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"
    ];

    public static bool IsValid(string? grade) =>
        !string.IsNullOrWhiteSpace(grade) && All.Contains(grade, StringComparer.Ordinal);
}
