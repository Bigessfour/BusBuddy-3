using System.Windows;
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

            try
            {
                IsBusy = true;
                StatusMessage = $"Refreshing drive path for '{SelectedRoute.RouteName}'...";
                var result = await _routeService.RefreshDrivePathAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    StatusMessage = string.IsNullOrWhiteSpace(result.Error)
                        ? "Drive path refresh failed."
                        : result.Error;
                    UiProofLog.Write(Logger, "Drive Path", "RouteManagementView", "failed", result.Error);
                    return;
                }

                await LoadSingleRouteAsync(SelectedRoute.RouteId).ConfigureAwait(true);
                var refresh = result.Value;
                if (refresh.Success)
                {
                    StatusMessage =
                        $"Drive path updated ({refresh.Path?.DistanceMeters} m, {refresh.Path?.Duration})";
                    UiProofLog.Write(
                        Logger,
                        "Drive Path",
                        "RouteManagementView",
                        "refreshed",
                        SelectedRoute.RouteName);
                    return;
                }

                var skip = refresh.Message
                    ?? "Stop order saved. Google Routes is not configured, so the road polyline was skipped.";
                StatusMessage = $"{SelectedRoute.RouteName}: {skip}";
                var outcome = refresh.Skipped ? "skipped" : "hydrated";
                UiProofLog.Write(Logger, "Drive Path", "RouteManagementView", outcome, skip);
                if (refresh.Skipped)
                {
                    MessageBox.Show(
                        $"{SelectedRoute.RouteName} has {SelectedRoute.StopCount ?? 0} geocoded stop(s).\n\n{skip}",
                        "Drive Path",
                        MessageBoxButton.OK,
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

                var ordered = await RouteStopOrderPlanner.ComputePinnedOrderAsync(
                    stopsResult.Value.ToList(),
                    _routeOptimization,
                    SelectedRoute.MaxCapacity,
                    DateTime.UtcNow).ConfigureAwait(true);
                if (!ordered.IsSuccess || ordered.Value is null)
                {
                    StatusMessage = ordered.Error ?? "Route Optimization failed.";
                    return;
                }

                var reorder = await _routeService.ReorderRouteStopsAsync(SelectedRoute.RouteId, ordered.Value.ToList())
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
