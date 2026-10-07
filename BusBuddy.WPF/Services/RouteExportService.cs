using System.IO;
using System.Text;
using BusBuddy.Core.Services;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Serilog;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service for exporting route schedules and student assignments
    /// </summary>
    public class RouteExportService
    {
        private readonly IRouteService _routeService;
        private readonly IStudentService _studentService;
        private readonly ILogger Logger = Log.ForContext<RouteExportService>();

        public RouteExportService(IRouteService routeService, IStudentService studentService)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _studentService = studentService ?? throw new ArgumentNullException(nameof(studentService));
        }

        /// <summary>
        /// Export route schedules to CSV format.
        /// </summary>
        /// <param name="outputPath">
        /// Destination file. Callers that already asked the clerk where to save must pass it: the rows
        /// carry student names, so defaulting to the Desktop would leave an unrequested roster there.
        /// </param>
        public Task<string> ExportRoutesToCsvAsync(string outputPath) =>
            ExportRoutesToCsvAsync(outputPath, onlyRouteIds: null);

        /// <param name="outputPath">Destination file. Callers that already asked the clerk where to save must pass it.</param>
        /// <param name="onlyRouteIds">Null exports every route. An empty list exports the header only.</param>
        public async Task<string> ExportRoutesToCsvAsync(string outputPath, IReadOnlyCollection<int>? onlyRouteIds)
        {
            try
            {
                Logger.Information("Starting route export to CSV");

                var routesResult = await _routeService.GetAllRoutesAsync();
                var students = await _studentService.GetAllStudentsAsync();

                if (!routesResult.IsSuccess)
                {
                    throw new InvalidOperationException($"Failed to load routes: {routesResult.Error}");
                }

                var routes = LimitRoutes(routesResult.Value, onlyRouteIds);

                var filePath = ResolveOutputPath(outputPath);

                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Route Name,School,Description,Date,AM Students Count,PM Students Count,AM Student Names,PM Student Names");

                // Data rows
                foreach (var route in routes)
                {
                    var amStudents = students.Where(s => StudentRouteAssignment.Matches(s, route, RouteTimeSlot.AM)).ToList();
                    var pmStudents = students.Where(s => StudentRouteAssignment.Matches(s, route, RouteTimeSlot.PM)).ToList();
                    var amStudentNames = string.Join("; ", amStudents.Select(s => s.StudentName));
                    var pmStudentNames = string.Join("; ", pmStudents.Select(s => s.StudentName));

                    csv.AppendLine($"\"{route.RouteName}\",\"{route.School}\",\"{route.Description}\"," +
                                  $"\"{route.Date:yyyy-MM-dd}\",{amStudents.Count},{pmStudents.Count},\"{amStudentNames}\",\"{pmStudentNames}\"");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString());

                Logger.Information("Route export completed: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error exporting routes to CSV");
                throw;
            }
        }

        /// <summary>
        /// Generate detailed text report of routes and student assignments.
        /// </summary>
        /// <param name="outputPath">Destination file. Callers that already asked the clerk where to save must pass it.</param>
        public Task<string> GenerateRouteReportAsync(string outputPath) =>
            GenerateRouteReportAsync(outputPath, onlyRouteIds: null);

        /// <param name="outputPath">Destination file. Callers that already asked the clerk where to save must pass it.</param>
        /// <param name="onlyRouteIds">Null includes every route. An empty list writes the summary with no route sections.</param>
        public async Task<string> GenerateRouteReportAsync(string outputPath, IReadOnlyCollection<int>? onlyRouteIds)
        {
            try
            {
                Logger.Information("Generating route report");

                var routesResult = await _routeService.GetAllRoutesAsync();
                var students = await _studentService.GetAllStudentsAsync();

                if (!routesResult.IsSuccess)
                {
                    throw new InvalidOperationException($"Failed to load routes: {routesResult.Error}");
                }

                var routes = LimitRoutes(routesResult.Value, onlyRouteIds);

                var filePath = ResolveOutputPath(outputPath);

                var report = new StringBuilder();

                // Header
                report.AppendLine("BUS BUDDY ROUTE REPORT");
                report.AppendLine("=".PadRight(50, '='));
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine();

                // Summary statistics
                report.AppendLine("SUMMARY");
                report.AppendLine("-".PadRight(30, '-'));
                report.AppendLine($"Total Routes: {routes.Count()}");
                report.AppendLine($"Total Students: {students.Count}");
                report.AppendLine($"AM Assigned Students: {students.Count(s => !StudentRouteAssignment.IsUnassignedAm(s))}");
                report.AppendLine($"PM Assigned Students: {students.Count(s => !StudentRouteAssignment.IsUnassignedPm(s))}");
                report.AppendLine($"Unassigned Students (AM): {students.Count(s => StudentRouteAssignment.IsUnassignedAm(s))}");
                report.AppendLine($"Unassigned Students (PM): {students.Count(s => StudentRouteAssignment.IsUnassignedPm(s))}");
                report.AppendLine();

                // Route details
                report.AppendLine("ROUTE DETAILS");
                report.AppendLine("-".PadRight(30, '-'));

                foreach (var route in routes.OrderBy(r => r.RouteName))
                {
                    var amStudents = students.Where(s => StudentRouteAssignment.Matches(s, route, RouteTimeSlot.AM)).OrderBy(s => s.StudentName).ToList();
                    var pmStudents = students.Where(s => StudentRouteAssignment.Matches(s, route, RouteTimeSlot.PM)).OrderBy(s => s.StudentName).ToList();

                    report.AppendLine($"Route: {route.RouteName}");
                    report.AppendLine($"  School: {route.School}");
                    report.AppendLine($"  Description: {route.Description}");
                    report.AppendLine($"  Date: {route.Date:yyyy-MM-dd}");
                    report.AppendLine($"  AM Students ({amStudents.Count}):");
                    foreach (var student in amStudents)
                    {
                        report.AppendLine($"    - {student.StudentName} (Grade: {student.Grade})");
                    }
                    report.AppendLine($"  PM Students ({pmStudents.Count}):");
                    foreach (var student in pmStudents)
                    {
                        report.AppendLine($"    - {student.StudentName} (Grade: {student.Grade})");
                    }

                    report.AppendLine("  Stops:");
                    var stopsResult = await _routeService.GetRouteStopsAsync(route.RouteId);
                    if (!stopsResult.IsSuccess || stopsResult.Value is null)
                    {
                        report.AppendLine("    (unavailable)");
                    }
                    else
                    {
                        foreach (var stop in stopsResult.Value.OrderBy(s => s.StopOrder))
                        {
                            report.AppendLine(
                                $"    {stop.StopOrder}. {stop.StopName}  {Clock(stop.ScheduledArrival)}-{Clock(stop.ScheduledDeparture)}");
                        }
                    }

                    report.AppendLine();
                }

                // Unassigned students
                var unassignedAM = students.Where(StudentRouteAssignment.IsUnassignedAm).OrderBy(s => s.StudentName).ToList();
                var unassignedPM = students.Where(StudentRouteAssignment.IsUnassignedPm).OrderBy(s => s.StudentName).ToList();

                if (unassignedAM.Any())
                {
                    report.AppendLine("UNASSIGNED STUDENTS (AM)");
                    report.AppendLine("-".PadRight(30, '-'));
                    foreach (var student in unassignedAM)
                    {
                        report.AppendLine($"  - {student.StudentName} (Grade: {student.Grade})");
                    }
                    report.AppendLine();
                }

                if (unassignedPM.Any())
                {
                    report.AppendLine("UNASSIGNED STUDENTS (PM)");
                    report.AppendLine("-".PadRight(30, '-'));
                    foreach (var student in unassignedPM)
                    {
                        report.AppendLine($"  - {student.StudentName} (Grade: {student.Grade})");
                    }
                    report.AppendLine();
                }

                await File.WriteAllTextAsync(filePath, report.ToString());

                Logger.Information("Route report generated: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating route report");
                throw;
            }
        }

        private static IEnumerable<Route> LimitRoutes(IEnumerable<Route>? routes, IReadOnlyCollection<int>? onlyRouteIds)
        {
            var list = routes ?? Enumerable.Empty<Route>();
            if (onlyRouteIds is null)
            {
                return list;
            }

            var ids = onlyRouteIds as ISet<int> ?? onlyRouteIds.ToHashSet();
            return list.Where(route => ids.Contains(route.RouteId));
        }

        private static string Clock(TimeSpan value) =>
            value == default
                ? "none"
                : $"{(int)value.TotalHours:00}:{value.Minutes:00}";

        private static string ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException(
                    "Export path is required. Do not write an unrequested student roster to the Desktop.",
                    nameof(outputPath));
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return outputPath;
        }
    }
}
