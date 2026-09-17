using System.IO;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Reports;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Ribbon Report / Print Map / Schedule use the PdfGrid route sheet,
/// not PdfReportService DrawString layout.
/// </summary>
public partial class RouteAssignmentViewModel
{
    private ICommand? _exportRouteSheetCommand;
    private ICommand? _printRouteSheetCommand;
    private ICommand? _viewRouteTimetableCommand;

    public ICommand ExportRouteSheetCommand =>
        _exportRouteSheetCommand ??= new RelayCommand(
            () => SaveRouteSheet(includeMap: false, preview: false),
            () => SelectedRoute != null);

    public ICommand PrintRouteSheetCommand =>
        _printRouteSheetCommand ??= new RelayCommand(
            () => SaveRouteSheet(includeMap: true, preview: false),
            () => SelectedRoute != null);

    /// <summary>On-screen timetable: PdfGrid sheet in the Syncfusion viewer.</summary>
    public ICommand ViewRouteTimetableCommand =>
        _viewRouteTimetableCommand ??= new RelayCommand(
            () => SaveRouteSheet(includeMap: false, preview: true),
            () => SelectedRoute != null);

    private void SaveRouteSheet(bool includeMap, bool preview)
    {
        if (SelectedRoute == null)
        {
            MessageBox.Show("Select a route first.", "Route required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            byte[]? mapPng = null;
            if (includeMap)
            {
                var mapVm = _map;
                if (mapVm != null)
                {
                    if (mapVm.LatestMapSnapshotPng == null || mapVm.LatestMapSnapshotPng.Length == 0)
                    {
                        TryProactiveMapSnapshotCapture();
                    }

                    mapPng = mapVm.LatestMapSnapshotPng;
                }
            }

            BusBuddy.Core.Models.Bus? bus = null;
            BusBuddy.Core.Models.Driver? driver = null;
            if (SelectedTimeSlot == BusBuddy.Core.Models.RouteTimeSlot.PM)
            {
                if (SelectedRoute.PMVehicleId.HasValue)
                {
                    bus = AvailableBuses.FirstOrDefault(b => b.BusId == SelectedRoute.PMVehicleId.Value);
                }

                if (SelectedRoute.PMDriverId.HasValue)
                {
                    driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.PMDriverId.Value);
                }
            }
            else
            {
                if (SelectedRoute.AMVehicleId.HasValue)
                {
                    bus = AvailableBuses.FirstOrDefault(b => b.BusId == SelectedRoute.AMVehicleId.Value);
                }

                if (SelectedRoute.AMDriverId.HasValue)
                {
                    driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.AMDriverId.Value);
                }
            }

            var pdfBytes = RouteSummaryPdfRenderer.Render(
                SelectedRoute,
                RouteStops.ToList(),
                AssignedStudentsForSelectedRoute.ToList(),
                bus,
                driver,
                NormalizeTimeSlot(SelectedTimeSlot),
                mapPng);

            if (preview)
            {
                var previewWindow = new PdfPreviewWindow(pdfBytes, GetRouteDisplayName(SelectedRoute) + " schedule");
                DialogOwner.Assign(previewWindow);
                previewWindow.Show();
                StatusMessage = $"Schedule preview: {RouteStops.Count} stops, {AssignedStudentCount} students";
                return;
            }

            var safeName = string.Join("_", (SelectedRoute.RouteName ?? "Route").Split(Path.GetInvalidFileNameChars()));
            var fileName = $"Route_{safeName}_{SelectedTimeSlot}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var exportDir = Path.Combine(AppContext.BaseDirectory, "Exports");
            Directory.CreateDirectory(exportDir);
            var fullPath = Path.Combine(exportDir, fileName);
            File.WriteAllBytes(fullPath, pdfBytes);
            StatusMessage = $"Route PDF exported: {fileName}" + (mapPng != null ? " (with map)" : string.Empty);
            Logger.Information(
                "Route PDF export complete: {File} (MapEmbedded={HasMap}) Size={SizeBytes} bytes Grid=PdfGrid",
                fullPath,
                mapPng != null,
                pdfBytes.Length);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Route sheet export failed RouteId={RouteId}", SelectedRoute.RouteId);
            StatusMessage = $"Report failed: {ex.Message}";
            MessageBox.Show($"Could not build the route sheet:\n{ex.Message}", "Report", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
