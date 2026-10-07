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
        bool openAfter = true)
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

        var busId = slot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;
        var driverId = slot == RouteTimeSlot.PM ? route.PMDriverId : route.AMDriverId;
        var bus = await FindBusAsync(routeService, busId).ConfigureAwait(true);
        var driver = await FindDriverAsync(routeService, driverId).ConfigureAwait(true);

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

        return new SchedulePdfResult(path, sheet.Stops.Count, sheet.Students.Count, bytes);
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

        var busId = route.AMVehicleId ?? route.PMVehicleId;
        var driverId = route.AMDriverId ?? route.PMDriverId;
        if (!busId.HasValue || !driverId.HasValue)
        {
            return false;
        }

        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var departure = day.Add(route.AMBeginTime ?? TimeSpan.FromHours(7));
        var arrival = departure.AddMinutes(route.EstimatedDuration ?? 45);
        if (arrival <= departure)
        {
            arrival = departure.AddMinutes(45);
        }

        await scheduleService.AddScheduleAsync(new Schedule
        {
            RouteId = route.RouteId,
            BusId = busId.Value,
            DriverId = driverId.Value,
            ScheduleDate = day,
            DepartureTime = departure,
            ArrivalTime = arrival,
            Location = route.School,
            Notes = $"Generated from Route Management for {route.RouteName}",
            Status = "Scheduled",
            CreatedDate = DateTime.UtcNow
        }).ConfigureAwait(true);
        return true;
    }
}

/// <summary>Printed stop sheet for one route.</summary>
internal readonly record struct SchedulePdfResult(string Path, int StopCount, int StudentCount, byte[] Pdf);
