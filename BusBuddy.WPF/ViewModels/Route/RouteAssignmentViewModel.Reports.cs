using System.IO;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.WPF;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Reports;
using Microsoft.Extensions.DependencyInjection;

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
            async () => await SaveRouteSheetAsync(includeMap: false),
            () => SelectedRoute != null);

    /// <summary>PdfGrid sheet with the current map snapshot embedded when one exists.</summary>
    public ICommand PrintRouteSheetCommand =>
        _printRouteSheetCommand ??= new RelayCommand(
            async () => await SaveRouteSheetAsync(includeMap: true),
            () => SelectedRoute != null);

    private IReadOnlyList<BusBuddy.Core.Models.Schedule> _datedSchedulesForSelected =
        Array.Empty<BusBuddy.Core.Models.Schedule>();

    private async Task RefreshDatedSchedulesAsync()
    {
        if (SelectedRoute is null)
        {
            _datedSchedulesForSelected = Array.Empty<BusBuddy.Core.Models.Schedule>();
            return;
        }

        var schedules = App.ServiceProvider?.GetService<IScheduleService>();
        if (schedules is null)
        {
            _datedSchedulesForSelected = Array.Empty<BusBuddy.Core.Models.Schedule>();
            return;
        }

        _datedSchedulesForSelected = await schedules
            .GetSchedulesForRouteOnDateAsync(
                SelectedRoute.RouteId,
                RouteDailyPairing.ServiceDateUtc(SelectedRoute))
            .ConfigureAwait(true);
    }

    private void ResolveSelectedSlotBusAndDriver(out BusBuddy.Core.Models.Bus? bus, out BusBuddy.Core.Models.Driver? driver)
    {
        bus = null;
        driver = null;
        if (SelectedRoute == null)
        {
            return;
        }

        var pairing = RouteDailyPairing.Resolve(
            SelectedRoute,
            NormalizeTimeSlot(SelectedTimeSlot),
            _datedSchedulesForSelected);

        var nav = RouteDailyPairing.PreferActiveNavMatch(
            _datedSchedulesForSelected,
            pairing.BusId,
            pairing.DriverId,
            pairing.FromSchedule);

        if (pairing.BusId is int busId)
        {
            bus = AvailableBuses.FirstOrDefault(b => b.BusId == busId) ?? nav?.Bus;
        }

        if (pairing.DriverId is int driverId)
        {
            driver = AvailableDrivers.FirstOrDefault(d => d.DriverId == driverId) ?? nav?.Driver;
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

    private async Task<bool> SaveRouteSheetAsync(bool includeMap)
    {
        if (SelectedRoute == null)
        {
            MessageBox.Show("Select a route first.", "Route required", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        try
        {
            await RefreshDatedSchedulesAsync().ConfigureAwait(true);

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
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Route sheet export failed RouteId={RouteId}", SelectedRoute.RouteId);
            StatusMessage = $"Report failed: {ex.Message}";
            MessageBox.Show($"Could not build the route sheet:\n{ex.Message}", "Report", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
