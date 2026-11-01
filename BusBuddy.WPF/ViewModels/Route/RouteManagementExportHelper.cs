using System.Diagnostics;
using System.IO;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;

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
        // Verb=print throws Win32Exception on many Windows setups after the PDF is already written
        // (guest 2026-09-17: "Failed printing schedule" after a successful write).
        _ = print;
        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not open {Path}", path);
        }
    }

    /// <summary>Unit tests set this false so a generated PDF is not handed to the shell.</summary>
    internal static bool OpenAfterWrite { get; set; } = true;

    /// <summary>
    /// Prints the published stop sheet (stops, clocks, riders) for one route.
    /// The district Daily Schedule table is a different report and is not this PDF.
    /// </summary>
    public static async Task<SchedulePdfResult> WriteSchedulePdfAsync(
        BusBuddy.Core.Models.Route route,
        IRouteService routeService,
        string? outputDirectory = null,
        bool openAfter = true,
        IScheduleService? scheduleService = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(routeService);

        var exportDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BusBuddy",
                "Printouts")
            : outputDirectory;
        Directory.CreateDirectory(exportDir);

        var slot = RouteSession.ToAssignmentSlot(route);
        var stopsResult = await routeService.GetRouteStopsAsync(route.RouteId).ConfigureAwait(true);
        if (stopsResult is not { IsSuccess: true, Value: not null })
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stopsResult?.Error)
                ? "Could not load published stops for this route."
                : stopsResult.Error);
        }

        var stops = stopsResult.Value.ToList();
        var studentsResult = await routeService.GetStudentsForRouteAsync(route.RouteId, slot).ConfigureAwait(true);
        var students = studentsResult is { IsSuccess: true, Value: not null }
            ? studentsResult.Value
            : new List<BusBuddy.Core.Models.Student>();

        var dated = scheduleService is null
            ? Array.Empty<Schedule>()
            : await scheduleService
                .GetSchedulesForRouteOnDateAsync(route.RouteId, RouteDailyPairing.ServiceDateUtc(route))
                .ConfigureAwait(true);
        var pairing = RouteDailyPairing.Resolve(route, slot, dated);
        var matchedSchedule = dated.FirstOrDefault(s =>
            s.BusId == pairing.BusId && s.DriverId == pairing.DriverId && pairing.FromSchedule);
        var bus = await FindBusAsync(routeService, pairing.BusId).ConfigureAwait(true)
            ?? matchedSchedule?.Bus;
        var driver = await FindDriverAsync(routeService, pairing.DriverId).ConfigureAwait(true)
            ?? matchedSchedule?.Driver;

        var sheet = RouteSummarySheetBuilder.Build(route, stops, students, bus, driver, slot);
        if (sheet.Stops.Count == 0)
        {
            throw new InvalidOperationException(
                $"'{route.RouteName}' has no published stops to print.");
        }

        var bytes = RouteSummaryPdfRenderer.Render(route, stops, students, bus, driver, slot);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("The schedule PDF was empty.");
        }

        var safeName = string.Join("_", (route.RouteName ?? "Route").Split(Path.GetInvalidFileNameChars()));
        var fileName = $"Route_{safeName}_{slot}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var path = Path.Combine(exportDir, fileName);
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);

        if (openAfter && OpenAfterWrite)
        {
            RevealOrOpen(path);
        }

        return new SchedulePdfResult(
            path,
            sheet.Stops.Count,
            sheet.Students.Count,
            bytes,
            sheet.BusLabel,
            sheet.DriverLabel);
    }

    private static async Task<BusBuddy.Core.Models.Bus?> FindBusAsync(IRouteService routeService, int? busId)
    {
        if (busId is not int id)
        {
            return null;
        }

        var buses = await routeService.GetAvailableBusesAsync().ConfigureAwait(true);
        if (buses is not { IsSuccess: true, Value: not null })
        {
            return null;
        }

        return buses.Value.FirstOrDefault(bus => bus.BusId == id);
    }

    private static async Task<BusBuddy.Core.Models.Driver?> FindDriverAsync(IRouteService routeService, int? driverId)
    {
        if (driverId is not int id)
        {
            return null;
        }

        var drivers = await routeService.GetAvailableDriversAsync().ConfigureAwait(true);
        if (drivers is not { IsSuccess: true, Value: not null })
        {
            return null;
        }

        return drivers.Value.FirstOrDefault(driver => driver.DriverId == id);
    }

    public static async Task<bool> TryPersistScheduleAsync(
        BusBuddy.Core.Models.Route route,
        IScheduleService? scheduleService)
    {
        if (scheduleService is null)
        {
            return false;
        }

        var slot = RouteSession.ToAssignmentSlot(route);
        var busId = slot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;
        var driverId = slot == RouteTimeSlot.PM ? route.PMDriverId : route.AMDriverId;
        if (!busId.HasValue || !driverId.HasValue)
        {
            return false;
        }

        var row = RouteDailyPairing.BuildSessionRow(route, slot, busId.Value, driverId.Value);
        row.Notes = $"Generated from Route Management for {route.RouteName}";
        await scheduleService.SaveRouteSessionScheduleAsync(row).ConfigureAwait(true);
        return true;
    }
}

/// <summary>Printed stop sheet for one route.</summary>
internal readonly record struct SchedulePdfResult(
    string Path,
    int StopCount,
    int StudentCount,
    byte[] Pdf,
    string BusLabel = "",
    string DriverLabel = "");
