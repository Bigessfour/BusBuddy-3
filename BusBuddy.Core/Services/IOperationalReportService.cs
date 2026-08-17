using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    public enum OperationalReportKind
    {
        StudentRoster,
        StudentRouteAssignment,
        EnrollmentSummary,
        UnassignedStudents,
        RouteSummary,
        DailySchedule,
        VehicleAssignment,
        RouteEfficiency,
        DriverRoster,
        LicenseExpiration,
        TrainingStatus,
        Compliance,
        FleetInventory,
        MaintenanceSchedule,
        FuelUsage,
        FleetUtilization,
        CsvExport,
        PdfExport,
        ExcelExport,
        PrintStudentLists,
        PrintRouteMaps,
        PrintSchedules
    }

    public sealed class OperationalReportResult
    {
        public byte[] FileBytes { get; init; } = [];
        public string FilePath { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string AiSummary { get; init; } = string.Empty;
        public bool UsedMockAi { get; init; }
    }

    public sealed class OperationalReportRequest
    {
        public OperationalReportKind Kind { get; init; }
        public string? OutputDirectory { get; init; }
        public string? OutputFilePath { get; init; }
        public bool AsCsv { get; init; }
        public int? RouteId { get; init; }
    }

    public interface IOperationalReportService
    {
        Task<OperationalReportResult> GenerateAsync(OperationalReportKind kind, string? outputDirectory = null);
        Task<OperationalReportResult> GenerateAsync(OperationalReportRequest request);
    }
}
