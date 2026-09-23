using System.Windows;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
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

    /// <summary>
    /// One load of published stops plus the assigned-rider filter. Draw and Optimize Order both use this.
    /// </summary>
    public async Task<RouteStopSets> LoadStopsAsync(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        using var scope = _scopes?.CreateScope();
        var routes = scope?.ServiceProvider.GetService<IRouteService>();
        if (routes is null)
        {
            return RouteStopSets.Empty;
        }

        var result = await routes.GetRouteStopsAsync(route.RouteId).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return RouteStopSets.Empty;
        }

        var all = result.Value.ToList();
        var slot = RouteSession.ToAssignmentSlot(route);
        var roster = await routes.GetStudentsForRouteAsync(route.RouteId, slot).ConfigureAwait(false);
        var students = roster.IsSuccess && roster.Value is not null
            ? roster.Value
            : new List<Student>();
        return new RouteStopSets(all, AssignedRouteStops.ForRouting(all, students));
    }

    /// <summary>
    /// Prepare the selected route's line and stops before the view-model touches the map.
    /// Published stops are loaded once, then again only if a rebuild wrote new stops.
    /// </summary>
    public async Task<MapRouteDraw> DrawAsync(
        Route? route,
        bool refreshDrivePath,
        int generation,
        IGeoDataService? geoData,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrent(generation))
        {
            return MapRouteDraw.Stale();
        }

        var routeName = route?.RouteName ?? "Unknown";
        IReadOnlyList<DistrictMapStop> published = [];
        IReadOnlyList<DistrictMapHome> homes = [];
        if (route is not null)
        {
            await OmitUnlistedSchoolsAsync(route, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent(generation))
            {
                return MapRouteDraw.Stale();
            }

            if (geoData is not null)
            {
                var snapshot = await geoData.GetDistrictMapAsync(route.RouteId, cancellationToken).ConfigureAwait(false);
                var selected = snapshot.SelectedRoute;
                if (selected is not null)
                {
                    published = selected.PublishedStops;
                    homes = selected.Homes;
                    if (string.IsNullOrWhiteSpace(route.WaypointsJson)
                        && !string.IsNullOrWhiteSpace(selected.WaypointsJson))
                    {
                        route.WaypointsJson = selected.WaypointsJson;
                    }
                }
            }

            if (!IsCurrent(generation))
            {
                return MapRouteDraw.Stale();
            }

            var rebuilt = await EnsureWaypointsAsync(route, published, geoData, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent(generation))
            {
                return MapRouteDraw.Stale();
            }

            if (rebuilt && geoData is not null)
            {
                var again = await geoData.GetDistrictMapAsync(route.RouteId, cancellationToken).ConfigureAwait(false);
                if (again.SelectedRoute is not null)
                {
                    published = again.SelectedRoute.PublishedStops;
                    homes = again.SelectedRoute.Homes;
                    if (!string.IsNullOrWhiteSpace(again.SelectedRoute.WaypointsJson))
                    {
                        route.WaypointsJson = again.SelectedRoute.WaypointsJson;
                    }
                }

                if (!IsCurrent(generation))
                {
                    return MapRouteDraw.Stale();
                }
            }
        }

        var validated = published.Where(s => LocationCoordinate.IsValidated(s.Latitude, s.Longitude)).ToList();
        var persist = default(MapRouteTrailPersist);
        if (refreshDrivePath && route is not null)
        {
            persist = await TryRefreshDrivePathAsync(route, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent(generation))
            {
                return MapRouteDraw.Stale();
            }
        }

        var plot = Build(route, validated.Count);
        var line = plot.Line.Where(p => LocationCoordinate.IsValidated(p.X, p.Y)).ToList();
        plot = Build(route, validated.Count, line.Count);

        if (!refreshDrivePath
            && route is not null
            && line.Count < 2
            && validated.Count >= 2
            && _routing is not null)
        {
            Logger.Information(
                "Auto-refreshing drive path — published stops={Published} renderable line={Line} RouteId={RouteId}",
                validated.Count,
                line.Count,
                route.RouteId);
            var refreshed = await TryRefreshDrivePathAsync(route, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent(generation))
            {
                return MapRouteDraw.Stale();
            }

            if (refreshed.Computed)
            {
                persist = refreshed;
                refreshDrivePath = true;
                plot = Build(route, validated.Count);
                line = plot.Line.Where(p => LocationCoordinate.IsValidated(p.X, p.Y)).ToList();
                plot = Build(route, validated.Count, line.Count);
            }
        }

        return new MapRouteDraw
        {
            Line = line,
            PublishedStops = validated,
            Homes = homes,
            Plot = plot,
            Persist = persist,
            RouteName = routeName,
            Refreshed = refreshDrivePath,
            HasRoute = route is not null
        };
    }

    private async Task OmitUnlistedSchoolsAsync(Route route, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes?.CreateScope();
            var rebuild = scope?.ServiceProvider.GetService<IRouteWaypointRebuildService>();
            if (rebuild is null)
            {
                return;
            }

            var cleanup = await rebuild.OmitUnlistedSchoolsAsync(route.RouteId, cancellationToken)
                .ConfigureAwait(false);
            if (!cleanup.Changed)
            {
                return;
            }

            route.WaypointsJson = cleanup.WaypointsJson;
            route.School = cleanup.School;
            Logger.Information(
                "RouteId={RouteId} dropped a school that is not an active destination",
                route.RouteId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Omit unlisted schools failed RouteId={RouteId}", route.RouteId);
        }
    }

    private async Task<bool> EnsureWaypointsAsync(
        Route route,
        IReadOnlyList<DistrictMapStop> published,
        IGeoDataService? geoData,
        CancellationToken cancellationToken)
    {
        var validatedCount = published.Count(s => LocationCoordinate.IsValidated(s.Latitude, s.Longitude));
        if (validatedCount >= 2)
        {
            var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
            var jsonStopCount = payload.Stops.Count > 0
                ? payload.Stops.Count
                : payload.MarkerStops.Count;
            if (string.IsNullOrWhiteSpace(route.WaypointsJson) || jsonStopCount != validatedCount)
            {
                if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    Logger.Information(
                        "WaypointsJson stop count {JsonStops} != published {PublishedStops} RouteId={RouteId} — rebuilding from published stops",
                        jsonStopCount,
                        validatedCount,
                        route.RouteId);
                }

                await RebuildAsync(route, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        Logger.Information(
            "RouteId={RouteId} has no published stops — rebuilding the trail from assigned homes",
            route.RouteId);
        await RebuildAsync(route, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
        {
            return true;
        }

        try
        {
            var loaded = geoData is null
                ? null
                : await geoData.GetRouteGeoDataAsync(route.RouteId).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(loaded?.WaypointsJson))
            {
                route.WaypointsJson = loaded.WaypointsJson;
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Ensure waypoints via GeoDataService failed RouteId={RouteId}", route.RouteId);
        }

        await RebuildAsync(route, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task RebuildAsync(Route route, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes?.CreateScope();
            var rebuild = scope?.ServiceProvider.GetService<IRouteWaypointRebuildService>();
            if (rebuild is null)
            {
                return;
            }

            var json = await rebuild.RebuildAndPersistAsync(route.RouteId, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                route.WaypointsJson = json;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Ensure waypoints via rebuild failed RouteId={RouteId}", route.RouteId);
        }
    }

    private async Task<MapRouteTrailPersist> TryRefreshDrivePathAsync(Route route, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(route.WaypointsJson))
        {
            return default;
        }

        var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
        if (payload.Stops.Count < 2 && payload.Points.Count < 2)
        {
            return default;
        }

        return await RefreshStoredPathAsync(route, cancellationToken).ConfigureAwait(false);
    }
}

internal readonly record struct RouteStopSets(IReadOnlyList<RouteStop> All, IReadOnlyList<RouteStop> Routable)
{
    public static RouteStopSets Empty { get; } = new(Array.Empty<RouteStop>(), Array.Empty<RouteStop>());
}

internal sealed class MapRouteDraw
{
    public static MapRouteDraw Stale() => new() { IsStale = true };

    public bool IsStale { get; init; }

    public bool HasRoute { get; init; }

    public string RouteName { get; init; } = "Unknown";

    public bool Refreshed { get; init; }

    public IReadOnlyList<Point> Line { get; init; } = Array.Empty<Point>();

    public IReadOnlyList<DistrictMapStop> PublishedStops { get; init; } = [];

    public IReadOnlyList<DistrictMapHome> Homes { get; init; } = [];

    public MapRouteTrailPlot Plot { get; init; }

    public MapRouteTrailPersist Persist { get; init; }
}
