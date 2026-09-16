using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Utilities;

internal readonly record struct MapLayerSeedCounts(int Schools, int Pickups, int Students, int Depots);

internal readonly record struct MapStudentBulkPlot(int Plotted, int Geocoded, int Total, string Status);

/// <summary>
/// Schools, catalog pickups, and student pins on the district map.
/// Pickup-vs-home policy lives in <see cref="StudentPlotLocation"/>.
/// </summary>
internal sealed class MapDistrictLayers
{
    private static readonly ILogger Logger = Log.ForContext<MapDistrictLayers>();

    private readonly IPickupStopService? _pickups;
    private readonly IDestinationService? _destinations;
    private readonly IStudentService? _students;
    private readonly IGeocodingService? _geocoding;
    private readonly IServiceScopeFactory? _scopes;
    private readonly MapPinPlot _plot;
    private readonly Func<(double Lat, double Lon, string Name)?>? _depot;

    public MapDistrictLayers(
        IPickupStopService? pickups,
        IDestinationService? destinations,
        IStudentService? students,
        IGeocodingService? geocoding,
        IServiceScopeFactory? scopes,
        MapPinPlot plot,
        Func<(double Lat, double Lon, string Name)?>? depot = null)
    {
        _pickups = pickups;
        _destinations = destinations;
        _students = students;
        _geocoding = geocoding;
        _scopes = scopes;
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
        _depot = depot;
    }

    /// <summary>
    /// District pins in fixed order: depot → schools → active pickups → students with stored coords.
    /// No geocoding / network on this path.
    /// </summary>
    public async Task<MapLayerSeedCounts> LoadDistrictLayersAsync()
    {
        var depotCount = PlotDepot();
        var schoolCount = await PlotSchoolsAsync().ConfigureAwait(true);
        var pickupCount = await PlotPickupsAsync().ConfigureAwait(true);
        var studentCount = await PlotStoredStudentsAsync().ConfigureAwait(true);
        Logger.Information(
            "District layers loaded Depots={Depots} Schools={Schools} Pickups={Pickups} Students={Students}",
            depotCount,
            schoolCount,
            pickupCount,
            studentCount);
        return new MapLayerSeedCounts(schoolCount, pickupCount, studentCount, depotCount);
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

                stops = result.Value;
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

    /// <summary>Students that already have pickup and/or home GPS — no geocode.</summary>
    public async Task<int> PlotStoredStudentsAsync()
    {
        using var scope = _scopes?.CreateScope();
        var pickups = await LoadPickupCatalogAsync(scope).ConfigureAwait(true);
        var students = await LoadStudentsAsync(scope).ConfigureAwait(true);
        return PlotStoredStudents(students, pickups);
    }

    public async Task<MapStudentBulkPlot> BulkPlotStudentsAsync()
    {
        using var scope = _scopes?.CreateScope();
        var studentService = Resolve(_students, scope);
        if (studentService is null)
        {
            return new MapStudentBulkPlot(0, 0, 0, "Student service unavailable");
        }

        List<Student> students;
        try
        {
            students = await studentService.GetAllStudentsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Bulk plot: failed loading students");
            return new MapStudentBulkPlot(0, 0, 0, "Load students failed");
        }

        if (students.Count == 0)
        {
            return new MapStudentBulkPlot(0, 0, 0, "No students");
        }

        var pickups = await LoadPickupCatalogAsync(scope).ConfigureAwait(true);
        var geocoded = 0;
        var plotted = 0;
        foreach (var stu in students)
        {
            var points = StudentPlotLocation.PinsFromStored(stu, pickups);
            if (points.Count == 0)
            {
                var geo = await TryGeocodeAsync(stu, scope).ConfigureAwait(true);
                if (geo is null)
                {
                    continue;
                }

                points = [new StudentPlotPoint(geo.Value.Lat, geo.Value.Lon, AtPickup: false, PickupName: null)];
                stu.Latitude = (decimal)geo.Value.Lat;
                stu.Longitude = (decimal)geo.Value.Lon;
                if (await studentService.UpdateStudentAsync(stu).ConfigureAwait(true))
                {
                    geocoded++;
                }
                else
                {
                    Logger.Warning("Bulk plot: failed persisting geocode for student {Id}", stu.StudentId);
                }
            }

            try
            {
                plotted += PlotStudentPins(stu, points);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Plot failed for student {Id}", stu.StudentId);
            }
        }

        var status = plotted == 0
            ? $"Student plotting complete — no locations ({students.Count} in DB; geocoded {geocoded})"
            : $"Student plotting complete — {plotted} locations";
        Logger.Information(
            "Bulk plot complete Geocoded={Geocoded} Plotted={Plotted} Total={Total}",
            geocoded,
            plotted,
            students.Count);
        return new MapStudentBulkPlot(plotted, geocoded, students.Count, status);
    }

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

        try
        {
            return StudentPlotLocation.Index(await service.GetActiveStopsAsync().ConfigureAwait(true));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "LoadPickupCatalogAsync failed");
            return StudentPlotLocation.Index(null);
        }
    }

    private async Task<IReadOnlyList<Destination>> LoadSchoolsAsync(IServiceScope? scope)
    {
        var dest = Resolve(_destinations, scope);
        if (dest is null)
        {
            return Array.Empty<Destination>();
        }

        try
        {
            return await dest.GetActiveSchoolsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "LoadSchoolsAsync failed");
            return Array.Empty<Destination>();
        }
    }

    private async Task<IReadOnlyList<Student>> LoadStudentsAsync(IServiceScope? scope)
    {
        var students = Resolve(_students, scope);
        if (students is null)
        {
            return Array.Empty<Student>();
        }

        try
        {
            return await students.GetAllStudentsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "LoadStudentsAsync failed");
            return Array.Empty<Student>();
        }
    }

    private async Task<(double Lat, double Lon)?> TryGeocodeAsync(Student student, IServiceScope? scope)
    {
        var geocoding = Resolve(_geocoding, scope);
        if (geocoding is null)
        {
            return null;
        }

        try
        {
            var geo = await geocoding.GeocodeAsync(
                student.HomeAddress, student.City, student.State, student.Zip).ConfigureAwait(true);
            if (geo.HasValue && LocationCoordinate.IsValidated(geo.Value.latitude, geo.Value.longitude))
            {
                return (geo.Value.latitude, geo.Value.longitude);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "IGeocodingService geocode failed for student {Id}", student.StudentId);
        }

        return null;
    }

    private static T? Resolve<T>(T? injected, IServiceScope? scope) where T : class =>
        injected
        ?? scope?.ServiceProvider.GetService<T>()
        ?? App.ServiceProvider?.GetService<T>();
}
