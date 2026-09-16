using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Opens the district map for one student: the pickup stop when it has GPS, otherwise the validated
/// home. specs/students.md allows a pin only for coordinates that came from Address Validation, so a
/// student with no usable coordinates opens the map without a pin rather than being scattered.
/// </summary>
public sealed class StudentFormMapCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormMapCoordinator>();

    private readonly Func<StudentModel> _student;
    private readonly Func<PickupStop?> _selectedPickupStop;
    private readonly ObservableCollection<PickupStop> _availablePickupStops;
    private readonly StudentFormValidationCoordinator _validation;
    private readonly Action<StudentModel>? _coordinatesCaptured;

    public StudentFormMapCoordinator(
        Func<StudentModel> student,
        Func<PickupStop?> selectedPickupStop,
        ObservableCollection<PickupStop> availablePickupStops,
        StudentFormValidationCoordinator validation,
        Action<StudentModel>? coordinatesCaptured = null)
    {
        _student = student;
        _selectedPickupStop = selectedPickupStop;
        _availablePickupStops = availablePickupStops;
        _validation = validation;
        _coordinatesCaptured = coordinatesCaptured;
    }

    public async Task ViewOnMapAsync()
    {
        var student = _student();
        try
        {
            Logger.Information("Opening map view for StudentId={StudentId}", student.StudentId);

            var sp = App.ServiceProvider;
            var pickups = await ResolvePickupCatalogForPlotAsync(sp).ConfigureAwait(true);
            var pins = StudentPlotLocation.PinsFromStored(student, pickups);
            if (pins.Count == 0 && string.IsNullOrWhiteSpace(student.HomeAddress))
            {
                _validation.SetGlobalError("Please enter a home address before viewing on map.");
                return;
            }

            _validation.IsValidating = true;
            _validation.SetStatus("Loading map preview...", Brushes.Blue);

            var mapsGeo = sp?.GetService<IMapsGeoService>() ?? sp?.GetService<IGeocodingService>();
            if (pins.Count == 0)
            {
                if (mapsGeo is null)
                {
                    ReportMappingUnconfigured();
                    MapViewLauncher.Show(Application.Current?.MainWindow as Window, _ => { });
                    return;
                }

                pins = await GeocodeHomePinAsync(student, mapsGeo).ConfigureAwait(true);
                if (pins.Count > 0)
                {
                    // The pin now belongs to the address text as typed; the save path uses this to
                    // drop coordinates if the clerk edits the address afterwards without re-validating.
                    _coordinatesCaptured?.Invoke(student);
                }
            }

            ShowMap(student, pins);
            ReportPlotOutcome(pins, mapsGeo);

            Logger.Information(
                "Map view opened for StudentId={StudentId} Pins={PinCount}",
                student.StudentId,
                pins.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening map view");
            _validation.SetGlobalError($"Map view failed: {ex.Message}");
            _validation.SetStatus("❌ Map failed to load", Brushes.Red);
        }
        finally
        {
            _validation.IsValidating = false;
        }
    }

    /// <summary>
    /// The stop the student actually boards at: the in-flight selection, the already-loaded catalog
    /// entry, or a direct lookup. Null means "plot the home instead".
    /// </summary>
    private async Task<IReadOnlyDictionary<int, PickupStop>?> ResolvePickupCatalogForPlotAsync(IServiceProvider? sp)
    {
        if (_selectedPickupStop() is { } selected)
        {
            return StudentPlotLocation.Index([selected]);
        }

        if (_student().PickupStopId is not int stopId)
        {
            return null;
        }

        var listed = _availablePickupStops.FirstOrDefault(s => s.PickupStopId == stopId);
        if (listed is not null)
        {
            return StudentPlotLocation.Index([listed]);
        }

        var stopService = sp?.GetService<IPickupStopService>();
        var stop = stopService is not null
            ? await stopService.GetByIdAsync(stopId).ConfigureAwait(true)
            : null;
        return stop is not null ? StudentPlotLocation.Index([stop]) : null;
    }

    private static async Task<IReadOnlyList<StudentPlotPoint>> GeocodeHomePinAsync(
        StudentModel student,
        IGeocodingService mapsGeo)
    {
        var coords = await mapsGeo
            .GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip)
            .ConfigureAwait(true);
        if (!coords.HasValue)
        {
            return [];
        }

        student.Latitude = (decimal)coords.Value.latitude;
        student.Longitude = (decimal)coords.Value.longitude;
        return [new StudentPlotPoint(coords.Value.latitude, coords.Value.longitude, AtPickup: false, PickupName: null)];
    }

    private static void ShowMap(StudentModel student, IReadOnlyList<StudentPlotPoint> pins)
    {
        MapViewLauncher.Show(Application.Current?.MainWindow as Window, vm =>
        {
            MapStudentPlot.Draw(
                (lat, lon, names, label, ids) => vm.PlotStop(lat, lon, names, label, studentIds: ids),
                student,
                pins);
            if (pins.Count > 0)
            {
                vm.CenterOnMarkers();
            }
        });
    }

    private void ReportPlotOutcome(IReadOnlyList<StudentPlotPoint> pins, IGeocodingService? mapsGeo)
    {
        if (pins.Count > 0)
        {
            _validation.SetStatus(
                pins[0].AtPickup ? $"✓ Location plotted at {pins[0].PickupName}" : "✓ Location plotted on map",
                Brushes.Green);
            return;
        }

        if (mapsGeo is null || (mapsGeo is IMapsGeoService maps && !maps.IsConfigured))
        {
            ReportMappingUnconfigured();
            return;
        }

        _validation.SetStatus("Address could not be geocoded — map opened without a pin.", Brushes.Orange);
    }

    private void ReportMappingUnconfigured() =>
        _validation.SetStatus("Mapping is not configured (missing GOOGLE_MAPS_API_KEY).", Brushes.Orange);
}
