using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Refreshes <see cref="Route.WaypointsJson"/> from Google Routes API <c>computeRoutes</c>.
/// Fail-open: returns false without wiping existing waypoints on error (FR-009).
/// Uses stored <em>stops</em> as origin/waypoints — never decoded road vertices.
/// </summary>
public static class RouteDrivePathRefresher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteDrivePathRefresher));

    /// <summary>Routes API intermediate waypoint cap (origin and destination are separate).</summary>
    public const int MaxIntermediateWaypoints = 25;

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

        var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
        var stops = payload.Stops;
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
                var intermediates = CapIntermediateWaypoints(
                    stops.Skip(1).Take(stops.Count - 2).ToList());
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
                    stops);

                Logger.Information(
                    "Drive path computed RouteId={RouteId} Stops={StopCount} Intermediates={Intermediates} DistanceMeters={DistanceMeters} Duration={Duration} ViaService={ViaService}",
                    route.RouteId,
                    stops.Count,
                    intermediates.Count,
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

    /// <summary>
    /// Evenly samples intermediates so a long stop list stays within the Routes API cap.
    /// </summary>
    public static IReadOnlyList<(double Latitude, double Longitude)> CapIntermediateWaypoints(
        IReadOnlyList<(double Latitude, double Longitude)> intermediates,
        int max = MaxIntermediateWaypoints)
    {
        if (intermediates is null || intermediates.Count == 0 || max < 1)
        {
            return Array.Empty<(double, double)>();
        }

        if (intermediates.Count <= max)
        {
            return intermediates;
        }

        if (max == 1)
        {
            return new[] { intermediates[0] };
        }

        var sampled = new (double Latitude, double Longitude)[max];
        var last = intermediates.Count - 1;
        for (var i = 0; i < max; i++)
        {
            var idx = (int)Math.Round(i * (double)last / (max - 1));
            sampled[i] = intermediates[idx];
        }

        return sampled;
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
