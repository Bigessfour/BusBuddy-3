using System.Diagnostics;
using System.IO;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Export / print / hop-5 schedule helpers for <see cref="RouteManagementViewModel"/>.
/// </summary>
internal static class RouteManagementExportHelper
{
    public static void WriteFallbackCsv(IEnumerable<BusBuddy.Core.Models.Route> routes, string fullPath)
    {
        using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
        sw.WriteLine("RouteId,RouteName,Date,Active,StudentCount,StopCount,School,BusNumber");
        foreach (var r in routes)
        {
            static string Csv(string? v)
            {
                if (string.IsNullOrEmpty(v))
                {
                    return string.Empty;
                }

                var esc = v.Replace("\"", "\"\"", StringComparison.Ordinal);
                return "\"" + esc + "\"";
            }

            sw.WriteLine(string.Join(',',
                r.RouteId,
                Csv(r.RouteName),
                r.Date.ToString("yyyy-MM-dd"),
                r.IsActive,
                r.StudentCount ?? 0,
                r.StopCount ?? 0,
                Csv(r.School),
                Csv(r.BusNumber)));
        }
    }

    public static void WriteFallbackReport(IEnumerable<BusBuddy.Core.Models.Route> routes, string fullPath)
    {
        using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
        sw.WriteLine($"Route Summary Export {DateTime.UtcNow:O}");
        sw.WriteLine("====================================");
        foreach (var r in routes)
        {
            sw.WriteLine(
                $"[{r.RouteId}] {r.RouteName} | School:{r.School} | Bus:{r.BusNumber} | Date:{r.Date:yyyy-MM-dd} | Active:{r.IsActive} | Students:{r.StudentCount ?? 0} | Stops:{r.StopCount ?? 0}");
        }
    }

    public static void RevealOrOpen(string path, bool print = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        if (print)
        {
            psi.Verb = "print";
        }

        Process.Start(psi);
    }

    public static async Task<string> WriteSchedulePdfAsync(
        BusBuddy.Core.Models.Route route,
        bool printAfter,
        IOperationalReportService? reportService,
        IBusBuddyDbContextFactory contextFactory)
    {
        var exportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BusBuddy",
            "Printouts");
        Directory.CreateDirectory(exportDir);

        string path;
        if (reportService is not null)
        {
            var generated = await reportService.GenerateAsync(new OperationalReportRequest
            {
                Kind = printAfter ? OperationalReportKind.PrintSchedules : OperationalReportKind.DailySchedule,
                RouteId = route.RouteId,
                OutputDirectory = exportDir
            }).ConfigureAwait(true);
            path = generated.FilePath;
        }
        else
        {
            path = RoutePdfPrinter.GenerateRoutePdf(
                contextFactory,
                route.RouteId,
                exportDir,
                RouteTimeSlot.Both);
        }

        RevealOrOpen(path, print: printAfter);
        return path;
    }

    public static async Task<bool> TryPersistScheduleAsync(
        int routeId,
        IScheduleService? scheduleService)
    {
        if (scheduleService is null)
        {
            return false;
        }

        return await scheduleService.AddDailyFromPublishedRouteAsync(
                routeId,
                DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc))
            .ConfigureAwait(true);
    }
}
