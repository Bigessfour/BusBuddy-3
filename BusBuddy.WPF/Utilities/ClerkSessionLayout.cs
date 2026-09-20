using System.Collections.Generic;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Session-only clerk layout: delete-student window size and students-grid column visibility.
/// Survives reopening the same window in this process; resets when the app exits.
/// </summary>
public static class ClerkSessionLayout
{
    private static readonly Dictionary<string, bool> StudentGridColumnHidden = new(StringComparer.Ordinal);

    public static double? StudentDeletionWidth { get; private set; }

    public static double? StudentDeletionHeight { get; private set; }

    public static bool HasStudentGridColumnPrefs => StudentGridColumnHidden.Count > 0;

    public static void RememberDeletionSize(double width, double height)
    {
        if (width < 400 || height < 400)
        {
            return;
        }

        StudentDeletionWidth = width;
        StudentDeletionHeight = height;
    }

    public static bool TryGetDeletionSize(out double width, out double height)
    {
        if (StudentDeletionWidth is double rememberedWidth &&
            StudentDeletionHeight is double rememberedHeight)
        {
            width = rememberedWidth;
            height = rememberedHeight;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    public static void RememberStudentGridColumnHidden(IEnumerable<KeyValuePair<string, bool>> columns)
    {
        StudentGridColumnHidden.Clear();
        foreach (var pair in columns)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                StudentGridColumnHidden[pair.Key] = pair.Value;
            }
        }
    }

    public static bool TryGetStudentGridColumnHidden(string key, out bool hidden) =>
        StudentGridColumnHidden.TryGetValue(key, out hidden);

    public static void Clear()
    {
        StudentDeletionWidth = null;
        StudentDeletionHeight = null;
        StudentGridColumnHidden.Clear();
    }
}
