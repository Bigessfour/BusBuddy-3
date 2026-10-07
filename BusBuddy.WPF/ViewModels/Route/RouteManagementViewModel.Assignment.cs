using System.IO;
using System.Windows;
using Serilog.Context;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.Views.Reports;

namespace BusBuddy.WPF.ViewModels.Route
{
    public partial class RouteManagementViewModel
    {
        private async Task AssignVehicleAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }
            if (SelectedBus is null && SelectedBusId is int busId)
            {
                SelectedBus = AvailableBuses.FirstOrDefault(b => b.BusId == busId);
            }
            if (SelectedBus is null)
            {
                StatusMessage = "Select a bus to assign";
                return;
            }
            if (IsBusy) return;
            try
            {
                using (LogContext.PushProperty("Operation", "AssignVehicle"))
                using (LogContext.PushProperty("RouteId", SelectedRoute.RouteId))
                {
                    IsBusy = true;
                    StatusMessage = $"Assigning bus {SelectedBus.BusNumber} to route '{SelectedRoute.RouteName}'...";
                    var result = await _routeService.AssignVehicleToRouteAsync(
                        SelectedRoute.RouteId, SelectedBus.BusId, SelectedTimeSlot).ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error) ? "Assignment failed" : result.Error;
                        Logger.Warning("Vehicle assignment failed: {Message}", result.Error);
                        UiProofLog.Write(
                            Logger,
                            "Assign Bus",
                            "RouteManagementView",
                            "failed",
                            result.Error);
                        return;
                    }

                    Logger.Information(
                        "Assigned vehicle {VehicleId} to route {RouteId} for {Slot} ViaService={ViaService}",
                        SelectedBus.BusId, SelectedRoute.RouteId, SelectedTimeSlot, true);
                    UiProofLog.Write(
                        Logger,
                        "Assign Bus",
                        "RouteManagementView",
                        "assigned",
                        $"RouteId={SelectedRoute.RouteId} BusId={SelectedBus.BusId} Slot={SelectedTimeSlot}");
                    await LoadSingleRouteAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                    StatusMessage = $"Assigned bus {SelectedBus.BusNumber} ({SelectedTimeSlot})";
                }
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, "Assign Bus", "RouteManagementView");
                StatusMessage = $"Error assigning vehicle: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AssignDriverAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }

            if (SelectedDriver is null && SelectedDriverId is int driverId)
            {
                SelectedDriver = AvailableDrivers.FirstOrDefault(d => d.DriverId == driverId);
            }

            if (SelectedDriver is null)
            {
                StatusMessage = "Select a driver to assign";
                return;
            }

            if (IsBusy)
            {
                return;
            }

            try
            {
                using (LogContext.PushProperty("Operation", "AssignDriver"))
                using (LogContext.PushProperty("RouteId", SelectedRoute.RouteId))
                {
                    IsBusy = true;
                    StatusMessage =
                        $"Assigning {SelectedDriver.DriverName} to route '{SelectedRoute.RouteName}'...";
                    var result = await _routeService.AssignDriverToRouteAsync(
                        SelectedRoute.RouteId, SelectedDriver.DriverId, SelectedTimeSlot).ConfigureAwait(true);
                    if (!result.IsSuccess)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                            ? "Driver assignment failed"
                            : result.Error;
                        Logger.Warning("Driver assignment failed: {Message}", result.Error);
                        UiProofLog.Write(
                            Logger,
                            "Assign Driver",
                            "RouteManagementView",
                            "failed",
                            result.Error);
                        return;
                    }

                    Logger.Information(
                        "Assigned driver {DriverId} to route {RouteId} for {Slot} ViaService={ViaService}",
                        SelectedDriver.DriverId, SelectedRoute.RouteId, SelectedTimeSlot, true);
                    UiProofLog.Write(
                        Logger,
                        "Assign Driver",
                        "RouteManagementView",
                        "assigned",
                        $"RouteId={SelectedRoute.RouteId} DriverId={SelectedDriver.DriverId} Slot={SelectedTimeSlot}");
                    await LoadSingleRouteAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                    StatusMessage = $"Assigned {SelectedDriver.DriverName} ({SelectedTimeSlot})";
                }
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, "Assign Driver", "RouteManagementView");
                StatusMessage = $"Error assigning driver: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Loads Active / In Service buses for assignment. Reloads so a bus added
        /// in Vehicle Management appears without restarting Route Management.
        /// </summary>
        public async Task EnsureBusesLoadedAsync()
        {
            try
            {
                var result = await _routeService.GetAvailableBusesAsync().ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    Logger.Warning("GetAvailableBusesAsync failed: {Error}", result.Error);
                    return;
                }

                AvailableBuses.Clear();
                foreach (var b in result.Value ?? [])
                {
                    AvailableBuses.Add(b);
                }
                Logger.Debug("Loaded {Count} assignable buses ViaService={ViaService}", AvailableBuses.Count, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading active buses");
            }
        }

        public async Task EnsureDriversLoadedAsync()
        {
            try
            {
                var result = await _routeService.GetAvailableDriversAsync().ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    Logger.Warning("GetAvailableDriversAsync failed: {Error}", result.Error);
                    return;
                }

                AvailableDrivers.Clear();
                foreach (var d in result.Value ?? [])
                {
                    AvailableDrivers.Add(d);
                }

                Logger.Debug("Loaded {Count} assignable drivers ViaService={ViaService}", AvailableDrivers.Count, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading available drivers");
            }
        }

        private List<BusBuddy.Core.Models.Route> VisibleRoutes() =>
            RoutesView.Cast<BusBuddy.Core.Models.Route>().ToList();

        private async Task LoadSingleRouteAsync(int routeId)
        {
            try
            {
                var result = await _routeService.GetRouteByIdAsync(routeId).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    return;
                }

                var updated = result.Value;
                var existing = Routes.FirstOrDefault(r => r.RouteId == routeId);
                if (existing is null)
                {
                    return;
                }

                existing.AMVehicleId = updated.AMVehicleId;
                existing.PMVehicleId = updated.PMVehicleId;
                existing.BusNumber = updated.BusNumber;
                existing.AMDriverId = updated.AMDriverId;
                existing.PMDriverId = updated.PMDriverId;
                existing.StudentCount = updated.StudentCount;
                existing.StopCount = updated.StopCount;
                existing.WaypointsJson = updated.WaypointsJson;
                existing.Distance = updated.Distance;
                existing.EstimatedDuration = updated.EstimatedDuration;
                existing.Path = updated.Path;
                existing.AMBeginTime = updated.AMBeginTime;
                existing.PMBeginTime = updated.PMBeginTime;
                existing.Session = updated.Session;
                OnPropertyChanged(nameof(SelectedRoute));
                SyncAssignmentFromSelectedRoute();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed refreshing route after assignment");
            }
        }
        private async Task ExportCsvAsync()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportRoutesCsv"))
                {
                    var fileName = $"BusBuddy_Routes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    var path = ExportFilePrompt.TryGetPath(fileName, "CSV files (*.csv)|*.csv|All files (*.*)|*.*");
                    if (path is null)
                    {
                        StatusMessage = "Export cancelled";
                        return;
                    }

                    var visible = VisibleRoutes();
                    if (_exportService is not null)
                    {
                        await _exportService.ExportRoutesToCsvAsync(path, visible.Select(r => r.RouteId).ToList())
                            .ConfigureAwait(true);
                        RouteManagementExportHelper.RevealOrOpen(path);
                        StatusMessage = $"Exported CSV: {visible.Count} visible route(s) ({Path.GetFileName(path)})";
                        return;
                    }

                    RouteManagementExportHelper.WriteFallbackCsv(visible, path);
                    RouteManagementExportHelper.RevealOrOpen(path);
                    StatusMessage = $"Exported {visible.Count} visible route(s)";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed exporting routes CSV");
                StatusMessage = $"Error exporting routes: {ex.Message}";
            }
        }

        private async Task ExportReportAsync()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportRouteSummary"))
                {
                    var fileName = $"BusBuddy_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
                    var path = ExportFilePrompt.TryGetPath(fileName, "Text files (*.txt)|*.txt|All files (*.*)|*.*");
                    if (path is null)
                    {
                        StatusMessage = "Export cancelled";
                        return;
                    }

                    var visible = VisibleRoutes();
                    if (_exportService is not null)
                    {
                        await _exportService.GenerateRouteReportAsync(path, visible.Select(r => r.RouteId).ToList())
                            .ConfigureAwait(true);
                        RouteManagementExportHelper.RevealOrOpen(path);
                        StatusMessage = $"Exported report: {visible.Count} visible route(s) ({Path.GetFileName(path)})";
                        return;
                    }

                    RouteManagementExportHelper.WriteFallbackReport(visible, path);
                    RouteManagementExportHelper.RevealOrOpen(path);
                    StatusMessage = "Exported route summary";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed exporting route summary");
                StatusMessage = $"Error exporting report: {ex.Message}";
            }
        }

        private async Task PrintScheduleAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route first";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = $"Printing schedule for '{SelectedRoute.RouteName}'...";
                var pdf = await RouteManagementExportHelper
                    .WriteSchedulePdfAsync(SelectedRoute, _routeService, openAfter: false)
                    .ConfigureAwait(true);
                var preview = new PdfPreviewWindow(
                    pdf.Pdf,
                    RouteSummarySheetBuilder.DisplayNameFor(SelectedRoute) + " schedule");
                DialogOwner.Assign(preview);
                preview.Show();
                Logger.Information(
                    "Route schedule preview DisplayName={DisplayName} Stops={Stops} Size={SizeBytes} bytes Grid=PdfGrid Preview=true Verb=none File={File}",
                    RouteSummarySheetBuilder.DisplayNameFor(SelectedRoute),
                    pdf.StopCount,
                    pdf.Pdf.Length,
                    pdf.Path);
                StatusMessage = $"Schedule preview: {pdf.StopCount} stops, {pdf.StudentCount} students ({Path.GetFileName(pdf.Path)})";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed printing schedule");
                StatusMessage = $"Error printing schedule: {ex.Message}";
                MessageBox.Show(
                    $"Could not build the published stop sheet:\n{ex.Message}",
                    "Print Schedule",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}
