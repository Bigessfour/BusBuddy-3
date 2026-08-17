using System;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Maps CLI / help aliases (Roster, RouteManifest, …) and enum names to <see cref="OperationalReportKind"/>.
    /// </summary>
    public static class OperationalReportKindParser
    {
        public static bool TryParse(string? value, out OperationalReportKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var key = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            if (Enum.TryParse(key, ignoreCase: true, out kind))
            {
                return true;
            }

            kind = key.ToLowerInvariant() switch
            {
                "roster" or "studentlist" or "studentlists" => OperationalReportKind.StudentRoster,
                "routemanifest" or "routesummary" or "routemap" or "routemaps" => OperationalReportKind.RouteSummary,
                "driverschedule" or "driverroster" => OperationalReportKind.DriverRoster,
                "schedule" or "schedules" or "dailyschedule" => OperationalReportKind.DailySchedule,
                "csv" or "csvexport" => OperationalReportKind.CsvExport,
                "excel" or "excelexport" or "xlsx" => OperationalReportKind.ExcelExport,
                "pdf" or "pdfexport" => OperationalReportKind.PdfExport,
                _ => (OperationalReportKind)(-1)
            };

            return (int)kind >= 0;
        }

        public static bool IsCsvFormat(string? format) =>
            format is not null &&
            (format.Equals("csv", StringComparison.OrdinalIgnoreCase)
             || format.Equals("excel", StringComparison.OrdinalIgnoreCase)
             || format.Equals("xlsx", StringComparison.OrdinalIgnoreCase));
    }
}
