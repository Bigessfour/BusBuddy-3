using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Logging;

namespace BusBuddy.WPF.ViewModels.Route
{
    public partial class RouteManagementViewModel
    {
        private async Task RefreshDrivePathAsync()
        {
            if (SelectedRoute is null || IsBusy)
            {
                return;
            }

            var routeName = SelectedRoute.RouteName;
            var routeId = SelectedRoute.RouteId;
            try
            {
                IsBusy = true;
                StatusMessage = $"Refreshing drive path for '{routeName}'...";
                var result = await _routeService.RefreshDrivePathAsync(routeId).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    var error = string.IsNullOrWhiteSpace(result.Error)
                        ? "Drive path refresh failed."
                        : result.Error;
                    StatusMessage = error;
                    UiProofLog.Write(Logger, "Drive Path", "RouteManagementView", "failed", result.Error);
                    ShowClerkNotice(error, "Drive Path", MessageBoxImage.Warning);
                    return;
                }

                await LoadSingleRouteAsync(routeId).ConfigureAwait(true);
                var refresh = result.Value;
                if (refresh.Success)
                {
                    var pathCaption = SelectedRoute?.Path;
                    var meters = refresh.Path?.DistanceMeters;
                    var duration = refresh.Path?.Duration;
                    StatusMessage =
                        $"Drive path updated ({meters} m, {duration})";
                    UiProofLog.Write(
                        Logger,
                        "Drive Path",
                        "RouteManagementView",
                        "refreshed",
                        routeName);
                    ShowClerkNotice(
                        $"{routeName}\n\nRoad path saved ({pathCaption ?? $"{meters} m, {duration}"}).\n\n"
                        + "Open Manage Route to plot the line on the map. Use Time Route there to publish stop clocks.",
                        "Drive Path",
                        MessageBoxImage.Information);
                    return;
                }

                var skip = refresh.Message
                    ?? "Stop order saved. Google Routes is not configured, so the road polyline was skipped.";
                StatusMessage = $"{SelectedRoute.RouteName}: {skip}";
                var outcome = refresh.Skipped ? "skipped" : "hydrated";
                UiProofLog.Write(Logger, "Drive Path", "RouteManagementView", outcome, skip);
                if (refresh.Skipped)
                {
                    ShowClerkNotice(
                        $"{SelectedRoute.RouteName} has {SelectedRoute.StopCount ?? 0} geocoded stop(s).\n\n{skip}",
                        "Drive Path",
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, "Drive Path", "RouteManagementView");
                StatusMessage = $"Error refreshing drive path: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OptimizeStopOrderAsync()
        {
            if (SelectedRoute is null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                if (_routeOptimization is null)
                {
                    StatusMessage = "Route Optimization is not configured. Drive Path still uses Google Routes.";
                    UiProofLog.Write(Logger, "Optimize Order", "RouteManagementView", "unconfigured");
                    return;
                }

                var stopsResult = await _routeService.GetRouteStopsAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                if (!stopsResult.IsSuccess || stopsResult.Value is null)
                {
                    StatusMessage = stopsResult.Error ?? "Could not load stops.";
                    return;
                }

                var allStops = stopsResult.Value.ToList();
                var slot = RouteSession.ToAssignmentSlot(SelectedRoute);
                var roster = await _routeService.GetStudentsForRouteAsync(SelectedRoute.RouteId, slot)
                    .ConfigureAwait(true);
                var students = roster.IsSuccess && roster.Value is not null
                    ? roster.Value
                    : new List<BusBuddy.Core.Models.Student>();
                var routable = AssignedRouteStops.ForRouting(allStops, students).ToList();
                var ordered = await RouteStopOrderPlanner.ComputePinnedOrderAsync(
                    routable,
                    _routeOptimization,
                    SelectedRoute.MaxCapacity,
                    DateTime.UtcNow).ConfigureAwait(true);
                if (!ordered.IsSuccess || ordered.Value is null)
                {
                    StatusMessage = ordered.Error ?? "Route Optimization failed.";
                    return;
                }

                var reorder = await _routeService.ReorderRouteStopsAsync(
                    SelectedRoute.RouteId,
                    AssignedRouteStops.OrderPreservingUnroutable(allStops, ordered.Value.ToList()))
                    .ConfigureAwait(true);
                if (!reorder.IsSuccess)
                {
                    StatusMessage = reorder.Error ?? "Could not save stop order.";
                    return;
                }

                await LoadSingleRouteAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                StatusMessage =
                    "Stop order optimized (start/end pinned). Drive path refreshed. Regenerate the schedule if published times should follow the new sequence.";
                UiProofLog.Write(
                    Logger,
                    "Optimize Order",
                    "RouteManagementView",
                    "optimized",
                    $"RouteId={SelectedRoute.RouteId} Stops={ordered.Value.Count}");
                Logger.Information(
                    "Optimize stop order RouteId={RouteId} Stops={Count}",
                    SelectedRoute.RouteId,
                    ordered.Value.Count);
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, "Optimize Order", "RouteManagementView");
                StatusMessage = $"Error optimizing stop order: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Clerk dialog. Headless runs (CI, unit tests) have no WPF application, and
        /// <see cref="MessageBox.Show(string)"/> would block the test host until the job is cancelled.
        /// </summary>
        private static void ShowClerkNotice(string message, string title, MessageBoxImage image)
        {
            if (Application.Current is null)
            {
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, image);
        }

        private async Task CopyRouteAsync()
        {
            if (SelectedRoute is null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var sourceName = SelectedRoute.RouteName;
                Logger.Information("Copying route {RouteId}:{RouteName}", SelectedRoute.RouteId, sourceName);
                var result = await _routeService.CloneRouteAsync(
                    SelectedRoute.RouteId,
                    DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc),
                    $"Copy of {sourceName}");
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Copy failed: {result.Error}";
                    Logger.Warning("CloneRouteAsync failed for {RouteName}: {Error}", sourceName, result.Error);
                    return;
                }

                await LoadRoutesAsync();
                StatusMessage = $"Copied route '{sourceName}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to copy route");
                StatusMessage = $"Error copying route: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
