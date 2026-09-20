using System.Globalization;
using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
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
    /// Clerk Time Route: published clocks from StartTimeString plus drive-path travel
    /// (route EstimatedDuration), not a dwell-only staircase.
    /// </summary>
    private async Task TimeRouteStopsAsync()
    {
        if (SelectedRoute == null || !RouteStops.Any())
        {
            return;
        }

        if (!IsStartTimeValid)
        {
            StatusMessage = "Cannot time stops — invalid Start Time (HH:mm)";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Calculating stop times...";

            if (!TimeSpan.TryParseExact(
                    _startTimeString.Trim(),
                    new[] { @"hh\:mm", @"h\:mm" },
                    CultureInfo.InvariantCulture,
                    out var startOfRun))
            {
                startOfRun = new TimeSpan(7, 30, 0);
                _startTimeString = "07:30";
                OnPropertyChanged(nameof(StartTimeString));
            }

            var stamp = DateTime.UtcNow;
            var plan = PublishedStopClockPlanner.Apply(
                RouteStops,
                startOfRun,
                SelectedRoute.EstimatedDuration,
                stamp);

            Logger.Information(
                "Published clocks RouteId={RouteId} Stops={Stops} TravelMinutes={Travel} DwellMinutes={Dwell} Source={Source} First={First} Last={Last}",
                SelectedRoute.RouteId,
                plan.StopCount,
                plan.TravelMinutes,
                plan.DwellMinutes,
                plan.TravelSource,
                plan.FirstArrival,
                plan.LastArrival);

            var persistResult = await _routeService.UpdateRouteStopsTimingAsync(SelectedRoute.RouteId, RouteStops);
            if (!persistResult.IsSuccess)
            {
                StatusMessage = $"Timing calculated but failed to persist: {persistResult.Error}";
                MessageBox.Show(persistResult.Error ?? "Failed to persist timing", "Timing Persistence", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                var lastClock = plan.LastArrival.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
                StatusMessage =
                    $"Timing updated for {plan.StopCount} stops (Start {StartTimeString}, {plan.TravelMinutes} min travel via {plan.TravelSource}, last {lastClock})";
            }

            OnPropertyChanged(nameof(RouteStops));
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

    /// <summary>
    /// Structural stop edits keep existing clocks. Clerk Time Route / schedule Re-time publishes new ones.
    /// </summary>
    private void MarkPublishedClocksStale(string reason)
    {
        Logger.Information(
            "Published clocks left unchanged after {Reason} RouteId={RouteId}; Time Route to republish",
            reason,
            SelectedRoute?.RouteId);
        StatusMessage = $"{StatusMessage} Times unchanged — Time Route to republish.";
    }

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
        SaveRouteSheet(includeMap: false, preview: true);
}
