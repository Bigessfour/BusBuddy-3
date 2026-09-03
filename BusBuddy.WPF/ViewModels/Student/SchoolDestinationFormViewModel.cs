using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

public sealed class SchoolDestinationFormViewModel : BaseViewModel, IDisposable
{
    private static readonly new ILogger Logger = Log.ForContext<SchoolDestinationFormViewModel>();

    /// <summary>Lamar, CO — clerk district default for the pick-map.</summary>
    public const double DefaultMapLatitude = 38.0872;
    public const double DefaultMapLongitude = -102.6208;
    public const int DefaultMapZoom = 13;

    private readonly IDestinationService _destinations;
    private readonly BusBuddyDbContext? _context;
    private readonly PlacesAddressAutocompleteCoordinator _addressAutocomplete;

    private string _name = string.Empty;
    private string _address = string.Empty;
    private string _city = "Lamar";
    private string _state = "CO";
    private string _zipCode = string.Empty;
    private string _startTimeText = "08:00";
    private string _dismissalTimeText = "15:30";
    private double _latitudeValue;
    private double _longitudeValue;
    private bool _hasMapPick;
    private string _validationMessage = string.Empty;
    private string _mapHint = "Click the map to set school GPS (optional but needed for Generate Routes stop times).";

    public event EventHandler<bool?>? RequestClose;

    public SchoolDestinationFormViewModel(IDestinationService destinations)
    {
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        _context = TryCreateDbContextViaDi();
        var places = App.ServiceProvider?.GetService<IPlacesAutocompleteService>();
        _addressAutocomplete = new PlacesAddressAutocompleteCoordinator(places);
        _addressAutocomplete.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlacesAddressAutocompleteCoordinator.IsPopupOpen))
            {
                OnPropertyChanged(nameof(IsAddressSuggestionPopupOpen));
            }
        };
        // Do NOT gate CanExecute — ButtonAdv often looks enabled while CanExecute=false → silent no-op.
        // Validate inside SaveAsync and surface ValidationMessage instead.
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ClearMapPickCommand = new RelayCommand(ClearMapPick);

        MapMarkers = new ObservableCollection<MapViewModel.MapMarker>();
        MapCenter = new Point(DefaultMapLatitude, DefaultMapLongitude);
        MapZoomLevel = DefaultMapZoom;
    }

    public string Title => "Add school";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value ?? string.Empty);
    }

    public string City
    {
        get => _city;
        set => SetProperty(ref _city, value ?? string.Empty);
    }

    public string State
    {
        get => _state;
        set => SetProperty(ref _state, (value ?? string.Empty).Trim().ToUpperInvariant());
    }

    public string ZipCode
    {
        get => _zipCode;
        set => SetProperty(ref _zipCode, value ?? string.Empty);
    }

    public string StartTimeText
    {
        get => _startTimeText;
        set => SetProperty(ref _startTimeText, value ?? string.Empty);
    }

    public string DismissalTimeText
    {
        get => _dismissalTimeText;
        set => SetProperty(ref _dismissalTimeText, value ?? string.Empty);
    }

    /// <summary>Bound to DoubleTextBox.Value (Syncfusion). 0 means unset unless HasMapPick.</summary>
    public double LatitudeValue
    {
        get => _latitudeValue;
        set
        {
            if (SetProperty(ref _latitudeValue, value))
            {
                if (Math.Abs(value) > 0.0001 || Math.Abs(_longitudeValue) > 0.0001)
                {
                    _hasMapPick = true;
                    RefreshMapMarker();
                }
            }
        }
    }

    public double LongitudeValue
    {
        get => _longitudeValue;
        set
        {
            if (SetProperty(ref _longitudeValue, value))
            {
                if (Math.Abs(value) > 0.0001 || Math.Abs(_latitudeValue) > 0.0001)
                {
                    _hasMapPick = true;
                    RefreshMapMarker();
                }
            }
        }
    }

    public bool HasMapPick
    {
        get => _hasMapPick;
        private set => SetProperty(ref _hasMapPick, value);
    }

    public string MapHint
    {
        get => _mapHint;
        private set => SetProperty(ref _mapHint, value);
    }

    public Point MapCenter { get; private set; }

    public int MapZoomLevel { get; private set; }

    public ObservableCollection<MapViewModel.MapMarker> MapMarkers { get; }

    /// <summary>True after a successful save that stored school GPS.</summary>
    public bool SavedWithGps { get; private set; }

    /// <summary>Destination id from the last successful save (for catalog refresh messages).</summary>
    public int? SavedDestinationId { get; private set; }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ObservableCollection<PlaceAutocompleteSuggestion> AddressSuggestions => _addressAutocomplete.Suggestions;

    public bool IsAddressSuggestionPopupOpen => _addressAutocomplete.IsPopupOpen;

    public bool IsAddressAutocompleteEnabled => _addressAutocomplete.IsEnabled;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearMapPickCommand { get; }

    /// <summary>Called from the view when the clerk clicks the SfMap (GetLatLonFromPoint).</summary>
    public void ApplyMapClick(double latitude, double longitude)
    {
        _latitudeValue = Math.Round(latitude, 6);
        _longitudeValue = Math.Round(longitude, 6);
        HasMapPick = true;
        OnPropertyChanged(nameof(LatitudeValue));
        OnPropertyChanged(nameof(LongitudeValue));
        RefreshMapMarker();
        MapHint = $"Pinned {LatitudeValue:F5}, {LongitudeValue:F5} — adjust on map or in the boxes.";
        Logger.Information("School form map pick Lat={Lat} Lon={Lon}", LatitudeValue, LongitudeValue);
    }

    private void ClearMapPick()
    {
        _latitudeValue = 0;
        _longitudeValue = 0;
        HasMapPick = false;
        MapMarkers.Clear();
        OnPropertyChanged(nameof(LatitudeValue));
        OnPropertyChanged(nameof(LongitudeValue));
        MapHint = "Click the map to set school GPS (optional but needed for Generate Routes stop times).";
    }

    private void RefreshMapMarker()
    {
        MapMarkers.Clear();
        if (!_hasMapPick)
        {
            return;
        }

        MapMarkers.Add(MapViewModel.MapMarker.FromDegrees(_latitudeValue, _longitudeValue, "School"));
        MapCenter = new Point(_latitudeValue, _longitudeValue);
        OnPropertyChanged(nameof(MapCenter));
    }

    private async Task SaveAsync()
    {
        Logger.Information(
            "Save school clicked NameLen={NameLen} AddressLen={AddrLen} City={City} State={State} ZipLen={ZipLen} Start={Start} Dismissal={Dismissal} HasGps={HasGps}",
            Name.Length, Address.Length, City, State, ZipCode.Length, StartTimeText, DismissalTimeText, HasUsableGps());

        var missing = BuildValidationErrors();
        if (missing is not null)
        {
            ValidationMessage = missing;
            Logger.Warning("Save school blocked: {Reason}", missing);
            return;
        }

        try
        {
            if (!TryParseTime(StartTimeText, out var start) || !TryParseTime(DismissalTimeText, out var dismissal))
            {
                ValidationMessage = "Use HH:mm for start and dismissal (example 08:00).";
                return;
            }

            if (_context is not null && !await DatabaseUserMessage.CanConnectAsync(_context).ConfigureAwait(true))
            {
                ValidationMessage = DatabaseUserMessage.UnavailableForOperation("save the school");
                return;
            }

            var (lat, lon) = await ResolveSchoolGpsAsync().ConfigureAwait(true);
            var school = await _destinations.AddSchoolAsync(
                Name.Trim(),
                Address.Trim(),
                City.Trim(),
                State.Trim(),
                ZipCode.Trim(),
                start,
                dismissal,
                lat,
                lon).ConfigureAwait(true);

            SavedWithGps = school.Latitude.HasValue && school.Longitude.HasValue;
            SavedDestinationId = school.DestinationId;
            Logger.Information(
                "School cataloged DestinationId={Id} Name={Name} HasGps={HasGps}",
                school.DestinationId, school.Name, SavedWithGps);
            StatusMessage = SavedWithGps
                ? $"Saved {school.Name}"
                : $"Saved {school.Name} without GPS; Generate Routes will not persist stop times";
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Add school failed");
            ValidationMessage = DatabaseUserMessage.ForOperation(ex, "save the school");
        }
    }

    private static BusBuddyDbContext? TryCreateDbContextViaDi()
    {
        try
        {
            var factory = App.ServiceProvider?.GetService(typeof(IBusBuddyDbContextFactory)) as IBusBuddyDbContextFactory;
            return factory?.CreateDbContext();
        }
        catch
        {
            return null;
        }
    }

    private string? BuildValidationErrors()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return "School name is required.";
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            return "Street address is required (example: 1105 Parkview Ave).";
        }

        if (string.IsNullOrWhiteSpace(City))
        {
            return "City is required.";
        }

        if (string.IsNullOrWhiteSpace(State) || State.Length != 2)
        {
            return "State must be the 2-letter code (example: CO).";
        }

        if (string.IsNullOrWhiteSpace(ZipCode))
        {
            return "ZIP is required.";
        }

        if (!TryParseTime(StartTimeText, out _))
        {
            return "Start time must be HH:mm (example: 08:00).";
        }

        if (!TryParseTime(DismissalTimeText, out _))
        {
            return "Dismissal time must be HH:mm (example: 15:30).";
        }

        return null;
    }

    private static bool TryParseTime(string text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Strip Syncfusion mask prompt chars if present
        var cleaned = text.Replace("_", string.Empty, StringComparison.Ordinal).Trim();
        return TimeSpan.TryParse(cleaned, CultureInfo.InvariantCulture, out value);
    }

    public async Task RefreshAddressSuggestionsAsync(string? input) =>
        await _addressAutocomplete.RefreshSuggestionsAsync(input).ConfigureAwait(true);

    public async Task<bool> ApplyAddressSuggestionAsync(PlaceAutocompleteSuggestion? suggestion)
    {
        var details = await _addressAutocomplete.ApplySuggestionAsync(suggestion).ConfigureAwait(true);
        if (suggestion is null || details is null)
        {
            return false;
        }

        var applied = PlaceAddressApplier.Apply(suggestion, details);
        if (!string.IsNullOrWhiteSpace(applied.Street))
        {
            Address = applied.Street;
        }

        if (!string.IsNullOrWhiteSpace(applied.City))
        {
            City = applied.City;
        }

        if (!string.IsNullOrWhiteSpace(applied.State))
        {
            State = applied.State;
        }

        if (!string.IsNullOrWhiteSpace(applied.Zip))
        {
            ZipCode = applied.Zip;
        }

        if (applied.Latitude.HasValue && applied.Longitude.HasValue)
        {
            ApplyMapClick(applied.Latitude.Value, applied.Longitude.Value);
        }

        ValidationMessage = "Address selected from Google Places — save to persist.";
        return true;
    }

    private bool HasUsableGps() =>
        _hasMapPick
        && Math.Abs(_latitudeValue) > 0.0001
        && Math.Abs(_longitudeValue) > 0.0001
        && _latitudeValue is >= -90 and <= 90
        && _longitudeValue is >= -180 and <= 180;

    private async Task<(decimal? Lat, decimal? Lon)> ResolveSchoolGpsAsync()
    {
        if (HasUsableGps())
        {
            return ((decimal)_latitudeValue, (decimal)_longitudeValue);
        }

        var mapsGeo = App.ServiceProvider?.GetService<IMapsGeoService>();
        if (mapsGeo is null)
        {
            Logger.Warning("IMapsGeoService not registered; school will save without GPS unless map/coords set");
            return (null, null);
        }

        if (!mapsGeo.IsConfigured)
        {
            Logger.Warning("Google Maps API key not configured; school will save without GPS unless map/coords set");
            return (null, null);
        }

        var result = await mapsGeo.ValidateAndGeocodeAsync(Address, City, State, ZipCode).ConfigureAwait(true);
        if (result.Ok && result.Latitude.HasValue && result.Longitude.HasValue)
        {
            return ((decimal)result.Latitude.Value, (decimal)result.Longitude.Value);
        }

        Logger.Warning("School address validation/geocode failed: {Error}", result.ErrorMessage);
        return (null, null);
    }

    public void Dispose()
    {
        _addressAutocomplete.Dispose();
        _context?.Dispose();
    }
}
