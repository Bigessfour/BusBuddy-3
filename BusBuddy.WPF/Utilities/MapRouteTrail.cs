using System.Windows;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Utilities;

internal readonly record struct MapRouteTrailPlot(
    IReadOnlyList<Point> Line,
    IReadOnlyList<(double Latitude, double Longitude)> Markers,
    string StatusMessage);

internal readonly record struct MapRouteTrailPersist(
    bool Computed,
    bool Persisted,
    string? Message);

/// <summary>
/// Builds the district-map gold trail from stored route geometry.
/// Google refresh is opt-in; decoded road vertices are never treated as stops.
/// </summary>
internal sealed class MapRouteTrail
{
    public const string WaypointPrefix = "WP ";

    private static readonly ILogger Logger = Log.ForContext<MapRouteTrail>();
    private readonly IRoutingService? _routing;
    private readonly IServiceScopeFactory? _scopes;
    private int _generation;

    public MapRouteTrail(IRoutingService? routing, IServiceScopeFactory? scopes)
    {
        _routing = routing;
        _scopes = scopes;
    }

    public int BeginDraw() => Interlocked.Increment(ref _generation);

    public bool IsCurrent(int generation) => generation == Volatile.Read(ref _generation);

    public async Task<MapRouteTrailPersist> RefreshStoredPathAsync(Route route, CancellationToken cancellationToken = default)
    {
        var refresh = await RouteDrivePathRefresher.TryRefreshAsync(_routing, route, cancellationToken).ConfigureAwait(false);
        if (!refresh.Success)
        {
            return new MapRouteTrailPersist(false, false, refresh.Message);
        }

        try
        {
            using var scope = _scopes?.CreateScope();
            var routes = scope?.ServiceProvider.GetService<IRouteService>();
            if (routes is null)
            {
                return new MapRouteTrailPersist(
                    true,
                    false,
                    "Drive path shown — not saved (route service unavailable)");
            }

            var update = await routes.UpdateRouteAsync(route).ConfigureAwait(false);
            if (!update.IsSuccess)
            {
                Logger.Warning(
                    "Drive path computed but save failed RouteId={RouteId} Error={Error}",
                    route.RouteId,
                    update.Error);
                return new MapRouteTrailPersist(
                    true,
                    false,
                    "Drive path shown — save failed. Apply the WaypointsJson migration if the column is still limited.");
            }

            return new MapRouteTrailPersist(true, true, refresh.Message);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Drive path persist failed — map still shows computed trail");
            return new MapRouteTrailPersist(true, false, "Drive path shown — save failed");
        }
    }

    public static MapRouteTrailPlot Build(
        Route? route,
        int publishedValidatedStopCount = 0,
        int renderableLinePointCount = -1)
    {
        if (route is null)
        {
            return new MapRouteTrailPlot(
                Array.Empty<Point>(),
                Array.Empty<(double, double)>(),
                "Select a route to display");
        }

        var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
        var line = payload.Points
            .Where(p => LocationCoordinate.IsValidated(p.Latitude, p.Longitude))
            .Select(p => new Point(p.Latitude, p.Longitude))
            .ToArray();
        var markers = payload.MarkerStops
            .Where(p => LocationCoordinate.IsValidated(p.Latitude, p.Longitude))
            .ToArray();
        var stopCount = publishedValidatedStopCount > 0
            ? publishedValidatedStopCount
            : markers.Length;
        var drawable = renderableLinePointCount >= 0 ? renderableLinePointCount : line.Length;
        var name = route.RouteName ?? "Unknown";
        var status = drawable >= 2
            ? $"Route {name}: trail ({drawable} point(s)) and {stopCount} published stop(s)"
            : stopCount > 0
                ? $"Route {name}: {stopCount} published stop(s) — press Refresh for Google drive path"
                : $"Route {name} has no waypoints to display";
        return new MapRouteTrailPlot(line, markers, status);
    }

    public static string MarkerLabel(int index, int count)
    {
        if (index == 0)
        {
            return WaypointPrefix + "Start";
        }

        if (index == count - 1)
        {
            return WaypointPrefix + "End";
        }

        return $"{WaypointPrefix}Stop {index}";
    }
}
