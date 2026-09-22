using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
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

    /// <summary>Zoom for a school pick-map once a district or campus exists.</summary>
    public const int DefaultMapZoom = MapDefaults.SchoolZoomLevel;

    private readonly IDestinationService _destinations;
    private readonly BusBuddyDbContext? _context;
    private readonly Destination? _editingSchool;
    private readonly SchoolFormMode _mode;
    private readonly string _loadedAddressKey = string.Empty;
    private bool _gpsTouched;

    private string _name = string.Empty;
    private string _address = string.Empty;
    private string _city = string.Empty;
    private string _state = string.Empty;
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
        : this(destinations, SchoolFormMode.Add, editingSchool: null)
    {
    }

    /// <summary>
    /// Opens the form on an existing campus so a clerk can correct its bell times only.
    /// </summary>
    public static SchoolDestinationFormViewModel ForSchoolTimes(
        IDestinationService destinations,
        Destination school) =>
        new(destinations, SchoolFormMode.TimesOnly, school ?? throw new ArgumentNullException(nameof(school)));

    /// <summary>
    /// Opens the form on an existing campus with every clerk field writable.
    /// </summary>
    public static SchoolDestinationFormViewModel ForEdit(
        IDestinationService destinations,
        Destination school) =>
        new(destinations, SchoolFormMode.EditAll, school ?? throw new ArgumentNullException(nameof(school)));

    private SchoolDestinationFormViewModel(
        IDestinationService destinations,
        SchoolFormMode mode,
        Destination? editingSchool)
    {
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        _mode = mode;
        _editingSchool = editingSchool;
        _context = TryCreateDbContextViaDi();
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ClearMapPickCommand = new RelayCommand(ClearMapPick);
        ValidateAddressCommand = new AsyncRelayCommand(ValidateAddressAsync, CanValidateAddress);

        MapMarkers = new ObservableCollection<MapMarker>();
        var camera = DistrictCameraUi.Resolve();
        MapCenter = new Point(camera.Latitude, camera.Longitude);
        MapZoomLevel = camera.ZoomLevel;
        var district = DistrictCameraUi.CurrentSettings();
        if (!string.IsNullOrWhiteSpace(district?.DepotCity))
        {
            _city = district!.DepotCity!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(district?.DepotState))
        {
            _state = district!.DepotState!.Trim().ToUpperInvariant();
        }

        if (editingSchool is not null)
        {
            PrefillFrom(editingSchool);
            _loadedAddressKey = AddressKey(editingSchool);
        }
    }

    /// <summary>True when the form was opened to correct an existing campus's bell times.</summary>
    public bool IsTimesOnlyEdit => _mode == SchoolFormMode.TimesOnly;

    /// <summary>False in times-only mode, where the campus identity and GPS are not editable.</summary>
    public bool CanEditSchoolDetails => !IsTimesOnlyEdit;

    public string Title => _mode switch
    {
        SchoolFormMode.TimesOnly => "Edit school times",
        SchoolFormMode.EditAll => "Edit school",
        _ => "Add school"
    };

    public string Headline => _mode switch
    {
        SchoolFormMode.TimesOnly => $"Edit bell times for {_editingSchool!.Name}",
        SchoolFormMode.EditAll => $"Edit {_editingSchool!.Name}",
        _ => "Add school (required before Generate Routes)"
    };

    public string SaveButtonLabel => _mode switch
    {
        SchoolFormMode.TimesOnly => "Save times",
        SchoolFormMode.EditAll => "Save school",
        _ => "Save school"
    };

    /// <summary>Start time stored by the last successful times-only save.</summary>
    public TimeSpan? SavedStartTime { get; private set; }

    /// <summary>Dismissal time stored by the last successful times-only save.</summary>
    public TimeSpan? SavedDismissalTime { get; private set; }

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

    public ObservableCollection<MapMarker> MapMarkers { get; }

    /// <summary>True after a successful save that stored school GPS.</summary>
    public bool SavedWithGps { get; private set; }

    /// <summary>Destination id from the last successful save (for catalog refresh messages).</summary>
    public int? SavedDestinationId { get; private set; }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearMapPickCommand { get; }
    public ICommand ValidateAddressCommand { get; }

    /// <summary>Formats a time-picker value as HH:mm for persistence.</summary>
    public static string FormatTimeText(DateTime? pickerValue, string fallbackWhenEmpty)
    {
        if (!pickerValue.HasValue)
        {
            return fallbackWhenEmpty;
        }

        return pickerValue.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Maps stored HH:mm text onto today's date for SfTimePicker.</summary>
    public static DateTime? ParseTimePickerValue(string? text, TimeSpan defaultTime)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DateTime.Today.Add(defaultTime);
        }

        return TryParseTime(text, out var parsed)
            ? DateTime.Today.Add(parsed)
            : DateTime.Today.Add(defaultTime);
    }

    /// <summary>Called from the view when the clerk clicks the SfMap (GetLatLonFromPoint).</summary>
    public void ApplyMapClick(double latitude, double longitude)
    {
        _latitudeValue = Math.Round(latitude, 6);
        _longitudeValue = Math.Round(longitude, 6);
        HasMapPick = true;
        _gpsTouched = true;
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
        _gpsTouched = true;
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

        MapMarkers.Add(MapMarker.FromDegrees(
            _latitudeValue,
            _longitudeValue,
            MapMarkerLabels.ForSchool(string.IsNullOrWhiteSpace(Name) ? "School" : Name),
            MapMarkerLabels.Kind.School));
    }

    private void PrefillFrom(Destination school)
    {
        _name = school.Name;
        _address = school.Address;
        _city = school.City;
        _state = school.State;
        _zipCode = school.ZipCode;
        _startTimeText = school.StartTime?.ToString(@"hh\:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        _dismissalTimeText = school.DismissalTime?.ToString(@"hh\:mm", CultureInfo.InvariantCulture) ?? string.Empty;

        if (school.Latitude.HasValue && school.Longitude.HasValue)
        {
            _latitudeValue = (double)school.Latitude.Value;
            _longitudeValue = (double)school.Longitude.Value;
            _hasMapPick = true;
            RefreshMapMarker();
        }

        _mapHint = _mode == SchoolFormMode.TimesOnly
            ? "Campus location is read-only here. Bell times drive Generate Routes stop times."
            : "Click the map or validate the address to update school GPS.";
    }

    private async Task SaveAsync()
    {
        if (IsTimesOnlyEdit)
        {
            await SaveSchoolTimesAsync(_editingSchool!).ConfigureAwait(true);
            return;
        }

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
            var school = _mode == SchoolFormMode.EditAll
                ? await _destinations.UpdateSchoolAsync(
                    _editingSchool!.DestinationId,
                    Name.Trim(),
                    Address.Trim(),
                    City.Trim(),
                    State.Trim(),
                    ZipCode.Trim(),
                    start,
                    dismissal,
                    lat,
                    lon).ConfigureAwait(true)
                : await _destinations.AddSchoolAsync(
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
                "School cataloged DestinationId={Id} Name={Name} Mode={Mode} HasGps={HasGps}",
                school.DestinationId, school.Name, _mode, SavedWithGps);
            StatusMessage = SavedWithGps
                ? $"Saved {school.Name}"
                : $"Saved {school.Name} without GPS; Generate Routes will not persist stop times";

            if (_mode == SchoolFormMode.EditAll)
            {
                await TryRegenerateSchedulesAsync(school.DestinationId, start).ConfigureAwait(true);
            }

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Save school failed Mode={Mode}", _mode);
            ValidationMessage = DatabaseUserMessage.ForOperation(ex, "save the school");
        }
    }

    /// <summary>
    /// Persists bell times for an existing campus, then asks the route planner to regenerate stop
    /// times so published routes stay consistent with the new schedule. A blank box clears that time.
    /// </summary>
    private async Task SaveSchoolTimesAsync(Destination school)
    {
        if (!TryParseOptionalTime(StartTimeText, out var start))
        {
            ValidationMessage = "Start time must be HH:mm";
            return;
        }

        if (!TryParseOptionalTime(DismissalTimeText, out var dismissal))
        {
            ValidationMessage = "Dismissal time must be HH:mm";
            return;
        }

        try
        {
            if (!await _destinations.UpdateSchoolTimesAsync(school.DestinationId, start, dismissal).ConfigureAwait(true))
            {
                ValidationMessage = "Failed to save school times";
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Update school times failed DestinationId={DestinationId}", school.DestinationId);
            ValidationMessage = DatabaseUserMessage.ForOperation(ex, "save the school times");
            return;
        }

        school.StartTime = start;
        school.DismissalTime = dismissal;
        SavedStartTime = start;
        SavedDismissalTime = dismissal;
        SavedDestinationId = school.DestinationId;
        StatusMessage = "School times saved";
        Logger.Information("School times saved DestinationId={DestinationId}", school.DestinationId);

        await TryRegenerateSchedulesAsync(school.DestinationId, start).ConfigureAwait(true);

        RequestClose?.Invoke(this, true);
    }

    private async Task TryRegenerateSchedulesAsync(int destinationId, TimeSpan? start)
    {
        var planner = App.ServiceProvider?.GetService<IRouteDeterminationService>();
        if (planner is null || !start.HasValue)
        {
            return;
        }

        var regen = await planner
            .RegenerateSchedulesForSchoolAsync(destinationId)
            .ConfigureAwait(true);
        StatusMessage = regen.Success
            ? $"{StatusMessage}; regenerated schedules on {regen.RoutesUpdated} route(s)"
            : $"{StatusMessage}; schedule regen: {regen.Error}";
    }

    /// <summary>An empty box means "no time recorded"; anything else must parse as HH:mm.</summary>
    private static bool TryParseOptionalTime(string? text, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!TryParseTime(text, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
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

    private bool CanValidateAddress() =>
        CanEditSchoolDetails
        && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(City)
        && !string.IsNullOrWhiteSpace(State)
        && State.Length == 2
        && !string.IsNullOrWhiteSpace(ZipCode);

    private async Task ValidateAddressAsync()
    {
        ValidationMessage = string.Empty;
        var mapsGeo = App.ServiceProvider?.GetService<IMapsGeoService>();
        if (mapsGeo is null)
        {
            ValidationMessage = "Address validation is not available (maps service not registered).";
            return;
        }

        if (!mapsGeo.IsConfigured)
        {
            ValidationMessage = "Set GOOGLE_MAPS_API_KEY to validate addresses with Google.";
            return;
        }

        var result = await mapsGeo.ValidateAndGeocodeAsync(Address, City, State, ZipCode).ConfigureAwait(true);
        if (result.Ok && result.Latitude.HasValue && result.Longitude.HasValue)
        {
            ApplyMapClick(result.Latitude.Value, result.Longitude.Value);
            ValidationMessage = "Address validated — GPS updated from Google.";
            Logger.Information(
                "School address validated HasCoords=true Lat={Lat} Lon={Lon}",
                result.Latitude.Value,
                result.Longitude.Value);
            return;
        }

        ValidationMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? "Address needs validation — pick a Google suggestion or correct the street."
            : result.ErrorMessage;
        Logger.Warning("School address validation failed: {Error}", ValidationMessage);
    }

    public void ApplyAppliedAddress(PlaceAddressApplier.AppliedAddress applied)
    {
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
    }

    private bool HasUsableGps() =>
        _hasMapPick
        && Math.Abs(_latitudeValue) > 0.0001
        && Math.Abs(_longitudeValue) > 0.0001
        && _latitudeValue is >= -90 and <= 90
        && _longitudeValue is >= -180 and <= 180;

    private async Task<(decimal? Lat, decimal? Lon)> ResolveSchoolGpsAsync()
    {
        var addressChanged = _mode == SchoolFormMode.EditAll
            && !string.Equals(_loadedAddressKey, AddressKey(Address, City, State, ZipCode), StringComparison.Ordinal);
        var useMapPick = HasUsableGps() && !(addressChanged && !_gpsTouched);

        if (useMapPick)
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
        _context?.Dispose();
    }

    private static string AddressKey(Destination school) =>
        AddressKey(school.Address, school.City, school.State, school.ZipCode);

    private static string AddressKey(string? address, string? city, string? state, string? zip) =>
        string.Join('\u001f', address?.Trim(), city?.Trim(), state?.Trim(), zip?.Trim());

    private enum SchoolFormMode
    {
        Add,
        EditAll,
        TimesOnly
    }
}
