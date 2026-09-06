using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Address validation, Places autocomplete, and geocode for the student form.
/// Keeps Maps orchestration out of <see cref="StudentFormViewModel"/>.
/// </summary>
public sealed class StudentFormAddressCoordinator : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormAddressCoordinator>();

    private readonly IMapsGeoService? _mapsGeo;
    private readonly PlacesAddressAutocompleteCoordinator _autocomplete;
    private string _validationMessage = string.Empty;
    private Brush _validationColor = Brushes.Gray;
    private bool _validationFailed;
    private bool _disableValidation;

    public StudentFormAddressCoordinator(
        IMapsGeoService? mapsGeo,
        IPlacesAutocompleteService? placesAutocomplete)
    {
        _mapsGeo = mapsGeo;
        _autocomplete = new PlacesAddressAutocompleteCoordinator(placesAutocomplete);
        _autocomplete.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlacesAddressAutocompleteCoordinator.IsPopupOpen))
            {
                OnPropertyChanged(nameof(IsPopupOpen));
            }
        };
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
            if (value)
            {
                ValidationFailed = false;
                ValidationMessage = "Address validation disabled";
                ValidationColor = Brushes.Gray;
            }
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<PlaceAutocompleteSuggestion> Suggestions =>
        _autocomplete.Suggestions;

    public bool IsAutocompleteEnabled => _autocomplete.IsEnabled && !DisableValidation;

    public bool IsPopupOpen => _autocomplete.IsPopupOpen;

    public void SetMinimalFieldsPresentMessage()
    {
        ValidationFailed = false;
        ValidationMessage = "✓ Minimal required fields present.";
        ValidationColor = Brushes.Green;
    }

    public Task RefreshSuggestionsAsync(string? input) =>
        _autocomplete.RefreshSuggestionsAsync(input);

    public async Task ApplySuggestionAsync(StudentModel student, PlaceAutocompleteSuggestion? suggestion)
    {
        var details = await _autocomplete.ApplySuggestionAsync(suggestion).ConfigureAwait(true);
        if (suggestion is null)
        {
            return;
        }

        if (details is null)
        {
            ValidationMessage = "Could not load address details for that suggestion.";
            ValidationColor = Brushes.Orange;
            return;
        }

        var applied = PlaceAddressApplier.Apply(suggestion, details);
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

        student.Latitude = null;
        student.Longitude = null;
        ValidationFailed = false;
        ValidationMessage = "Address selected — click Validate Address before save.";
        ValidationColor = Brushes.Blue;
        Logger.Information(
            "Places suggestion applied PlaceIdPrefix={PlaceIdPrefix}",
            suggestion.PlaceId[..Math.Min(8, suggestion.PlaceId.Length)]);
    }

    public async Task ValidateAsync(StudentModel student)
    {
        try
        {
            if (DisableValidation)
            {
                ValidationFailed = false;
                ValidationMessage = "Address validation disabled";
                ValidationColor = Brushes.Gray;
                return;
            }

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

                    ValidationFailed = false;
                    var precisionNote = string.IsNullOrWhiteSpace(maps.Precision)
                        ? string.Empty
                        : $" ({maps.Precision} precision)";
                    ValidationMessage = string.IsNullOrWhiteSpace(maps.FormattedAddress)
                        ? $"Address validated via Google Maps{precisionNote}."
                        : $"Address validated{precisionNote}: {maps.FormattedAddress}";
                    ValidationColor = Brushes.Green;
                    Logger.Information("Address validation successful via Maps Platform");
                    CoordinatesUpdated?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (maps.MappingUnconfigured)
                {
                    await ApplyLocalFallbackAsync(
                        student,
                        "Google Maps API key not configured (set GOOGLE_MAPS_API_KEY). Local format check:")
                        .ConfigureAwait(true);
                    return;
                }

                ValidationFailed = true;
                ValidationMessage = $"Address validation failed: {maps.ErrorMessage ?? "undeliverable or incomplete"}";
                ValidationColor = Brushes.Red;
                Logger.Warning("Address validation failed: {Error}", maps.ErrorMessage);
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
        if (student.Latitude.HasValue && student.Longitude.HasValue)
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
                return true;
            }
        }

        return false;
    }

    public void Dispose() => _autocomplete.Dispose();

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
            ValidationMessage = $"{prefix} format OK; GPS coordinates captured.";
            ValidationColor = Brushes.Green;
            CoordinatesUpdated?.Invoke(this, EventArgs.Empty);
            return;
        }

        ValidationFailed = false;
        ValidationMessage = $"{prefix} street/city/state/ZIP look OK. GPS geocode unavailable.";
        ValidationColor = Brushes.Orange;
        Logger.Warning("Address local format OK; geocoding unavailable");
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
