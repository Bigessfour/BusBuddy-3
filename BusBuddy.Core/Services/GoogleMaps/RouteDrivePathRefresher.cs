using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Refreshes <see cref="Route.WaypointsJson"/> from Google Routes API <c>computeRoutes</c>.
/// Fail-open: returns false without wiping existing waypoints on error (FR-009).
/// </summary>
public static class RouteDrivePathRefresher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteDrivePathRefresher));

    public static async Task<DrivePathRefreshResult> TryRefreshAsync(
        IRoutingService? routingService,
        Route route,
        CancellationToken cancellationToken = default)
    {
        if (routingService is null)
        {
            return DrivePathRefreshResult.Skip("Routing service not registered.");
        }

        if (route is null)
        {
            return DrivePathRefreshResult.Skip("No route selected.");
        }

        var stops = RouteWaypointSerializer.Parse(route.WaypointsJson);
        if (stops.Count < 2)
        {
            return DrivePathRefreshResult.Skip("Need at least two geocoded stops for a drive path.");
        }

        using (LogContext.PushProperty("Operation", "RefreshDrivePath"))
        using (LogContext.PushProperty("RouteId", route.RouteId))
        {
            try
            {
                var origin = stops[0];
                var destination = stops[^1];
                var intermediates = stops.Skip(1).Take(stops.Count - 2).ToList();
                var path = await routingService
                    .ComputeDrivePathAsync(origin, destination, intermediates, cancellationToken)
                    .ConfigureAwait(false);

                if (!path.Succeeded || path.Points.Count == 0)
                {
                    Logger.Warning(
                        "Drive path refresh skipped for route {RouteId}: {Error}",
                        route.RouteId,
                        path.Error);
                    return DrivePathRefreshResult.Failed(path.Error ?? "Drive path computation failed.");
                }

                route.WaypointsJson = RouteWaypointSerializer.FromEncodedPolyline(
                    path.EncodedPolyline!,
                    path.Points);

                Logger.Information(
                    "Drive path computed RouteId={RouteId} Stops={StopCount} DistanceMeters={DistanceMeters} Duration={Duration} ViaService={ViaService}",
                    route.RouteId,
                    stops.Count,
                    path.DistanceMeters,
                    path.Duration,
                    true);

                return DrivePathRefreshResult.Succeeded(path);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Drive path refresh failed — keeping stored waypoints");
                return DrivePathRefreshResult.Failed(ex.Message);
            }
        }
    }
}

public sealed class DrivePathRefreshResult
{
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string? Message { get; init; }
    public DrivePathResult? Path { get; init; }

    public static DrivePathRefreshResult Succeeded(DrivePathResult path) =>
        new() { Success = true, Path = path, Message = "Drive path refreshed." };

    public static DrivePathRefreshResult Failed(string message) =>
        new() { Success = false, Message = message };

    public static DrivePathRefreshResult Skip(string message) =>
        new() { Skipped = true, Message = message };
}
