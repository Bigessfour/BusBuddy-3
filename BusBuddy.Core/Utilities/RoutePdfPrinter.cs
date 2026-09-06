using System;
using System.IO;
using System.Linq;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Builds a printable route-summary PDF (stops, students, assigned bus/driver).
    /// </summary>
    public static class RoutePdfPrinter
    {
        public static string GenerateFirstActiveRoutePdf(
            IBusBuddyDbContextFactory contextFactory,
            string outputDirectory,
            RouteTimeSlot slot = RouteTimeSlot.AM)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            using var ctx = contextFactory.CreateDbContext();
            var routeId = ctx.Routes.AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RouteId)
                .Select(r => r.RouteId)
                .FirstOrDefault();
            if (routeId == 0)
            {
                throw new InvalidOperationException("No active route found to export.");
            }

            return GenerateRoutePdf(contextFactory, routeId, outputDirectory, slot);
        }

        public static string GenerateRoutePdf(
            IBusBuddyDbContextFactory contextFactory,
            int routeId,
            string outputDirectory,
            RouteTimeSlot slot = RouteTimeSlot.AM)
        {
            var opId = Guid.NewGuid().ToString("N");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ArgumentNullException.ThrowIfNull(contextFactory);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Environment.CurrentDirectory;
            }

            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception exDir)
            {
                Log.Error(exDir, "[RoutePdfPrinter] Failed to ensure output directory {Dir} (OpId={OpId})", outputDirectory, opId);
                throw;
            }

            using var ctx = contextFactory.CreateDbContext();
            using (Serilog.Context.LogContext.PushProperty("OpId", opId))
            using (Serilog.Context.LogContext.PushProperty("Slot", slot))
            using (Serilog.Context.LogContext.PushProperty("RouteId", routeId))
            {
                try
                {
                    Log.Information(
                        "[RoutePdfPrinter] Begin route PDF generation (OpId={OpId}, OutputDir={Dir}, Slot={Slot}, RouteId={RouteId})",
                        opId, outputDirectory, slot, routeId);

                    var route = ctx.Routes.AsNoTracking().FirstOrDefault(r => r.RouteId == routeId);
                    if (route == null)
                    {
                        Log.Warning("[RoutePdfPrinter] Route {RouteId} not found (OpId={OpId})", routeId, opId);
                        throw new InvalidOperationException($"Route {routeId} was not found.");
                    }

                    using (Serilog.Context.LogContext.PushProperty("RouteName", route.RouteName))
                    {
                        var stops = ctx.RouteStops.AsNoTracking()
                            .Where(rs => rs.RouteId == route.RouteId)
                            .OrderBy(rs => rs.StopOrder)
                            .ToList();
                        Log.Debug("[RoutePdfPrinter] Loaded {StopCount} stops for route {RouteId} (OpId={OpId})", stops.Count, route.RouteId, opId);

                        var students = ctx.Students.AsNoTracking()
                            .Where(s => (slot == RouteTimeSlot.AM && s.AMRoute == route.RouteName) ||
                                        (slot == RouteTimeSlot.PM && s.PMRoute == route.RouteName) ||
                                        slot == RouteTimeSlot.Both)
                            .OrderBy(s => s.StudentName)
                            .ToList();
                        Log.Debug("[RoutePdfPrinter] Loaded {StudentCount} students matched for slot {Slot} (OpId={OpId})", students.Count, slot, opId);

                        var vehicleId = slot == RouteTimeSlot.PM ? route.PMVehicleId : route.AMVehicleId;
                        var driverId = slot == RouteTimeSlot.PM ? route.PMDriverId : route.AMDriverId;
                        Bus? bus = vehicleId.HasValue
                            ? ctx.Buses.AsNoTracking().FirstOrDefault(b => b.BusId == vehicleId.Value)
                            : null;
                        Driver? driver = driverId.HasValue
                            ? ctx.Drivers.AsNoTracking().FirstOrDefault(d => d.DriverId == driverId.Value)
                            : null;

                        var pdfService = new PdfReportService();
                        var bytes = pdfService.GenerateRouteSummaryReport(route, stops, students, bus, driver, slot);

                        var fileName = $"RouteSummary_{route.RouteId}_{slot}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        var path = Path.Combine(outputDirectory, fileName);
                        File.WriteAllBytes(path, bytes);

                        sw.Stop();
                        Log.Information(
                            "[RoutePdfPrinter] Route PDF generated (Path={Path}, Size={SizeBytes} bytes, Stops={StopCount}, Students={StudentCount}, ElapsedMs={Elapsed}, OpId={OpId})",
                            path, bytes.LongLength, stops.Count, students.Count, sw.ElapsedMilliseconds, opId);
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Log.Error(ex, "[RoutePdfPrinter] Failure generating route PDF (ElapsedMs={Elapsed}, OpId={OpId})", sw.ElapsedMilliseconds, opId);
                    throw;
                }
            }
        }
    }
}
