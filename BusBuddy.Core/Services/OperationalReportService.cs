using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Builds operational PDFs/CSVs from live services and optional Ollama commentary.
    /// </summary>
    public sealed class OperationalReportService : IOperationalReportService
    {
        private static readonly ILogger Logger = Log.ForContext<OperationalReportService>();
        private readonly PdfReportService _pdf;
        private readonly IStudentService _students;
        private readonly IRouteService _routes;
        private readonly IDriverService? _drivers;
        private readonly IBusService? _buses;
        private readonly IFuelService? _fuel;
        private readonly IMaintenanceService? _maintenance;
        private readonly GrokGlobalAPI? _grok;

        public OperationalReportService(
            PdfReportService pdf,
            IStudentService students,
            IRouteService routes,
            IDriverService? drivers = null,
            IBusService? buses = null,
            IFuelService? fuel = null,
            IMaintenanceService? maintenance = null,
            GrokGlobalAPI? grok = null)
        {
            _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
            _students = students ?? throw new ArgumentNullException(nameof(students));
            _routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _drivers = drivers;
            _buses = buses;
            _fuel = fuel;
            _maintenance = maintenance;
            _grok = grok;
        }

        public Task<OperationalReportResult> GenerateAsync(OperationalReportKind kind, string? outputDirectory = null) =>
            GenerateAsync(new OperationalReportRequest { Kind = kind, OutputDirectory = outputDirectory });

        public async Task<OperationalReportResult> GenerateAsync(OperationalReportRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var kind = request.Kind;
            var students = await _students.GetAllStudentsAsync().ConfigureAwait(false) ?? new List<Student>();
            var routesResult = await _routes.GetAllActiveRoutesAsync().ConfigureAwait(false);
            var routes = routesResult.IsSuccess && routesResult.Value is not null
                ? routesResult.Value.ToList()
                : new List<Route>();
            var route = SelectRoute(routes, request.RouteId);
            if (request.RouteId.HasValue && route is null)
            {
                throw new InvalidOperationException($"No active route with RouteId {request.RouteId.Value}.");
            }

            var drivers = _drivers is null ? new List<Driver>() : await _drivers.GetAllDriversAsync().ConfigureAwait(false) ?? new List<Driver>();
            var buses = _buses is null
                ? new List<Bus>()
                : (await _buses.GetAllBusesAsync().ConfigureAwait(false))?.ToList() ?? new List<Bus>();
            var fuel = _fuel is null
                ? new List<Fuel>()
                : (await _fuel.GetAllFuelRecordsAsync().ConfigureAwait(false))?.ToList() ?? new List<Fuel>();
            var maintenance = _maintenance is null
                ? new List<Maintenance>()
                : (await _maintenance.GetAllMaintenanceRecordsAsync().ConfigureAwait(false))?.ToList() ?? new List<Maintenance>();

            var title = DisplayName(kind);
            var (headers, rows, facts) = BuildTable(kind, students, routes, drivers, buses, fuel, maintenance);
            var ai = await TryCommentaryAsync(title, facts).ConfigureAwait(false);
            var isCsv = request.AsCsv || kind is OperationalReportKind.CsvExport or OperationalReportKind.ExcelExport;
            var writeSingleRoutePdf = !isCsv
                && kind == OperationalReportKind.RouteSummary
                && request.RouteId.HasValue
                && route is not null;
            var bytes = isCsv
                ? Encoding.UTF8.GetBytes(ToCsv(headers, rows))
                : writeSingleRoutePdf
                    ? BuildRouteSummaryPdf(route!, students, buses, drivers, ai.Text)
                    : _pdf.GenerateTabularReport(title, headers, rows, ai.Text);
            var reportedRows = writeSingleRoutePdf
                ? students.Count(s =>
                    string.Equals(s.AMRoute, route!.RouteName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.PMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase))
                : rows.Count;

            var path = ResolveOutputPath(request, kind, isCsv);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);

            var prefix = kind.ToString().StartsWith("Print", StringComparison.Ordinal) ? "Saved PDF for print" : "Wrote";
            var routeNote = writeSingleRoutePdf ? $", route {route!.RouteName}" : string.Empty;
            var status = $"{prefix} {title} ({reportedRows} row(s){routeNote}, {bytes.Length} bytes) → {path}";
            Logger.Information("Operational report {Kind} written to {Path} bytes={Bytes}", kind, path, bytes.Length);

            return new OperationalReportResult
            {
                FileBytes = bytes,
                FilePath = path,
                Status = status,
                AiSummary = ai.Text,
                UsedMockAi = ai.Mock
            };
        }

        private static Route? SelectRoute(IReadOnlyList<Route> routes, int? routeId)
        {
            if (routeId.HasValue)
            {
                return routes.FirstOrDefault(r => r.RouteId == routeId.Value);
            }

            return routes.Count > 0 ? routes[0] : null;
        }

        private static string ResolveOutputPath(OperationalReportRequest request, OperationalReportKind kind, bool isCsv)
        {
            var ext = isCsv ? ".csv" : ".pdf";
            if (!string.IsNullOrWhiteSpace(request.OutputFilePath))
            {
                return Path.ChangeExtension(request.OutputFilePath, ext);
            }

            var dir = string.IsNullOrWhiteSpace(request.OutputDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BusBuddy", "Reports")
                : request.OutputDirectory;
            return Path.Combine(dir, $"{kind}-{DateTime.Now:yyyyMMdd-HHmmss}{ext}");
        }

        private byte[] BuildRouteSummaryPdf(
            Route route,
            IReadOnlyList<Student> students,
            IReadOnlyList<Bus> buses,
            IReadOnlyList<Driver> drivers,
            string? notes)
        {
            var assigned = students
                .Where(s => string.Equals(s.AMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(s.PMRoute, route.RouteName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var bus = route.AMVehicleId.HasValue
                ? buses.FirstOrDefault(b => b.BusId == route.AMVehicleId.Value)
                : null;
            var driver = route.AMDriverId.HasValue
                ? drivers.FirstOrDefault(d => d.DriverId == route.AMDriverId.Value)
                : null;
            var pdf = _pdf.GenerateRouteSummaryReport(
                route,
                Array.Empty<RouteStop>(),
                assigned,
                bus,
                driver,
                RouteTimeSlot.AM);
            if (pdf.Length > 0)
            {
                return pdf;
            }

            return _pdf.GenerateTabularReport(
                "Route Summary",
                new[] { "Student", "AM", "PM" },
                assigned.Select(s => (IReadOnlyList<string>)new[] { s.StudentName, s.AMRoute ?? "", s.PMRoute ?? "" }).ToList(),
                notes);
        }

        private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, string Facts) BuildTable(
            OperationalReportKind kind,
            IReadOnlyList<Student> students,
            IReadOnlyList<Route> routes,
            IReadOnlyList<Driver> drivers,
            IReadOnlyList<Bus> buses,
            IReadOnlyList<Fuel> fuel,
            IReadOnlyList<Maintenance> maintenance)
        {
            var unassigned = students.Where(s =>
                string.IsNullOrWhiteSpace(s.AMRoute) && string.IsNullOrWhiteSpace(s.PMRoute)).ToList();

            return kind switch
            {
                OperationalReportKind.StudentRoster or OperationalReportKind.PdfExport
                    or OperationalReportKind.PrintStudentLists or OperationalReportKind.CsvExport
                    or OperationalReportKind.ExcelExport => (
                    new[] { "Name", "Grade", "AM", "PM", "School" },
                    students.Select(s => (IReadOnlyList<string>)new[]
                    {
                        s.StudentName, s.Grade ?? "", s.AMRoute ?? "", s.PMRoute ?? "", s.School ?? ""
                    }).ToList(),
                    $"{students.Count} students; {unassigned.Count} unassigned"),

                OperationalReportKind.StudentRouteAssignment => (
                    new[] { "Name", "AM Route", "PM Route" },
                    students.Select(s => (IReadOnlyList<string>)new[]
                    {
                        s.StudentName, s.AMRoute ?? "(none)", s.PMRoute ?? "(none)"
                    }).ToList(),
                    $"{students.Count(s => !string.IsNullOrWhiteSpace(s.AMRoute) || !string.IsNullOrWhiteSpace(s.PMRoute))} assigned of {students.Count}"),

                OperationalReportKind.EnrollmentSummary => (
                    new[] { "Grade", "Count" },
                    students.GroupBy(s => string.IsNullOrWhiteSpace(s.Grade) ? "(none)" : s.Grade!)
                        .OrderBy(g => g.Key)
                        .Select(g => (IReadOnlyList<string>)new[] { g.Key, g.Count().ToString(CultureInfo.InvariantCulture) })
                        .ToList(),
                    $"{students.Count} enrolled across {students.Select(s => s.Grade).Distinct().Count()} grades"),

                OperationalReportKind.UnassignedStudents => (
                    new[] { "Name", "Grade", "Address" },
                    unassigned.Select(s => (IReadOnlyList<string>)new[]
                    {
                        s.StudentName, s.Grade ?? "", s.HomeAddress ?? ""
                    }).ToList(),
                    $"{unassigned.Count} students missing AM and PM routes"),

                OperationalReportKind.RouteSummary or OperationalReportKind.DailySchedule
                    or OperationalReportKind.PrintSchedules or OperationalReportKind.PrintRouteMaps => (
                    new[] { "Route", "School", "AM riders", "PM riders" },
                    routes.Select(r => (IReadOnlyList<string>)new[]
                    {
                        r.RouteName ?? "",
                        r.School ?? "",
                        students.Count(s => string.Equals(s.AMRoute, r.RouteName, StringComparison.OrdinalIgnoreCase))
                            .ToString(CultureInfo.InvariantCulture),
                        students.Count(s => string.Equals(s.PMRoute, r.RouteName, StringComparison.OrdinalIgnoreCase))
                            .ToString(CultureInfo.InvariantCulture)
                    }).ToList(),
                    $"{routes.Count} active routes"),

                OperationalReportKind.VehicleAssignment => (
                    new[] { "Route", "AM bus", "PM bus" },
                    routes.Select(r => (IReadOnlyList<string>)new[]
                    {
                        r.RouteName ?? "",
                        BusNumber(buses, r.AMVehicleId),
                        BusNumber(buses, r.PMVehicleId)
                    }).ToList(),
                    $"{routes.Count(r => r.AMVehicleId.HasValue || r.PMVehicleId.HasValue)} routes with a vehicle"),

                OperationalReportKind.RouteEfficiency => (
                    new[] { "Route", "Assigned", "Notes" },
                    routes.Select(r =>
                    {
                        var count = students.Count(s =>
                            string.Equals(s.AMRoute, r.RouteName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(s.PMRoute, r.RouteName, StringComparison.OrdinalIgnoreCase));
                        return (IReadOnlyList<string>)new[]
                        {
                            r.RouteName ?? "",
                            count.ToString(CultureInfo.InvariantCulture),
                            count == 0 ? "empty" : "in service"
                        };
                    }).ToList(),
                    $"{routes.Count} routes; {unassigned.Count} still unassigned"),

                OperationalReportKind.DriverRoster or OperationalReportKind.Compliance => (
                    new[] { "Driver", "Status", "Training", "License" },
                    drivers.Select(d => (IReadOnlyList<string>)new[]
                    {
                        d.DriverName,
                        d.Status ?? "",
                        d.TrainingComplete ? "Complete" : "Incomplete",
                        d.LicenseExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""
                    }).ToList(),
                    $"{drivers.Count} drivers; {drivers.Count(d => d.NeedsAttention)} need attention"),

                OperationalReportKind.LicenseExpiration => (
                    new[] { "Driver", "Expires", "Status" },
                    drivers.OrderBy(d => d.LicenseExpiryDate ?? DateTime.MaxValue)
                        .Select(d => (IReadOnlyList<string>)new[]
                        {
                            d.DriverName,
                            d.LicenseExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(none)",
                            d.LicenseStatus ?? ""
                        }).ToList(),
                    $"{drivers.Count(d => d.LicenseExpiryDate.HasValue && d.LicenseExpiryDate.Value < DateTime.Today.AddDays(30))} licenses due in 30 days"),

                OperationalReportKind.TrainingStatus => (
                    new[] { "Driver", "Training" },
                    drivers.Select(d => (IReadOnlyList<string>)new[]
                    {
                        d.DriverName,
                        d.TrainingComplete ? "Complete" : "Incomplete"
                    }).ToList(),
                    $"{drivers.Count(d => !d.TrainingComplete)} drivers missing training"),

                OperationalReportKind.FleetInventory or OperationalReportKind.FleetUtilization => (
                    new[] { "Bus", "Status", "Seats", "Year" },
                    buses.Select(b => (IReadOnlyList<string>)new[]
                    {
                        b.BusNumber ?? "",
                        b.Status ?? "",
                        b.SeatingCapacity.ToString(CultureInfo.InvariantCulture),
                        b.Year.ToString(CultureInfo.InvariantCulture)
                    }).ToList(),
                    $"{buses.Count} buses; {buses.Count(b => string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase))} active"),

                OperationalReportKind.MaintenanceSchedule => (
                    new[] { "Date", "Vehicle", "Work", "Priority" },
                    maintenance.OrderByDescending(m => m.Date).Take(40)
                        .Select(m => (IReadOnlyList<string>)new[]
                        {
                            m.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            m.VehicleId.ToString(CultureInfo.InvariantCulture),
                            m.MaintenanceCompleted ?? "",
                            m.Priority ?? ""
                        }).ToList(),
                    $"{maintenance.Count} maintenance records"),

                OperationalReportKind.FuelUsage => (
                    new[] { "Date", "Vehicle", "Gallons", "Cost" },
                    fuel.OrderByDescending(f => f.FuelDate).Take(40)
                        .Select(f => (IReadOnlyList<string>)new[]
                        {
                            f.FuelDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            f.VehicleFueledId.ToString(CultureInfo.InvariantCulture),
                            (f.Gallons ?? 0).ToString("0.0", CultureInfo.InvariantCulture),
                            (f.TotalCost ?? 0).ToString("0.00", CultureInfo.InvariantCulture)
                        }).ToList(),
                    $"{fuel.Count} fuel records"),

                _ => (
                    new[] { "Item", "Value" },
                    new List<IReadOnlyList<string>>
                    {
                        new[] { "Students", students.Count.ToString(CultureInfo.InvariantCulture) },
                        new[] { "Routes", routes.Count.ToString(CultureInfo.InvariantCulture) }
                    },
                    $"{students.Count} students, {routes.Count} routes")
            };
        }

        private static string BusNumber(IReadOnlyList<Bus> buses, int? id) =>
            id.HasValue ? buses.FirstOrDefault(b => b.BusId == id.Value)?.BusNumber ?? id.Value.ToString(CultureInfo.InvariantCulture) : "(none)";

        private static string DisplayName(OperationalReportKind kind) => kind switch
        {
            OperationalReportKind.StudentRoster => "Student Roster",
            OperationalReportKind.StudentRouteAssignment => "Student Route Assignments",
            OperationalReportKind.EnrollmentSummary => "Enrollment Summary",
            OperationalReportKind.UnassignedStudents => "Unassigned Students",
            OperationalReportKind.RouteSummary => "Route Summary",
            OperationalReportKind.DailySchedule => "Daily Schedule",
            OperationalReportKind.VehicleAssignment => "Vehicle Assignment",
            OperationalReportKind.RouteEfficiency => "Route Efficiency",
            OperationalReportKind.DriverRoster => "Driver Roster",
            OperationalReportKind.LicenseExpiration => "License Expiration",
            OperationalReportKind.TrainingStatus => "Training Status",
            OperationalReportKind.Compliance => "Compliance",
            OperationalReportKind.FleetInventory => "Fleet Inventory",
            OperationalReportKind.MaintenanceSchedule => "Maintenance Schedule",
            OperationalReportKind.FuelUsage => "Fuel Usage",
            OperationalReportKind.FleetUtilization => "Fleet Utilization",
            OperationalReportKind.CsvExport => "CSV Export",
            OperationalReportKind.PdfExport => "PDF Export",
            OperationalReportKind.ExcelExport => "Excel-ready CSV",
            OperationalReportKind.PrintStudentLists => "Student Lists",
            OperationalReportKind.PrintRouteMaps => "Route List",
            OperationalReportKind.PrintSchedules => "Schedules",
            _ => kind.ToString()
        };

        private static string ToCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', headers.Select(Csv)));
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(',', row.Select(Csv)));
            }

            return sb.ToString();
        }

        private static string Csv(string? value)
        {
            value ??= string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            }

            return value;
        }

        private async Task<(string Text, bool Mock)> TryCommentaryAsync(string topic, string facts)
        {
            if (_grok is null)
            {
                return ($"{topic}: {facts}", true);
            }

            try
            {
                var text = await _grok.GetShortCommentaryAsync(topic, facts).ConfigureAwait(false);
                var mock = text.StartsWith("Mock insight", StringComparison.OrdinalIgnoreCase);
                return (text, mock);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AI commentary skipped for {Topic}", topic);
                return ($"{topic}: {facts}", true);
            }
        }
    }
}
