using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Utilities;

internal readonly record struct MapLayerSeedCounts(int Schools, int Pickups, int Students);

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
    private readonly Action<double, double, IEnumerable<string>?, string?> _plot;

    public MapDistrictLayers(
        IPickupStopService? pickups,
        IDestinationService? destinations,
        IStudentService? students,
        IGeocodingService? geocoding,
        IServiceScopeFactory? scopes,
        Action<double, double, IEnumerable<string>?, string?> plot)
    {
        _pickups = pickups;
        _destinations = destinations;
        _students = students;
        _geocoding = geocoding;
        _scopes = scopes;
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
    }

    public async Task<MapLayerSeedCounts> SeedAsync()
    {
        using var scope = _scopes?.CreateScope();
        var pickupsTask = LoadPickupCatalogAsync(scope);
        var schoolsTask = LoadSchoolsAsync(scope);
        var studentsTask = LoadStudentsAsync(scope);
        await Task.WhenAll(pickupsTask, schoolsTask, studentsTask).ConfigureAwait(true);

        var pickups = await pickupsTask.ConfigureAwait(true);
        var schoolCount = PlotSchools(await schoolsTask.ConfigureAwait(true));
        var pickupCount = PlotPickups(pickups);
        var studentCount = PlotStoredStudents(await studentsTask.ConfigureAwait(true), pickups);
        return new MapLayerSeedCounts(schoolCount, pickupCount, studentCount);
    }

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
            var point = StudentPlotLocation.TryFromStored(stu, pickups);
            if (point is null)
            {
                var geo = await TryGeocodeAsync(stu, scope).ConfigureAwait(true);
                if (geo is null)
                {
                    continue;
                }

                point = new StudentPlotPoint(geo.Value.Lat, geo.Value.Lon, AtPickup: false, PickupName: null);
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
                PlotStudent(stu, point.Value);
                plotted++;
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
        foreach (var school in schools.Where(s => s.HasGpsCoordinates))
        {
            _plot((double)school.Latitude!, (double)school.Longitude!, null, MapMarkerLabels.ForSchool(school.Name));
            plotted++;
        }

        return plotted;
    }

    private int PlotPickups(IReadOnlyDictionary<int, PickupStop> catalog)
    {
        var plotted = 0;
        foreach (var stop in catalog.Values)
        {
            _plot((double)stop.Latitude, (double)stop.Longitude, null, MapMarkerLabels.ForPickup(stop.Name));
            plotted++;
        }

        return plotted;
    }

    private int PlotStoredStudents(IReadOnlyList<Student> students, IReadOnlyDictionary<int, PickupStop> pickups)
    {
        var plotted = 0;
        foreach (var stu in students)
        {
            var point = StudentPlotLocation.TryFromStored(stu, pickups);
            if (point is null)
            {
                continue;
            }

            PlotStudent(stu, point.Value);
            plotted++;
        }

        return plotted;
    }

    private void PlotStudent(Student student, StudentPlotPoint point)
    {
        var name = student.StudentName ?? student.StudentNumber ?? "Student";
        var label = point.AtPickup ? MapMarkerLabels.ForPickup(point.PickupName) : name;
        _plot(point.Latitude, point.Longitude, new[] { name }, label);
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
        if (_geocoding is not null)
        {
            try
            {
                var geo = await _geocoding.GeocodeAsync(
                    student.HomeAddress, student.City, student.State, student.Zip).ConfigureAwait(true);
                if (geo.HasValue)
                {
                    return (geo.Value.latitude, geo.Value.longitude);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "IGeocodingService geocode failed for student {Id}", student.StudentId);
            }
        }

        var mapsGeo = scope?.ServiceProvider.GetService<IMapsGeoService>();
        if (mapsGeo is null || !mapsGeo.IsConfigured)
        {
            return null;
        }

        try
        {
            return await mapsGeo.GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "IMapsGeoService geocode failed for student {Id}", student.StudentId);
            return null;
        }
    }

    private static T? Resolve<T>(T? injected, IServiceScope? scope) where T : class =>
        injected
        ?? scope?.ServiceProvider.GetService<T>()
        ?? App.ServiceProvider?.GetService<T>();
}
