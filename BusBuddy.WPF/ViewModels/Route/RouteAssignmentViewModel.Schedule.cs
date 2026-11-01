using System.Globalization;
using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.WPF;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Route;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Drive path, published clocks, and the route schedule window.
/// Rider / stop CRUD stays in <c>Commands</c>.
/// </summary>
public partial class RouteAssignmentViewModel
{
    /// <summary>
    /// Clerk-initiated Google Routes polyline from published <see cref="RouteStop"/> rows.
    /// </summary>
    private async Task RefreshAssignmentDrivePathAsync()
    {
        if (SelectedRoute == null || RouteStops.Count < 2 || IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Refreshing drive path for {GetRouteDisplayName(SelectedRoute)}...";
            var result = await _routeService.RefreshDrivePathAsync(SelectedRoute.RouteId);
            if (!result.IsSuccess || result.Value is null)
            {
                StatusMessage = result.Error ?? "Drive path refresh failed.";
                return;
            }

            var refresh = result.Value;
            if (refresh.Success)
            {
                StatusMessage =
                    $"Drive path updated ({refresh.Path?.DistanceMeters} m, {refresh.Path?.Duration})";
                await ApplyPublishedClocksAfterStopChangeAsync("drive path");
                await PlotRouteOnMapAsync();
                return;
            }

            StatusMessage = $"{GetRouteDisplayName(SelectedRoute)}: {refresh.Message}";
            if (refresh.Skipped)
            {
                MessageBox.Show(
                    $"{GetRouteDisplayName(SelectedRoute)} needs at least two validated stops.\n\n{refresh.Message}",
                    "Drive Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Assignment drive path failed RouteId={RouteId}", SelectedRoute.RouteId);
            StatusMessage = $"Error refreshing drive path: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            (RefreshDrivePathCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Clerk Time Route: one published-clock plan from school bells. The start box shows the barn departure.
    /// </summary>
    private async Task TimeRouteStopsAsync()
    {
        if (SelectedRoute == null || !RouteStops.Any())
        {
            return;
        }

        if (_routeDetermination is null)
        {
            StatusMessage = "Clock plan is not available.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Calculating stop times...";
            var result = await _routeDetermination.ApplyPublishedClocksAsync(SelectedRoute.RouteId);
            if (result.RoutesUpdated > 0)
            {
                ApplyBeginTime(result);
                _routeStopsLoad = LoadRouteStopsAsync();
                if (await _routeStopsLoad)
                {
                    var begin = result.BeginTime?.ToString(@"hh\:mm", CultureInfo.InvariantCulture) ?? string.Empty;
                    var estimate = result.Estimated ? " Straight-line estimate." : string.Empty;
                    StatusMessage = $"Timing updated. Barn departure {begin}.{estimate}";
                }
            }
            else
            {
                StatusMessage = result.Error ?? "Clock plan left published times unchanged.";
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to time route stops");
            StatusMessage = $"Error timing stops: {ex.Message}";
            MessageBox.Show($"Failed to time stops: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private async Task ApplyPublishedClocksAfterStopChangeAsync(string reason)
    {
        if (SelectedRoute is null || _routeDetermination is null)
        {
            return;
        }

        try
        {
            var result = await _routeDetermination.ApplyPublishedClocksAsync(SelectedRoute.RouteId);
            _routeStopsLoad = LoadRouteStopsAsync();
            if (!await _routeStopsLoad)
            {
                return;
            }

            if (result.RoutesUpdated > 0)
            {
                ApplyBeginTime(result);
                var estimate = result.Estimated ? " Straight-line estimate." : string.Empty;
                StatusMessage = $"{StatusMessage} Clocks updated.{estimate}";
            }
            else
            {
                var warning = result.Error ?? "published clocks were left unchanged";
                Logger.Information(
                    "Published clocks unchanged after {Reason} RouteId={RouteId} Warning={Warning}",
                    reason,
                    SelectedRoute.RouteId,
                    warning);
                StatusMessage = $"{StatusMessage} {warning}";
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Published clocks skipped after {Reason} RouteId={RouteId}", reason, SelectedRoute.RouteId);
        }
    }

    private void ApplyBeginTime(RouteGenerationResult result)
    {
        if (SelectedRoute is null || result.BeginTime is not TimeSpan begin)
        {
            return;
        }

        if (RouteSession.Canonical(SelectedRoute.Session) == RouteSession.PM)
        {
            SelectedRoute.PMBeginTime = begin;
        }
        else
        {
            SelectedRoute.AMBeginTime = begin;
        }

        _startTimeString = begin.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(StartTimeString));
    }

    /// <summary>
    /// Opens the published stop timetable for the selected route row.
    /// Not the district <c>DriverScheduleView</c> / SfScheduler calendar.
    /// </summary>
    private async Task ViewScheduleAsync()
    {
        if (SelectedRoute == null)
        {
            MessageBox.Show("Select a route first.", "Route required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsLoading)
        {
            return;
        }

        var notRidingIds = await LoadNotRidingStudentIdsAsync();
        await RefreshDatedSchedulesAsync();

        ResolveSelectedSlotBusAndDriver(out var bus, out var driver);
        if (!RouteScheduleViewModel.TryCreate(
                SelectedRoute,
                RouteStops,
                AssignedStudentsForSelectedRoute,
                bus,
                driver,
                NormalizeTimeSlot(SelectedTimeSlot),
                out var scheduleVm,
                ReTimeSelectedRouteSheetAsync,
                PrintSelectedRouteSheetPreviewAsync,
                ConfirmOverwritePublishedClocks,
                notRidingIds,
                SaveDatedSubstituteAsync)
            || scheduleVm is null)
        {
            return;
        }

        var window = new RouteScheduleWindow(scheduleVm);
        DialogOwner.Assign(window);
        window.Show();
        var clocks = string.Join(
            " ",
            scheduleVm.Sheet.Stops.Select(s => s.Arrival));
        Logger.Information(
            "Opened route schedule DisplayName={DisplayName} Stops={Stops} FirstLast={FirstLast} Clocks={Clocks}",
            scheduleVm.Sheet.DisplayName,
            scheduleVm.Sheet.Stops.Count,
            scheduleVm.FirstLastClockText,
            clocks);
        StatusMessage = RouteStops.Count == 0
            ? RouteScheduleViewModel.EmptyStopsHint
            : $"Schedule: {scheduleVm.Sheet.DisplayName} {scheduleVm.FirstLastClockText}";
    }

    private bool ConfirmOverwritePublishedClocks() =>
        MessageBox.Show(
            "Overwrite published times?",
            "Re-time route",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

    private async Task<RouteSummarySheet?> ReTimeSelectedRouteSheetAsync()
    {
        await TimeRouteStopsAsync();
        if (SelectedRoute == null)
        {
            return null;
        }

        await RefreshDatedSchedulesAsync();
        return BuildSelectedRouteSheet(await LoadNotRidingStudentIdsAsync());
    }

    /// <summary>
    /// Published route calendar day (UTC date). Same key as rider exceptions so Schedule
    /// badges match Mark not riding on a dated row.
    /// </summary>
    private DateTime PublishedSessionDateUtc =>
        DateTime.SpecifyKind(
            (SelectedRoute is null || SelectedRoute.Date == default
                ? DateTime.UtcNow
                : SelectedRoute.Date).Date,
            DateTimeKind.Utc);

    private async Task<IReadOnlySet<int>?> LoadNotRidingStudentIdsAsync()
    {
        if (SelectedRoute == null)
        {
            return null;
        }

        var exceptions = await _routeService.GetRiderExceptionStudentIdsAsync(
            SelectedRoute.RouteId,
            PublishedSessionDateUtc);
        return exceptions.IsSuccess && exceptions.Value is { Count: > 0 }
            ? exceptions.Value.ToHashSet()
            : null;
    }

    private Task PrintSelectedRouteSheetPreviewAsync() =>
        SaveRouteSheetAsync(includeMap: false);

    /// <summary>
    /// Dated spare bus / substitute driver for the selected route's service date only.
    /// Writes <see cref="Schedule"/>; never calls Assign* on year-default Route FKs.
    /// </summary>
    private async Task<RouteSummarySheet?> SaveDatedSubstituteAsync()
    {
        if (SelectedRoute is null)
        {
            return null;
        }

        await RefreshDatedSchedulesAsync();
        ResolveSelectedSlotBusAndDriver(out var currentBus, out var currentDriver);

        var dialogVm = new RouteDatedSubstituteViewModel(
            GetRouteDisplayName(SelectedRoute),
            RouteDailyPairing.ServiceDateUtc(SelectedRoute),
            AvailableBuses,
            AvailableDrivers,
            currentBus,
            currentDriver);
        var dialog = new RouteDatedSubstituteDialog(dialogVm);
        DialogOwner.Assign(dialog);
        var accepted = dialog.ShowDialog() == true;
        if (!accepted || dialogVm.SelectedBus is null || dialogVm.SelectedDriver is null)
        {
            return null;
        }

        var bus = dialogVm.SelectedBus;
        var driver = dialogVm.SelectedDriver;
        if (DriverStudentRouteEligibility.TryGetAssignmentBlockReason(driver, out var reason))
        {
            MessageBox.Show(
                $"Cannot assign substitute driver {driver.DriverName}: {reason}",
                "Substitute blocked",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        var slot = NormalizeTimeSlot(SelectedTimeSlot);
        var row = RouteDailyPairing.BuildSessionRow(SelectedRoute, slot, bus.BusId, driver.DriverId);
        var tripService = App.ServiceProvider?.GetService<ITripEventService>();
        if (tripService is null)
        {
            if (MessageBox.Show(
                    "Trip overlap check is unavailable. Save substitute anyway? Home-route year defaults stay unchanged.",
                    "Substitute",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return null;
            }
        }
        else
        {
            var warnings = await tripService
                .GetAssignmentWarningsAsync(bus.BusId, driver.DriverId, row.DepartureTime, row.ArrivalTime)
                .ConfigureAwait(true);
            if (warnings.Count > 0)
            {
                var text = string.Join(Environment.NewLine, warnings)
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Home-route year defaults stay unchanged. Save substitute anyway?";
                if (MessageBox.Show(
                        text,
                        "Trip overlap warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return null;
                }
            }
        }

        var schedules = App.ServiceProvider?.GetService<IScheduleService>();
        if (schedules is null)
        {
            MessageBox.Show(
                "Schedule service is not available.",
                "Substitute",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        await schedules.SaveRouteSessionScheduleAsync(row).ConfigureAwait(true);
        Logger.Information(
            "Saved dated substitute RouteId={RouteId} Date={Date} BusId={BusId} DriverId={DriverId} RouteAmBus={AmBus} RouteAmDriver={AmDriver}",
            SelectedRoute.RouteId,
            row.ScheduleDate,
            bus.BusId,
            driver.DriverId,
            SelectedRoute.AMVehicleId,
            SelectedRoute.AMDriverId);

        await RefreshDatedSchedulesAsync();
        return BuildSelectedRouteSheet(await LoadNotRidingStudentIdsAsync());
    }
}
