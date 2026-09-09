using System.IO;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// CSV roster in and out for the students grid. Exports land under the clerk's Documents folder —
/// specs/students.md keeps rosters in the local clerk workflow, never in the repo.
/// </summary>
public sealed class StudentsCsvCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsCsvCoordinator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;

    public StudentsCsvCoordinator(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>Writes the supplied (already filtered) rows to a timestamped CSV and returns its path.</summary>
    public static string Export(IReadOnlyList<StudentModel> rows)
    {
        var exportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BusBuddy", "Exports");
        Directory.CreateDirectory(exportDir);
        var fileName = $"students-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var fullPath = Path.Combine(exportDir, fileName);

        using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
        sw.WriteLine("StudentId,StudentName,StudentNumber,Grade,AMRoute,PMRoute,School,DestinationId,Latitude,Longitude,Active");
        foreach (var s in rows)
        {
            sw.WriteLine(string.Join(',',
                s.StudentId,
                Csv(s.StudentName),
                Csv(s.StudentNumber),
                Csv(s.Grade),
                Csv(s.AMRoute),
                Csv(s.PMRoute),
                Csv(s.School),
                s.DestinationId,
                s.Latitude,
                s.Longitude,
                s.Active));
        }

        sw.Flush();
        Logger.Information("Exported {Count} students to {File}", rows.Count, fullPath);
        return fullPath;

        static string Csv(string? v)
        {
            if (string.IsNullOrEmpty(v)) return string.Empty;
            var escaped = v.Replace("\"", "\"\"", StringComparison.Ordinal);
            return "\"" + escaped + "\"";
        }
    }

    /// <summary>Asks the clerk for a CSV to import; returns null when the dialog is cancelled.</summary>
    public static string? PromptForCsvPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import students from CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Imports a student CSV via <see cref="ISeedDataService"/> and returns the added count.</summary>
    public async Task<int> ImportAsync(string csvPath)
    {
        var seed = App.ServiceProvider?.GetService<ISeedDataService>()
            ?? new SeedDataService(_contextFactory);
        var added = await seed.ImportStudentsFromCsvAsync(csvPath);
        Logger.Information("CSV import finished Added={Added} Path={Path}", added, csvPath);
        return added;
    }
}
