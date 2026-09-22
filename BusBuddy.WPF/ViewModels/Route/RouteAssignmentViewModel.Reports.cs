using System.IO;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Reports;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Ribbon Report / Print Map use the PdfGrid route sheet.
/// Schedule opens <see cref="RouteScheduleViewModel"/> on the same sheet numbers.
/// </summary>
public partial class RouteAssignmentViewModel
{
    private ICommand? _exportRouteSheetCommand;
    private ICommand? _printRouteSheetCommand;

    public ICommand ExportRouteSheetCommand =>
        _exportRouteSheetCommand ??= new RelayCommand(
            () => SaveRouteSheet(includeMap: false),
            () => SelectedRoute != null);

    /// <summary>PdfGrid sheet with the current map snapshot embedded when one exists.</summary>
    public ICommand PrintRouteSheetCommand =>
        _printRouteSheetCommand ??= new RelayCommand(
            () => SaveRouteSheet(includeMap: true),
            () => SelectedRoute != null);

    private void ResolveSelectedSlotBusAndDriver(out BusBuddy.Core.Models.Bus? bus, out BusBuddy.Core.Models.Driver? driver)
    {
        bus = null;
        driver = null;
        if (SelectedRoute == null)
        {
            return;
        }

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

            return;
        }

        if (SelectedRoute.AMVehicleId.HasValue)
        {
            bus = AvailableBuses.FirstOrDefault(b => b.BusId == SelectedRoute.AMVehicleId.Value);
        }

        if (SelectedRoute.AMDriverId.HasValue)
        {
            driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == SelectedRoute.AMDriverId.Value);
        }
    }

    private RouteSummarySheet BuildSelectedRouteSheet() =>
        BuildSelectedRouteSheet(notRidingStudentIds: null);

    private RouteSummarySheet BuildSelectedRouteSheet(IReadOnlySet<int>? notRidingStudentIds)
    {
        if (SelectedRoute == null)
        {
            throw new InvalidOperationException("A route must be selected to build the sheet.");
        }

        ResolveSelectedSlotBusAndDriver(out var bus, out var driver);
        return RouteSummarySheetBuilder.Build(
            SelectedRoute,
            RouteStops.ToList(),
            AssignedStudentsForSelectedRoute.ToList(),
            bus,
            driver,
            NormalizeTimeSlot(SelectedTimeSlot),
            notRidingStudentIds: notRidingStudentIds);
    }

    private void SaveRouteSheet(bool includeMap)
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
                    mapVm.LatestMapSnapshotPng = null;
                    mapVm.RequestMapSnapshot();
                    mapPng = mapVm.LatestMapSnapshotPng;
                }
            }

            ResolveSelectedSlotBusAndDriver(out var bus, out var driver);

            var pdfBytes = RouteSummaryPdfRenderer.Render(
                SelectedRoute,
                RouteStops.ToList(),
                AssignedStudentsForSelectedRoute.ToList(),
                bus,
                driver,
                NormalizeTimeSlot(SelectedTimeSlot),
                mapPng);

            var displayName = GetRouteDisplayName(SelectedRoute);
            var safeName = string.Join("_", (SelectedRoute.RouteName ?? "Route").Split(Path.GetInvalidFileNameChars()));
            var fileName = $"Route_{safeName}_{SelectedTimeSlot}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var exportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BusBuddy",
                "Printouts");
            Directory.CreateDirectory(exportDir);
            var fullPath = Path.Combine(exportDir, fileName);
            File.WriteAllBytes(fullPath, pdfBytes);

            var previewTitle = displayName + (includeMap ? " route sheet (map)" : " route sheet");
            var previewWindow = new PdfPreviewWindow(pdfBytes, previewTitle);
            DialogOwner.Assign(previewWindow);
            previewWindow.Show();

            var mapNote = mapPng != null ? " (with map)" : string.Empty;
            StatusMessage = $"Opened route sheet preview{mapNote} — saved to Printouts\\{fileName}";
            Logger.Information(
                "Route sheet preview DisplayName={DisplayName} Stops={Stops} Size={SizeBytes} bytes Grid=PdfGrid Preview=true Verb=none File={File} MapEmbedded={HasMap}",
                displayName,
                RouteStops.Count,
                pdfBytes.Length,
                fullPath,
                mapPng != null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Route sheet export failed RouteId={RouteId}", SelectedRoute.RouteId);
            StatusMessage = $"Report failed: {ex.Message}";
            MessageBox.Show($"Could not build the route sheet:\n{ex.Message}", "Report", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
