using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.WPF;
using BusBuddy.WPF.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Reports
{
    /// <summary>
    /// ViewModel for the Reports view - manages report generation and analytics
    /// Provides access to various reports for students, routes, drivers, and fleet
    /// </summary>
    public class ReportsViewModel : BaseViewModel
    {
        private readonly IOperationalReportService _reportService;
        private bool _isGeneratingReport;
        private string _lastReportGenerated = "None";
        private string _aiReportSummary = "Run a report to see local AI insights (Ollama, with mock fallback).";

        public ReportsViewModel()
            : this(CreateDefaultReportService())
        {
        }

        public ReportsViewModel(IOperationalReportService reportService)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            InitializeCommands();
            StatusMessage = "Ready to generate reports";
        }

        private static IOperationalReportService CreateDefaultReportService()
        {
            var sp = App.ServiceProvider;
            return sp?.GetService<IOperationalReportService>()
                ?? new OperationalReportService(
                    new PdfReportService(),
                    sp?.GetService<IStudentService>() ?? new StudentService(new BusBuddy.Core.Data.BusBuddyDbContextFactory()),
                    sp?.GetService<IRouteService>() ?? new RouteService(new BusBuddy.Core.Data.BusBuddyDbContextFactory()),
                    sp?.GetService<IDriverService>(),
                    sp?.GetService<BusBuddy.Core.Services.Interfaces.IBusService>(),
                    sp?.GetService<IFuelService>(),
                    sp?.GetService<IMaintenanceService>(),
                    sp?.GetService<GrokGlobalAPI>());
        }

        #region Properties

        /// <summary>
        /// Indicates if a report is currently being generated
        /// </summary>
        public bool IsGeneratingReport
        {
            get => _isGeneratingReport;
            set => SetProperty(ref _isGeneratingReport, value);
        }

        /// <summary>
        /// Timestamp of the last generated report
        /// </summary>
        public string LastReportGenerated
        {
            get => _lastReportGenerated;
            set => SetProperty(ref _lastReportGenerated, value);
        }

        /// <summary>
        /// History of generated reports for SfDataGrid binding (Finish UI element)
        /// </summary>
        public ObservableCollection<ReportEntry> GeneratedReports { get; } = new ObservableCollection<ReportEntry>();

        /// <summary>
        /// AI-powered summary from Ollama / GrokGlobalAPI (mock fallback when offline).
        /// </summary>
        public string AIReportSummary
        {
            get => _aiReportSummary;
            private set => SetProperty(ref _aiReportSummary, value);
        }

        #endregion

        #region Commands

        // Student Reports
        public ICommand GenerateStudentRosterCommand { get; private set; } = null!;
        public ICommand GenerateStudentRouteReportCommand { get; private set; } = null!;
        public ICommand GenerateEnrollmentSummaryCommand { get; private set; } = null!;
        public ICommand GenerateUnassignedStudentsCommand { get; private set; } = null!;

        // Route Reports
        public ICommand GenerateRouteSummaryCommand { get; private set; } = null!;
        public ICommand GenerateDailyScheduleCommand { get; private set; } = null!;
        public ICommand GenerateVehicleAssignmentCommand { get; private set; } = null!;
        public ICommand GenerateRouteEfficiencyCommand { get; private set; } = null!;

        // Driver Reports
        public ICommand GenerateDriverRosterCommand { get; private set; } = null!;
        public ICommand GenerateLicenseExpirationCommand { get; private set; } = null!;
        public ICommand GenerateTrainingStatusCommand { get; private set; } = null!;
        public ICommand GenerateComplianceReportCommand { get; private set; } = null!;

        // Fleet Reports
        public ICommand GenerateFleetInventoryCommand { get; private set; } = null!;
        public ICommand GenerateMaintenanceScheduleCommand { get; private set; } = null!;
        public ICommand GenerateFuelUsageCommand { get; private set; } = null!;
        public ICommand GenerateFleetUtilizationCommand { get; private set; } = null!;

        // Export and Print Commands
        public ICommand ExportAllDataToCsvCommand { get; private set; } = null!;
        public ICommand ExportAllDataToPdfCommand { get; private set; } = null!;
        public ICommand ExportAllDataToExcelCommand { get; private set; } = null!;
        public ICommand PrintStudentListsCommand { get; private set; } = null!;
        public ICommand PrintRouteMapsCommand { get; private set; } = null!;
        public ICommand PrintSchedulesCommand { get; private set; } = null!;

        #endregion

        #region Command Initialization

        private void InitializeCommands()
        {
            // Student Reports
            GenerateStudentRosterCommand = new AsyncRelayCommand(ExecuteGenerateStudentRosterAsync);
            GenerateStudentRouteReportCommand = new AsyncRelayCommand(ExecuteGenerateStudentRouteReportAsync);
            GenerateEnrollmentSummaryCommand = new AsyncRelayCommand(ExecuteGenerateEnrollmentSummaryAsync);
            GenerateUnassignedStudentsCommand = new AsyncRelayCommand(ExecuteGenerateUnassignedStudentsAsync);

            // Route Reports
            GenerateRouteSummaryCommand = new AsyncRelayCommand(ExecuteGenerateRouteSummaryAsync);
            GenerateDailyScheduleCommand = new AsyncRelayCommand(ExecuteGenerateDailyScheduleAsync);
            GenerateVehicleAssignmentCommand = new AsyncRelayCommand(ExecuteGenerateVehicleAssignmentAsync);
            GenerateRouteEfficiencyCommand = new AsyncRelayCommand(ExecuteGenerateRouteEfficiencyAsync);

            // Driver Reports
            GenerateDriverRosterCommand = new AsyncRelayCommand(ExecuteGenerateDriverRosterAsync);
            GenerateLicenseExpirationCommand = new AsyncRelayCommand(ExecuteGenerateLicenseExpirationAsync);
            GenerateTrainingStatusCommand = new AsyncRelayCommand(ExecuteGenerateTrainingStatusAsync);
            GenerateComplianceReportCommand = new AsyncRelayCommand(ExecuteGenerateComplianceReportAsync);

            // Fleet Reports
            GenerateFleetInventoryCommand = new AsyncRelayCommand(ExecuteGenerateFleetInventoryAsync);
            GenerateMaintenanceScheduleCommand = new AsyncRelayCommand(ExecuteGenerateMaintenanceScheduleAsync);
            GenerateFuelUsageCommand = new AsyncRelayCommand(ExecuteGenerateFuelUsageAsync);
            GenerateFleetUtilizationCommand = new AsyncRelayCommand(ExecuteGenerateFleetUtilizationAsync);

            // Export and Print Commands
            ExportAllDataToCsvCommand = new AsyncRelayCommand(ExecuteExportAllDataToCsvAsync);
            ExportAllDataToPdfCommand = new AsyncRelayCommand(ExecuteExportAllDataToPdfAsync);
            ExportAllDataToExcelCommand = new AsyncRelayCommand(ExecuteExportAllDataToExcelAsync);
            PrintStudentListsCommand = new AsyncRelayCommand(ExecutePrintStudentListsAsync);
            PrintRouteMapsCommand = new AsyncRelayCommand(ExecutePrintRouteMapsAsync);
            PrintSchedulesCommand = new AsyncRelayCommand(ExecutePrintSchedulesAsync);
        }

        #endregion

        #region Student Report Commands

        private Task ExecuteGenerateStudentRosterAsync() =>
            ExecuteKindAsync(OperationalReportKind.StudentRoster, "Student Roster Report");

        private Task ExecuteGenerateStudentRouteReportAsync() =>
            ExecuteKindAsync(OperationalReportKind.StudentRouteAssignment, "Student Route Assignment Report");

        private Task ExecuteGenerateEnrollmentSummaryAsync() =>
            ExecuteKindAsync(OperationalReportKind.EnrollmentSummary, "Enrollment Summary Report");

        private Task ExecuteGenerateUnassignedStudentsAsync() =>
            ExecuteKindAsync(OperationalReportKind.UnassignedStudents, "Unassigned Students Report");

        #endregion

        #region Route Report Commands

        private Task ExecuteGenerateRouteSummaryAsync() =>
            ExecuteKindAsync(OperationalReportKind.RouteSummary, "Route Summary Report");

        private Task ExecuteGenerateDailyScheduleAsync() =>
            ExecuteKindAsync(OperationalReportKind.DailySchedule, "Daily Schedule Report");

        private Task ExecuteGenerateVehicleAssignmentAsync() =>
            ExecuteKindAsync(OperationalReportKind.VehicleAssignment, "Vehicle Assignment Report");

        private Task ExecuteGenerateRouteEfficiencyAsync() =>
            ExecuteKindAsync(OperationalReportKind.RouteEfficiency, "Route Efficiency Report");

        #endregion

        #region Driver Report Commands

        private Task ExecuteGenerateDriverRosterAsync() =>
            ExecuteKindAsync(OperationalReportKind.DriverRoster, "Driver Roster Report");

        private Task ExecuteGenerateLicenseExpirationAsync() =>
            ExecuteKindAsync(OperationalReportKind.LicenseExpiration, "License Expiration Report");

        private Task ExecuteGenerateTrainingStatusAsync() =>
            ExecuteKindAsync(OperationalReportKind.TrainingStatus, "Training Status Report");

        private Task ExecuteGenerateComplianceReportAsync() =>
            ExecuteKindAsync(OperationalReportKind.Compliance, "Compliance Report");

        #endregion

        #region Fleet Report Commands

        private Task ExecuteGenerateFleetInventoryAsync() =>
            ExecuteKindAsync(OperationalReportKind.FleetInventory, "Fleet Inventory Report");

        private Task ExecuteGenerateMaintenanceScheduleAsync() =>
            ExecuteKindAsync(OperationalReportKind.MaintenanceSchedule, "Maintenance Schedule Report");

        private Task ExecuteGenerateFuelUsageAsync() =>
            ExecuteKindAsync(OperationalReportKind.FuelUsage, "Fuel Usage Report");

        private Task ExecuteGenerateFleetUtilizationAsync() =>
            ExecuteKindAsync(OperationalReportKind.FleetUtilization, "Fleet Utilization Report");

        #endregion

        #region Export and Print Commands

        private Task ExecuteExportAllDataToCsvAsync() =>
            ExecuteKindAsync(OperationalReportKind.CsvExport, "CSV Export");

        private Task ExecuteExportAllDataToPdfAsync() =>
            ExecuteKindAsync(OperationalReportKind.PdfExport, "PDF Export");

        private Task ExecuteExportAllDataToExcelAsync() =>
            ExecuteKindAsync(OperationalReportKind.ExcelExport, "Excel Export");

        private Task ExecutePrintStudentListsAsync() =>
            ExecuteKindAsync(OperationalReportKind.PrintStudentLists, "Print Student Lists");

        private Task ExecutePrintRouteMapsAsync() =>
            ExecuteKindAsync(OperationalReportKind.PrintRouteMaps, "Print Route Maps");

        private Task ExecutePrintSchedulesAsync() =>
            ExecuteKindAsync(OperationalReportKind.PrintSchedules, "Print Schedules");

        #endregion

        #region Helper Methods

        private Task ExecuteKindAsync(OperationalReportKind kind, string reportName) =>
            ExecuteReportGeneration(reportName, async () =>
            {
                var generated = await _reportService.GenerateAsync(kind);
                AIReportSummary = generated.AiSummary;
                return generated.Status;
            });

        private async Task ExecuteReportGeneration(string reportName, Func<Task<string>> reportAction)
        {
            try
            {
                Logger.Information("Starting execution of report command: {Report}", reportName);
                IsGeneratingReport = true;
                StatusMessage = $"Generating {reportName}...";

                var result = await reportAction();

                StatusMessage = result;
                LastReportGenerated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                GeneratedReports.Insert(0, new ReportEntry
                {
                    Name = reportName,
                    GeneratedAt = DateTime.Now.ToString("HH:mm:ss"),
                    Result = result
                });
                if (GeneratedReports.Count > 8)
                {
                    GeneratedReports.RemoveAt(GeneratedReports.Count - 1);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating report: {ReportName}", reportName);
                StatusMessage = $"Error generating {reportName}: {ex.Message}";
            }
            finally
            {
                IsGeneratingReport = false;
            }
        }

        #endregion

        /// <summary>
        /// Simple entry model for GeneratedReports SfDataGrid (Finish UI proof; no new files)
        /// </summary>
        public class ReportEntry
        {
            public string Name { get; set; } = "";
            public string GeneratedAt { get; set; } = "";
            public string Result { get; set; } = "";
        }
    }
}
