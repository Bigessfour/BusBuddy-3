using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
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
                var requested = await RequestDrivePathAsync(routingService, stops, cancellationToken)
                    .ConfigureAwait(false);
                var path = requested.Path;
                var intermediates = requested.Intermediates;
                var droppedIntermediates = requested.DroppedIntermediates;

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
                    stops,
                    path.Steps);
                ApplyPathMetrics(route, path);

                Logger.Information(
                    "Drive path computed RouteId={RouteId} Stops={StopCount} Intermediates={Intermediates} DroppedIntermediates={Dropped} DistanceMeters={DistanceMeters} Duration={Duration} ViaService={ViaService}",
                    route.RouteId,
                    stops.Count,
                    intermediates.Count,
                    droppedIntermediates,
                    path.DistanceMeters,
                    path.Duration,
                    true);

                return DrivePathRefreshResult.Succeeded(path, droppedIntermediates);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Drive path refresh failed — keeping stored waypoints");
                return DrivePathRefreshResult.Failed(ex.Message);
            }
        }
    }

    /// <summary>
    /// Road instructions for the published stop list. Does not change <see cref="Route.WaypointsJson"/>.
    /// A path that has an encoded line but no stored stops is left alone — decoded vertices are not waypoints.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TryReadStepsAsync(
        IRoutingService? routingService,
        Route? route,
        CancellationToken cancellationToken = default)
    {
        if (route is null)
        {
            return Array.Empty<string>();
        }

        var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
        if (payload.Directions.Count > 0)
        {
            return payload.Directions;
        }

        if (routingService is null || payload.Stops.Count < 2)
        {
            return Array.Empty<string>();
        }

        try
        {
            var requested = await RequestDrivePathAsync(routingService, payload.Stops, cancellationToken)
                .ConfigureAwait(false);
            return requested.Path.Succeeded ? requested.Path.Steps : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Road directions unavailable RouteId={RouteId}", route.RouteId);
            return Array.Empty<string>();
        }
    }

    private static async Task<RequestedDrivePath> RequestDrivePathAsync(
        IRoutingService routingService,
        IReadOnlyList<(double Latitude, double Longitude)> stops,
        CancellationToken cancellationToken)
    {
        var origin = stops[0];
        var destination = stops[^1];
        var allIntermediates = stops.Skip(1).Take(Math.Max(0, stops.Count - 2)).ToList();
        var intermediates = CapIntermediateWaypoints(allIntermediates);
        var path = await routingService
            .ComputeDrivePathAsync(origin, destination, intermediates, cancellationToken)
            .ConfigureAwait(false);
        return new RequestedDrivePath(
            path,
            intermediates,
            Math.Max(0, allIntermediates.Count - intermediates.Count));
    }

    private readonly record struct RequestedDrivePath(
        DrivePathResult Path,
        IReadOnlyList<(double Latitude, double Longitude)> Intermediates,
        int DroppedIntermediates);

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

    /// <summary>Miles, minutes, and the grid Path caption from a Routes API result.</summary>
    public static void ApplyPathMetrics(Route route, DrivePathResult path)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(path);

        if (path.DistanceMeters is int meters && meters > 0)
        {
            route.Distance = decimal.Round(meters / 1609.344m, 2);
        }

        if (!string.IsNullOrWhiteSpace(path.Duration)
            && path.Duration.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(path.Duration.TrimEnd('s', 'S'), out var seconds))
        {
            route.EstimatedDuration = Math.Max(1, (int)Math.Round(seconds / 60.0));
        }

        if (route.Distance is decimal miles)
        {
            route.Path = string.IsNullOrWhiteSpace(path.Duration)
                ? $"{miles:0.0} mi"
                : $"{miles:0.0} mi · {path.Duration}";
        }
    }
}

public sealed class DrivePathRefreshResult
{
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string? Message { get; init; }
    public DrivePathResult? Path { get; init; }

    /// <summary>Intermediate stops left out of the Routes API call because of <see cref="RouteDrivePathRefresher.MaxIntermediateWaypoints"/>.</summary>
    public int DroppedIntermediateCount { get; init; }

    public static DrivePathRefreshResult Succeeded(DrivePathResult path, int droppedIntermediates = 0)
    {
        var message = droppedIntermediates > 0
            ? $"Drive path refreshed. {droppedIntermediates} intermediate stop(s) omitted (Google Routes allows {RouteDrivePathRefresher.MaxIntermediateWaypoints} intermediates)."
            : "Drive path refreshed.";
        return new DrivePathRefreshResult
        {
            Success = true,
            Path = path,
            Message = message,
            DroppedIntermediateCount = droppedIntermediates
        };
    }

    public static DrivePathRefreshResult Failed(string message) =>
        new() { Success = false, Message = message };

    public static DrivePathRefreshResult Skip(string message) =>
        new() { Skipped = true, Message = message };
}
