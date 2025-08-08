using System.IO;
using System.Text;
using BusBuddy.Core.Services;
using BusBuddy.Core.Models;
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
        /// Export route schedules to CSV format
        /// </summary>
        public async Task<string> ExportRoutesToCsvAsync()
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

                var routes = routesResult.Value ?? Enumerable.Empty<Route>();

                var fileName = $"BusBuddy_Routes_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Route Name,School,Description,Date,AM Students Count,PM Students Count,AM Student Names,PM Student Names");

                // Data rows
                foreach (var route in routes)
                {
                    var amStudents = students.Where(s => s.AMRoute == route.RouteName).ToList();
                    var pmStudents = students.Where(s => s.PMRoute == route.RouteName).ToList();
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
        /// Generate detailed text report of routes and student assignments
        /// </summary>
        public async Task<string> GenerateRouteReportAsync()
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

                var routes = routesResult.Value ?? Enumerable.Empty<Route>();

                var fileName = $"BusBuddy_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

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
                report.AppendLine($"AM Assigned Students: {students.Count(s => !string.IsNullOrEmpty(s.AMRoute))}");
                report.AppendLine($"PM Assigned Students: {students.Count(s => !string.IsNullOrEmpty(s.PMRoute))}");
                report.AppendLine($"Unassigned Students (AM): {students.Count(s => string.IsNullOrEmpty(s.AMRoute))}");
                report.AppendLine($"Unassigned Students (PM): {students.Count(s => string.IsNullOrEmpty(s.PMRoute))}");
                report.AppendLine();

                // Route details
                report.AppendLine("ROUTE DETAILS");
                report.AppendLine("-".PadRight(30, '-'));

                foreach (var route in routes.OrderBy(r => r.RouteName))
                {
                    var amStudents = students.Where(s => s.AMRoute == route.RouteName).OrderBy(s => s.StudentName).ToList();
                    var pmStudents = students.Where(s => s.PMRoute == route.RouteName).OrderBy(s => s.StudentName).ToList();

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
                    report.AppendLine();
                }

                // Unassigned students
                var unassignedAM = students.Where(s => string.IsNullOrEmpty(s.AMRoute)).OrderBy(s => s.StudentName).ToList();
                var unassignedPM = students.Where(s => string.IsNullOrEmpty(s.PMRoute)).OrderBy(s => s.StudentName).ToList();

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
    }
}
