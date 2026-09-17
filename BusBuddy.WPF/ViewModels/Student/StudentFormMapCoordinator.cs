using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Student;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Student-form map: the pin window (not District Map). The form is modal, so
/// <see cref="MapViewLauncher"/> opens a window the clerk cannot use. Gold = Google
/// geocode of the street address; blue = clerk pickup. specs/students.md: plot only
/// after coordinates exist; clerks do not type lat/lng.
/// </summary>
public sealed class StudentFormMapCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormMapCoordinator>();

    private readonly Func<StudentModel> _student;
    private readonly Func<PickupStop?> _selectedPickupStop;
    private readonly ObservableCollection<PickupStop> _availablePickupStops;
    private readonly StudentFormValidationCoordinator _validation;
    private readonly Action<StudentModel>? _coordinatesCaptured;
    private readonly IStudentService? _studentService;

    public StudentFormMapCoordinator(
        Func<StudentModel> student,
        Func<PickupStop?> selectedPickupStop,
        ObservableCollection<PickupStop> availablePickupStops,
        StudentFormValidationCoordinator validation,
        Action<StudentModel>? coordinatesCaptured = null,
        IStudentService? studentService = null)
    {
        _student = student;
        _selectedPickupStop = selectedPickupStop;
        _availablePickupStops = availablePickupStops;
        _validation = validation;
        _coordinatesCaptured = coordinatesCaptured;
        _studentService = studentService;
    }

    public Task AdjustHomePinAsync() => OpenHomePinAsync();

    public Task ViewOnMapAsync() => OpenHomePinAsync();

    /// <summary>
    /// Clerk-nudge for a validated (or about-to-be-validated) home. Does not change address text.
    /// </summary>
    private async Task OpenHomePinAsync()
    {
        var student = _student();
        if (string.IsNullOrWhiteSpace(student.HomeAddress) && !student.HasValidatedHomeCoordinates)
        {
            _validation.SetGlobalError("Enter and validate the home address, then adjust the pin.");
            return;
        }

        _validation.IsValidating = true;
        _validation.SetStatus("Loading map preview...", Brushes.Blue);
        try
        {
            var validated = await TryGeocodeAddressAsync(student).ConfigureAwait(true);
            var catalog = ResolveSelectedCatalogStop(student);
            var pinVm = new StudentHomePinViewModel(student, validated, catalog);
            var window = new StudentHomePinWindow(pinVm);
            DialogOwner.Assign(window);
            if (window.ShowDialog() != true || !pinVm.HasMapPick)
            {
                _validation.SetStatus("Map closed — pickup pin unchanged.", Brushes.Gray);
                return;
            }

            student.Latitude = (decimal)pinVm.LatitudeValue;
            student.Longitude = (decimal)pinVm.LongitudeValue;
            _coordinatesCaptured?.Invoke(student);

            if (student.StudentId > 0)
            {
                var service = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
                if (service is null)
                {
                    _validation.SetStatus(
                        "Pickup pin is on the form only. Save the student to persist it.",
                        Brushes.Orange);
                    return;
                }

                var persisted = await service.UpdateHomeGeocodeAsync(
                    student.StudentId,
                    student.Latitude,
                    student.Longitude,
                    student.PlaceId).ConfigureAwait(true);
                if (!persisted)
                {
                    _validation.SetGlobalError(
                        "Could not persist the pickup pin. The form still has the new point — save the student.");
                    _validation.SetStatus("Pickup pin not saved to the database.", Brushes.Orange);
                    return;
                }
            }

            _validation.SetStatus(
                "Pickup pin saved. Street address is unchanged. Refresh Drive Path if this home is already a published stop.",
                Brushes.Green);
            Logger.Information(
                "Clerk adjusted home pin StudentId={StudentId} HasCoords={HasCoords} HadValidatedPin={HadValidated}",
                student.StudentId,
                student.HasValidatedHomeCoordinates,
                validated.HasValue);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening student home pin map");
            _validation.SetGlobalError($"Map view failed: {ex.Message}");
            _validation.SetStatus("Map failed to load", Brushes.Red);
        }
        finally
        {
            _validation.IsValidating = false;
        }
    }

    private PickupStop? ResolveSelectedCatalogStop(StudentModel student)
    {
        if (_selectedPickupStop() is { } selected)
        {
            return selected;
        }

        if (student.PickupStopId is not int stopId)
        {
            return null;
        }

        return _availablePickupStops.FirstOrDefault(s => s.PickupStopId == stopId);
    }

    private async Task<(double Latitude, double Longitude)?> TryGeocodeAddressAsync(StudentModel student)
    {
        if (string.IsNullOrWhiteSpace(student.HomeAddress))
        {
            return null;
        }

        var mapsGeo = App.ServiceProvider?.GetService<IMapsGeoService>()
            ?? App.ServiceProvider?.GetService<IGeocodingService>();
        if (mapsGeo is null)
        {
            return student.HasValidatedHomeCoordinates
                ? ((double)student.Latitude!, (double)student.Longitude!)
                : null;
        }

        var coords = await mapsGeo
            .GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip)
            .ConfigureAwait(true);
        return coords;
    }
}
