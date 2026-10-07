using System.Globalization;
using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Route;

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
                await LoadRouteStopsAsync();
                var begin = result.BeginTime?.ToString(@"hh\:mm", CultureInfo.InvariantCulture) ?? string.Empty;
                var estimate = result.Estimated ? " Straight-line estimate." : string.Empty;
                StatusMessage = $"Timing updated. Barn departure {begin}.{estimate}";
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
            await LoadRouteStopsAsync();
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
                PrintSelectedRouteSheetPreview,
                ConfirmOverwritePublishedClocks,
                notRidingIds)
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

    private void PrintSelectedRouteSheetPreview() =>
        SaveRouteSheet(includeMap: false);
}
