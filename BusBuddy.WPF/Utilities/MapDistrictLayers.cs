using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Utilities;

internal readonly record struct MapLayerSeedCounts(int Schools, int Pickups, int Students, int Depots);

internal readonly record struct MapDistrictLoad(
    MapLayerSeedCounts Counts,
    IReadOnlyList<string> NeedsValidation,
    Exception? Failure = null);

/// <summary>
/// Plots a <see cref="DistrictMapSnapshot"/> onto the district map.
/// Pickup-vs-home policy lives in <see cref="StudentPlotLocation"/>, applied by <see cref="GeoDataService"/>.
/// </summary>
internal sealed class MapDistrictLayers
{
    private static readonly ILogger Logger = Log.ForContext<MapDistrictLayers>();

    private readonly IGeoDataService _geo;
    private readonly IServiceScopeFactory? _scopes;
    private readonly MapPinPlot _plot;
    private readonly Func<(double Lat, double Lon, string Name)?>? _depot;

    public MapDistrictLayers(
        IGeoDataService geo,
        IServiceScopeFactory? scopes,
        MapPinPlot plot,
        Func<(double Lat, double Lon, string Name)?>? depot = null)
    {
        _geo = geo ?? throw new ArgumentNullException(nameof(geo));
        _scopes = scopes;
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
        _depot = depot;
    }

    /// <summary>
    /// Default district overlay: depot → schools → active pickups (no student homes until a route is selected).
    /// Schools, catalog stops, and the needs-validation list come from one query.
    /// </summary>
    public async Task<MapDistrictLoad> LoadDistrictBaseLayersAsync()
    {
        var depotCount = PlotDepot();
        DistrictMapSnapshot snapshot;
        try
        {
            snapshot = await QueryAsync(null).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "District base layers skipped");
            return new MapDistrictLoad(new MapLayerSeedCounts(0, 0, 0, depotCount), [], ex);
        }

        var schoolCount = PlotPlaces(snapshot.Schools, MapMarkerLabels.ForSchool);
        var pickupCount = PlotPlaces(snapshot.CatalogStops, MapMarkerLabels.ForPickup);
        Logger.Information(
            "District base layers loaded Depots={Depots} Schools={Schools} Pickups={Pickups}",
            depotCount,
            schoolCount,
            pickupCount);
        return new MapDistrictLoad(
            new MapLayerSeedCounts(schoolCount, pickupCount, 0, depotCount),
            snapshot.NeedsValidation);
    }

    /// <summary>Homes for riders on <paramref name="route"/>, from one district-map query.</summary>
    public async Task<int> PlotAssignedStudentsForRouteAsync(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var snapshot = await QueryAsync(route.RouteId).ConfigureAwait(true);
        return PlotHomes(snapshot.SelectedRoute?.Homes ?? []);
    }

    public int PlotHomes(IReadOnlyList<DistrictMapHome> homes)
    {
        var plotted = 0;
        foreach (var home in homes)
        {
            if (home.Pins.Count == 0)
            {
                continue;
            }

            MapStudentPlot.Draw(_plot, home.StudentName, home.Pins, home.StudentId);
            plotted++;
        }

        if (plotted == 0)
        {
            Logger.Information("No assigned students to plot");
        }

        return plotted;
    }

    /// <summary>Bus barn pin from <see cref="DistrictDepot"/> / Settings.</summary>
    public int PlotDepotPins() => PlotDepot();

    public async Task<int> PlotSchoolsAsync()
    {
        var snapshot = await QueryAsync(null).ConfigureAwait(true);
        return PlotPlaces(snapshot.Schools, MapMarkerLabels.ForSchool);
    }

    public async Task<int> PlotPickupsAsync()
    {
        var snapshot = await QueryAsync(null).ConfigureAwait(true);
        return PlotPlaces(snapshot.CatalogStops, MapMarkerLabels.ForPickup);
    }

    /// <summary>Active catalog stops are on the district snapshot. Published route stops stay here.</summary>
    public async Task<int> PlotRouteStopsAsync(IReadOnlyCollection<int> routeIds)
    {
        if (routeIds.Count == 0)
        {
            return 0;
        }

        using var scope = _scopes?.CreateScope();
        var routes = Resolve<IRouteService>(null, scope);
        if (routes is null)
        {
            Logger.Information("PlotRouteStopsAsync skipped — IRouteService not registered");
            return 0;
        }

        var plotted = 0;
        foreach (var routeId in routeIds)
        {
            IEnumerable<RouteStop> stops;
            try
            {
                var result = await routes.GetRouteStopsAsync(routeId).ConfigureAwait(true);
                if (!result.IsSuccess)
                {
                    Logger.Warning("Route stops unavailable RouteId={RouteId} Error={Error}", routeId, result.Error);
                    continue;
                }

                var route = await routes.GetRouteByIdAsync(routeId).ConfigureAwait(true);
                IReadOnlyList<Student> students = Array.Empty<Student>();
                if (route is { IsSuccess: true, Value: not null })
                {
                    var slot = RouteSession.ToAssignmentSlot(route.Value);
                    var roster = await routes.GetStudentsForRouteAsync(routeId, slot).ConfigureAwait(true);
                    if (roster.IsSuccess && roster.Value is not null)
                    {
                        students = roster.Value;
                    }
                }

                stops = AssignedRouteStops.ForRouting(result.Value, students);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "GetRouteStopsAsync failed RouteId={RouteId}", routeId);
                continue;
            }

            foreach (var stop in stops.Where(s => s.HasValidatedCoordinates).OrderBy(s => s.StopOrder))
            {
                var name = string.IsNullOrWhiteSpace(stop.StopName) ? $"Stop {stop.StopOrder}" : stop.StopName;
                _plot((double)stop.Latitude!.Value, (double)stop.Longitude!.Value, null, MapMarkerLabels.ForRouteStop(name), null);
                plotted++;
            }
        }

        return plotted;
    }

    private Task<DistrictMapSnapshot> QueryAsync(int? routeId) =>
        _geo.GetDistrictMapAsync(routeId);

    private int PlotPlaces(IReadOnlyList<DistrictMapPlace> places, Func<string, string> label)
    {
        var plotted = 0;
        foreach (var place in places)
        {
            if (!LocationCoordinate.IsValidated(place.Latitude, place.Longitude))
            {
                continue;
            }

            _plot(place.Latitude, place.Longitude, null, label(place.Name), null);
            plotted++;
        }

        return plotted;
    }

    private int PlotDepot()
    {
        if (_depot?.Invoke() is not { } depot)
        {
            return 0;
        }

        _plot(depot.Lat, depot.Lon, null, MapMarkerLabels.ForDepot(depot.Name), null);
        return 1;
    }

    private static T? Resolve<T>(T? injected, IServiceScope? scope) where T : class =>
        injected
        ?? scope?.ServiceProvider.GetService<T>()
        ?? App.ServiceProvider?.GetService<T>();
}
