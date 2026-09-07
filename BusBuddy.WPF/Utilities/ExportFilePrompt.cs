using System.IO;
using Microsoft.Win32;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>Save-file prompt shared by map GeoJSON export and route-management CSV.</summary>
internal static class ExportFilePrompt
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ExportFilePrompt));

    public static string? TryGetPath(string defaultFileName, string filter, string? defaultExt = null)
    {
        try
        {
            if (System.Windows.Application.Current is not null)
            {
                var dialog = new SaveFileDialog
                {
                    FileName = defaultFileName,
                    Filter = filter,
                    OverwritePrompt = true
                };
                if (!string.IsNullOrWhiteSpace(defaultExt))
                {
                    dialog.DefaultExt = defaultExt;
                }

                return dialog.ShowDialog() == true ? dialog.FileName : null;
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "SaveFileDialog unavailable; using documents folder");
        }

        var exportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BusBuddy",
            "Exports");
        Directory.CreateDirectory(exportDir);
        return Path.Combine(exportDir, defaultFileName);
    }
}
