using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Address validation and geocode for the student form.
/// Places type-ahead lives on <c>PlacesAddressBox</c>; this coordinator maps an applied result.
/// </summary>
public sealed class StudentFormAddressCoordinator : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormAddressCoordinator>();

    private readonly IMapsGeoService? _mapsGeo;
    private readonly IStudentService? _studentService;
    private readonly IPlacesAutocompleteService? _places;
    private string _validationMessage = string.Empty;
    private Brush _validationColor = Brushes.Gray;
    private bool _validationFailed;
    private bool _disableValidation;
    private string? _pinnedAddressKey;

    public StudentFormAddressCoordinator(
        IMapsGeoService? mapsGeo,
        IPlacesAutocompleteService? placesAutocomplete,
        IStudentService? studentService = null,
        StudentModel? loadedStudent = null)
    {
        _mapsGeo = mapsGeo;
        _studentService = studentService;
        _places = placesAutocomplete;
        TrackPinnedAddress(loadedStudent);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Fired after validate/geocode sets student coordinates.</summary>
    public event EventHandler? CoordinatesUpdated;

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public Brush ValidationColor
    {
        get => _validationColor;
        private set => SetProperty(ref _validationColor, value);
    }

    public bool ValidationFailed
    {
        get => _validationFailed;
        private set => SetProperty(ref _validationFailed, value);
    }

    /// <summary>
    /// Suppresses interactive Places autocomplete only. It does NOT suppress Address Validation on
    /// save: specs/students.md requires home addresses to be validated on every write path, with
    /// failure recorded as "incomplete" rather than skipped.
    /// </summary>
    public bool DisableValidation
    {
        get => _disableValidation;
        set
        {
            if (_disableValidation == value)
            {
                return;
            }

            _disableValidation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAutocompleteEnabled));
        }
    }

    public bool IsAutocompleteEnabled => _places?.IsConfigured == true && !DisableValidation;

    /// <summary>
    /// Normalized street|city|state|zip. Used to tie the stored pin to the address text that produced it.
    /// </summary>
    public static string AddressKey(StudentModel student) =>
        string.Join(
            "|",
            new[] { student.HomeAddress, student.City, student.State, student.Zip }
                .Select(part => (part ?? string.Empty).Trim().ToUpperInvariant()));

    /// <summary>
    /// Records the address text the current coordinates belong to (loaded record, or a fresh
    /// validate / Places / geocode result). Cleared when the record has no validated pin.
    /// </summary>
    public void TrackPinnedAddress(StudentModel? student)
    {
        _pinnedAddressKey = student is not null && student.HasValidatedHomeCoordinates
            ? AddressKey(student)
            : null;
    }

    /// <summary>
    /// False when the clerk edited the address text after the pin was produced and no re-validation
    /// succeeded. Unknown provenance (coordinates but no tracked key) is treated as a mismatch too:
    /// the only way a form-owned pin is trusted is if this coordinator saw the address that produced it.
    /// </summary>
    public bool CoordinatesMatchAddress(StudentModel student)
    {
        if (!student.HasValidatedHomeCoordinates)
        {
            return true;
        }

        return _pinnedAddressKey is not null
            && string.Equals(_pinnedAddressKey, AddressKey(student), StringComparison.Ordinal);
    }

    public void SetMinimalFieldsPresentMessage()
    {
        ValidationFailed = false;
        ValidationMessage = "✓ Minimal required fields present.";
        ValidationColor = Brushes.Green;
    }

    public async Task ApplyAppliedAsync(StudentModel student, PlaceAddressApplier.AppliedAddress applied)
    {
        if (!string.IsNullOrWhiteSpace(applied.Street))
        {
            student.HomeAddress = applied.Street;
        }

        if (!string.IsNullOrWhiteSpace(applied.City))
        {
            student.City = applied.City;
        }

        if (!string.IsNullOrWhiteSpace(applied.State))
        {
            student.State = applied.State;
        }

        if (!string.IsNullOrWhiteSpace(applied.Zip))
        {
            student.Zip = applied.Zip;
        }

        if (!string.IsNullOrWhiteSpace(applied.PlaceId))
        {
            student.PlaceId = applied.PlaceId;
        }

        var hasPlaceCoordinates = applied.Latitude.HasValue
            && applied.Longitude.HasValue
            && LocationCoordinate.IsValidated((decimal)applied.Latitude.Value, (decimal)applied.Longitude.Value);

        if (hasPlaceCoordinates)
        {
            student.Latitude = (decimal)applied.Latitude!.Value;
            student.Longitude = (decimal)applied.Longitude!.Value;
        }
        else
        {
            student.Latitude = null;
            student.Longitude = null;
        }

        TrackPinnedAddress(student);

        ValidationFailed = false;
        ValidationMessage = hasPlaceCoordinates
            ? "Address selected and coordinates captured from Places."
            : "Address selected — click Validate Address to capture coordinates before save.";
        ValidationColor = hasPlaceCoordinates ? Brushes.Green : Brushes.Blue;
        Logger.Information(
            "Places suggestion applied PlaceIdPrefix={PlaceIdPrefix} CoordinatesCaptured={CoordinatesCaptured}",
            string.IsNullOrEmpty(applied.PlaceId) ? string.Empty : applied.PlaceId[..Math.Min(8, applied.PlaceId.Length)],
            hasPlaceCoordinates);

        if (hasPlaceCoordinates)
        {
            await PersistIfExistingAsync(student).ConfigureAwait(true);
            CoordinatesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ValidateAsync(StudentModel student)
    {
        try
        {
            Logger.Information("Validating address for student");
            if (string.IsNullOrWhiteSpace(student.HomeAddress))
            {
                ValidationFailed = false;
                ValidationMessage = "Please enter an address before validating.";
                ValidationColor = Brushes.Orange;
                return;
            }

            var mapsGeo = _mapsGeo ?? App.ServiceProvider?.GetService<IMapsGeoService>();
            if (mapsGeo is not null)
            {
                var maps = await mapsGeo.ValidateAndGeocodeAsync(
                    student.HomeAddress, student.City, student.State, student.Zip).ConfigureAwait(true);
                if (maps.Ok)
                {
                    if (maps.Latitude.HasValue)
                    {
                        student.Latitude = (decimal)maps.Latitude.Value;
                    }

                    if (maps.Longitude.HasValue)
                    {
                        student.Longitude = (decimal)maps.Longitude.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(maps.PlaceId))
                    {
                        student.PlaceId = maps.PlaceId;
                    }

                    ValidationFailed = false;
                    var precisionNote = string.IsNullOrWhiteSpace(maps.Precision)
                        ? string.Empty
                        : $" ({maps.Precision} precision)";
                    ValidationMessage = string.IsNullOrWhiteSpace(maps.FormattedAddress)
                        ? $"Address validated via Google Maps{precisionNote}."
                        : $"Address validated{precisionNote}: {maps.FormattedAddress}";
                    ValidationColor = Brushes.Green;
                    Logger.Information("Address validation successful via Maps Platform");
                    TrackPinnedAddress(student);
                    await PersistIfExistingAsync(student).ConfigureAwait(true);
                    CoordinatesUpdated?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (maps.MappingUnconfigured)
                {
                    if (student.HasValidatedHomeCoordinates)
                    {
                        ReportLiveValidationUnavailable(
                            student,
                            maps.ErrorMessage ?? "GOOGLE_MAPS_API_KEY not configured");
                        return;
                    }

                    await ApplyLocalFallbackAsync(
                        student,
                        "Google Maps API key not configured (set GOOGLE_MAPS_API_KEY). Local format check:")
                        .ConfigureAwait(true);
                    return;
                }

                ReportLiveValidationUnavailable(student, maps.ErrorMessage ?? "undeliverable or incomplete");
                return;
            }

            await ApplyLocalFallbackAsync(
                student,
                "Maps Address Validation is not registered in DI. Local format check:")
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error validating address");
            ValidationFailed = true;
            ValidationMessage = "Error validating address. Please check format and try again.";
            ValidationColor = Brushes.Red;
        }
    }

    public async Task<bool> TryGeocodeAsync(StudentModel student)
    {
        if (LocationCoordinate.IsValidated(student.Latitude, student.Longitude))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(student.HomeAddress))
        {
            return false;
        }

        var mapsGeo = _mapsGeo ?? App.ServiceProvider?.GetService<IMapsGeoService>();
        if (mapsGeo is not null)
        {
            var maps = await mapsGeo.ValidateAndGeocodeAsync(
                student.HomeAddress, student.City, student.State, student.Zip).ConfigureAwait(true);
            if (maps.Ok && maps.Latitude.HasValue && maps.Longitude.HasValue)
            {
                student.Latitude = (decimal)maps.Latitude.Value;
                student.Longitude = (decimal)maps.Longitude.Value;
                if (!string.IsNullOrWhiteSpace(maps.PlaceId))
                {
                    student.PlaceId = maps.PlaceId;
                }

                TrackPinnedAddress(student);
                await PersistIfExistingAsync(student).ConfigureAwait(true);
                return true;
            }
        }

        var geocoder = App.ServiceProvider?.GetService<IGeocodingService>();
        if (geocoder is not null && !ReferenceEquals(geocoder, mapsGeo))
        {
            var geo = await geocoder.GeocodeAsync(
                student.HomeAddress, student.City, student.State, student.Zip).ConfigureAwait(true);
            if (geo.HasValue)
            {
                student.Latitude = (decimal)geo.Value.latitude;
                student.Longitude = (decimal)geo.Value.longitude;
                TrackPinnedAddress(student);
                await PersistIfExistingAsync(student).ConfigureAwait(true);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Save-path geocode: unlike <see cref="TryGeocodeAsync"/> this does not trust coordinates that are
    /// already present, because the address text may have changed since they were produced.
    /// </summary>
    public Task<bool> TryGeocodeForSaveAsync(StudentModel student)
    {
        if (!CoordinatesMatchAddress(student))
        {
            Logger.Information(
                "Address text changed since coordinates were captured StudentId={StudentId} — re-geocoding before save",
                student.StudentId);
            student.Latitude = null;
            student.Longitude = null;
            student.PlaceId = null;
        }

        return TryGeocodeAsync(student);
    }

    public void Dispose()
    {
        // Address type-ahead lives on PlacesAddressBox; this coordinator only maps applied details.
    }

    private async Task ApplyLocalFallbackAsync(StudentModel student, string prefix)
    {
        var local = StudentAddressValidator.ValidateComponents(
            student.HomeAddress ?? string.Empty,
            student.City ?? string.Empty,
            student.State ?? string.Empty,
            student.Zip ?? string.Empty);

        if (!local.IsValid)
        {
            ValidationFailed = true;
            ValidationMessage = $"{prefix} {local.ErrorMessage}";
            ValidationColor = Brushes.Red;
            Logger.Warning("Address local format failed: {Error}", local.ErrorMessage);
            return;
        }

        if (await TryGeocodeAsync(student).ConfigureAwait(true))
        {
            ValidationFailed = false;
            ValidationMessage = student.HasValidatedHomeCoordinates
                ? $"{prefix} format OK. Stored coordinates were kept (live geocode is not configured)."
                : $"{prefix} format OK; GPS coordinates captured.";
            ValidationColor = Brushes.Green;
            CoordinatesUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        ValidationFailed = true;
        ValidationMessage = $"{prefix} street/city/state/ZIP look OK. GPS geocode unavailable — needs validation.";
        ValidationColor = Brushes.Orange;
        Logger.Warning("Address local format OK; geocoding unavailable — not a pin");
    }

    private void ReportLiveValidationUnavailable(StudentModel student, string liveError)
    {
        if (student.HasValidatedHomeCoordinates)
        {
            ValidationFailed = false;
            ValidationMessage =
                $"Live Address Validation unavailable: {liveError} Stored lat/lng on this record were kept — they are not missing.";
            ValidationColor = Brushes.Orange;
            Logger.Warning(
                "Address Validation unavailable but stored coordinates exist StudentId={StudentId}: {Error}",
                student.StudentId,
                liveError);
            return;
        }

        ValidationFailed = true;
        ValidationMessage = $"Address validation failed: {liveError}";
        ValidationColor = Brushes.Red;
        Logger.Warning("Address validation failed: {Error}", liveError);
    }

    private async Task PersistIfExistingAsync(StudentModel student)
    {
        if (student.StudentId <= 0)
        {
            return;
        }

        var service = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (service is null)
        {
            Logger.Warning("Geocode not persisted — IStudentService unavailable");
            return;
        }

        try
        {
            var persisted = await service.UpdateHomeGeocodeAsync(
                student.StudentId,
                student.Latitude,
                student.Longitude,
                student.PlaceId).ConfigureAwait(true);
            if (!persisted.IsSuccess)
            {
                Logger.Warning(
                    "Failed to persist geocode for StudentId={StudentId}: {Error}",
                    student.StudentId,
                    persisted.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to persist geocode for StudentId={StudentId}", student.StudentId);
        }
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
