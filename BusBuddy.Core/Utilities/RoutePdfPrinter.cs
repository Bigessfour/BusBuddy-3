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
    /// Minimal MVP helper to generate a Route Summary PDF for the first active route found.
    /// Not wired to UI; invoked manually from Program or a quick REPL for smoke verification.
    /// </summary>
    public static class RoutePdfPrinter
    {
        public static string GenerateFirstActiveRoutePdf(IBusBuddyDbContextFactory contextFactory, string outputDirectory, RouteTimeSlot slot = RouteTimeSlot.AM)
        {
            var opId = Guid.NewGuid().ToString("N");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ArgumentNullException.ThrowIfNull(contextFactory);
            if (string.IsNullOrWhiteSpace(outputDirectory)) outputDirectory = Environment.CurrentDirectory;

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
            {
                try
                {
                    Log.Information("[RoutePdfPrinter] Begin route PDF generation (OpId={OpId}, OutputDir={Dir}, Slot={Slot})", opId, outputDirectory, slot);

                    var route = ctx.Routes.AsNoTracking().OrderBy(r => r.RouteId).FirstOrDefault(r => r.IsActive);
                    if (route == null)
                    {
                        Log.Warning("[RoutePdfPrinter] No active route found (OpId={OpId})", opId);
                        throw new InvalidOperationException("No active route found to export.");
                    }

                    using (Serilog.Context.LogContext.PushProperty("RouteId", route.RouteId))
                    using (Serilog.Context.LogContext.PushProperty("RouteName", route.RouteName))
                    {
                        Log.Debug("[RoutePdfPrinter] Selected active route {RouteId} - {RouteName} (OpId={OpId})", route.RouteId, route.RouteName, opId);

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

                        if (stops.Count == 0)
                        {
                            Log.Warning("[RoutePdfPrinter] Route {RouteId} has zero stops (OpId={OpId})", route.RouteId, opId);
                        }
                        if (students.Count == 0)
                        {
                            Log.Information("[RoutePdfPrinter] No students matched for route {RouteId} and slot {Slot} (OpId={OpId})", route.RouteId, slot, opId);
                        }

                        Bus? bus = null; // Future: resolve assignment
                        Driver? driver = null; // Future: resolve assignment

                        var pdfService = new PdfReportService();
                        Log.Debug("[RoutePdfPrinter] Invoking PdfReportService.GenerateRouteSummaryReport (OpId={OpId})");
                        var bytes = pdfService.GenerateRouteSummaryReport(route, stops, students, bus, driver, slot);

                        var fileName = $"RouteSummary_{route.RouteId}_{slot}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        var path = Path.Combine(outputDirectory, fileName);
                        try
                        {
                            File.WriteAllBytes(path, bytes);
                        }
                        catch (Exception ioEx)
                        {
                            Log.Error(ioEx, "[RoutePdfPrinter] Failed writing PDF to {Path} (OpId={OpId})", path, opId);
                            throw;
                        }

                        sw.Stop();
                        Log.Information("[RoutePdfPrinter] Route PDF generated (Path={Path}, Size={SizeBytes} bytes, Stops={StopCount}, Students={StudentCount}, ElapsedMs={Elapsed}, OpId={OpId})",
                            path,
                            bytes.LongLength,
                            stops.Count,
                            students.Count,
                            sw.ElapsedMilliseconds,
                            opId);
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
