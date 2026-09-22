using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Utilities;

internal readonly record struct MapLayerSeedCounts(int Schools, int Pickups, int Students, int Depots);

/// <summary>
/// Schools, catalog pickups, and student pins on the district map.
/// Pickup-vs-home policy lives in <see cref="StudentPlotLocation"/>.
/// </summary>
internal sealed class MapDistrictLayers
{
    private static readonly ILogger Logger = Log.ForContext<MapDistrictLayers>();

    private readonly IPickupStopService? _pickups;
    private readonly IDestinationService? _destinations;
    private readonly IServiceScopeFactory? _scopes;
    private readonly MapPinPlot _plot;
    private readonly Func<(double Lat, double Lon, string Name)?>? _depot;

    public MapDistrictLayers(
        IPickupStopService? pickups,
        IDestinationService? destinations,
        IServiceScopeFactory? scopes,
        MapPinPlot plot,
        Func<(double Lat, double Lon, string Name)?>? depot = null)
    {
        _pickups = pickups;
        _destinations = destinations;
        _scopes = scopes;
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
        _depot = depot;
    }

    /// <summary>
    /// Default district overlay: depot → schools → active pickups (no student homes until a route is selected).
    /// </summary>
    public async Task<MapLayerSeedCounts> LoadDistrictBaseLayersAsync()
    {
        var depotCount = PlotDepot();
        var schoolCount = await TryPlotAsync(PlotSchoolsAsync, "schools").ConfigureAwait(true);
        var pickupCount = await TryPlotAsync(PlotPickupsAsync, "catalog stops").ConfigureAwait(true);
        Logger.Information(
            "District base layers loaded Depots={Depots} Schools={Schools} Pickups={Pickups}",
            depotCount,
            schoolCount,
            pickupCount);
        return new MapLayerSeedCounts(schoolCount, pickupCount, 0, depotCount);
    }

    /// <summary>Schools and catalog stops skipped because they have no validated coordinates.</summary>
    public async Task<IReadOnlyList<string>> ListNeedsValidationAsync()
    {
        using var scope = _scopes?.CreateScope();
        var lines = new List<string>();
        foreach (var school in await LoadSchoolsAsync(scope).ConfigureAwait(true))
        {
            if (!school.HasValidatedCoordinates)
            {
                lines.Add($"School: {DisplayPlaceName(school.Name)}");
            }
        }

        foreach (var stop in (await LoadPickupCatalogAsync(scope).ConfigureAwait(true)).Values)
        {
            if (!stop.HasValidatedCoordinates)
            {
                lines.Add($"Pickup: {DisplayPlaceName(stop.Name)}");
            }
        }

        return lines;
    }

    /// <summary>Homes and catalog pickups for riders assigned to <paramref name="route"/> (AM/PM slot).</summary>
    public async Task<int> PlotAssignedStudentsForRouteAsync(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        using var scope = _scopes?.CreateScope();
        var routeService = Resolve<IRouteService>(null, scope);
        if (routeService is null)
        {
            Logger.Warning("PlotAssignedStudentsForRouteAsync skipped — no route service");
            return 0;
        }

        var slot = RouteSession.ToAssignmentSlot(route);
        var result = await routeService.GetStudentsForRouteAsync(route.RouteId, slot).ConfigureAwait(true);
        if (!result.IsSuccess || result.Value is null || result.Value.Count == 0)
        {
            Logger.Information(
                "No assigned students to plot RouteId={RouteId} Slot={Slot}",
                route.RouteId,
                slot);
            return 0;
        }

        var pickups = await LoadPickupCatalogAsync(scope).ConfigureAwait(true);
        var plotted = PlotStoredStudents(result.Value, pickups);
        Logger.Information(
            "Plotted assigned students RouteId={RouteId} Slot={Slot} Roster={Roster} Pins={Pins}",
            route.RouteId,
            slot,
            result.Value.Count,
            plotted);
        return plotted;
    }

    /// <summary>Bus barn pin from <see cref="DistrictDepot"/> / Settings.</summary>
    public int PlotDepotPins() => PlotDepot();

    public async Task<int> PlotSchoolsAsync()
    {
        using var scope = _scopes?.CreateScope();
        return PlotSchools(await LoadSchoolsAsync(scope).ConfigureAwait(true));
    }

    public async Task<int> PlotPickupsAsync()
    {
        using var scope = _scopes?.CreateScope();
        return PlotPickups(await LoadPickupCatalogAsync(scope).ConfigureAwait(true));
    }

    /// <summary>Active catalog stops keyed by id — no plotting.</summary>
    public async Task<IReadOnlyDictionary<int, PickupStop>> LoadPickupIndexAsync()
    {
        using var scope = _scopes?.CreateScope();
        return await LoadPickupCatalogAsync(scope).ConfigureAwait(true);
    }

    /// <summary>
    /// Published boarding points on the given routes (<c>RouteStop</c> rows with validated GPS). Generated
    /// routes carry stops here rather than in the catalog. Plotted as route-stop (WP) pins: a stop that lands
    /// on an existing home / catalog pin tags that pin (gold ring) instead of stacking a duplicate caption.
    /// </summary>
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
                if (route.IsSuccess && route.Value is not null)
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

    private static string DisplayPlaceName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name.Trim();

    private int PlotSchools(IReadOnlyList<Destination> schools)
    {
        var plotted = 0;
        foreach (var school in schools.Where(s => s.HasValidatedCoordinates))
        {
            _plot((double)school.Latitude!, (double)school.Longitude!, null, MapMarkerLabels.ForSchool(school.Name), null);
            plotted++;
        }

        return plotted;
    }

    private int PlotPickups(IReadOnlyDictionary<int, PickupStop> catalog)
    {
        var plotted = 0;
        foreach (var stop in catalog.Values.Where(s => s.HasValidatedCoordinates))
        {
            _plot((double)stop.Latitude, (double)stop.Longitude, null, MapMarkerLabels.ForPickup(stop.Name), null);
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

    private int PlotStoredStudents(IReadOnlyList<Student> students, IReadOnlyDictionary<int, PickupStop> pickups)
    {
        var plotted = 0;
        foreach (var stu in students)
        {
            plotted += PlotStudentPins(stu, StudentPlotLocation.PinsFromStored(stu, pickups));
        }

        return plotted;
    }

    private int PlotStudentPins(Student student, IReadOnlyList<StudentPlotPoint> points)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        MapStudentPlot.Draw(_plot, student, points);
        return 1;
    }

    private async Task<IReadOnlyDictionary<int, PickupStop>> LoadPickupCatalogAsync(IServiceScope? scope)
    {
        var service = Resolve(_pickups, scope);
        if (service is null)
        {
            return StudentPlotLocation.Index(null);
        }

        return StudentPlotLocation.Index(await service.GetActiveStopsAsync().ConfigureAwait(true));
    }

    private async Task<IReadOnlyList<Destination>> LoadSchoolsAsync(IServiceScope? scope)
    {
        var dest = Resolve(_destinations, scope);
        if (dest is null)
        {
            return Array.Empty<Destination>();
        }

        return await dest.GetActiveSchoolsAsync().ConfigureAwait(true);
    }

    private async Task<int> TryPlotAsync(Func<Task<int>> plot, string layerName)
    {
        try
        {
            return await plot().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "District {Layer} layer skipped", layerName);
            return 0;
        }
    }

    private static T? Resolve<T>(T? injected, IServiceScope? scope) where T : class =>
        injected
        ?? scope?.ServiceProvider.GetService<T>()
        ?? App.ServiceProvider?.GetService<T>();
}
