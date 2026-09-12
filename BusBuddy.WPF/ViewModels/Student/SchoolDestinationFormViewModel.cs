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
using BusBuddy.Core.Services.Interfaces;
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
        : this(destinations, editingSchool: null)
    {
    }

    /// <summary>
    /// Opens the form on an existing campus so a clerk can correct its bell times. Only the times are
    /// writable: <see cref="IDestinationService"/> exposes <c>UpdateSchoolTimesAsync</c> and no general
    /// campus update, so name, address, and GPS stay read-only rather than silently discarding edits.
    /// </summary>
    public static SchoolDestinationFormViewModel ForSchoolTimes(
        IDestinationService destinations,
        Destination school) =>
        new(destinations, school ?? throw new ArgumentNullException(nameof(school)));

    private SchoolDestinationFormViewModel(IDestinationService destinations, Destination? editingSchool)
    {
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        _editingSchool = editingSchool;
        _context = TryCreateDbContextViaDi();
        // Do NOT gate CanExecute — ButtonAdv often looks enabled while CanExecute=false → silent no-op.
        // Validate inside SaveAsync and surface ValidationMessage instead.
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ClearMapPickCommand = new RelayCommand(ClearMapPick);

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
        }
    }

    /// <summary>True when the form was opened to correct an existing campus's bell times.</summary>
    public bool IsTimesOnlyEdit => _editingSchool is not null;

    /// <summary>False in times-only mode, where the campus identity and GPS are not editable.</summary>
    public bool CanEditSchoolDetails => !IsTimesOnlyEdit;

    public string Title => IsTimesOnlyEdit ? "Edit school times" : "Add school";

    public string Headline => IsTimesOnlyEdit
        ? $"Edit bell times for {_editingSchool!.Name}"
        : "Add school (required before Generate Routes)";

    public string SaveButtonLabel => IsTimesOnlyEdit ? "Save times" : "Save school";

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

        MapMarkers.Add(MapMarker.FromDegrees(
            _latitudeValue,
            _longitudeValue,
            MapMarkerLabels.ForSchool(string.IsNullOrWhiteSpace(Name) ? "School" : Name),
            MapMarkerLabels.Kind.School));
        MapCenter = new Point(_latitudeValue, _longitudeValue);
        OnPropertyChanged(nameof(MapCenter));
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

        _mapHint = "Campus location is read-only here. Bell times drive Generate Routes stop times.";
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

        var planner = App.ServiceProvider?.GetService<IRouteDeterminationService>();
        if (planner is not null && start.HasValue)
        {
            var regen = await planner
                .RegenerateSchedulesForSchoolAsync(school.DestinationId)
                .ConfigureAwait(true);
            StatusMessage = regen.Success
                ? $"School times saved; regenerated schedules on {regen.RoutesUpdated} route(s)"
                : $"School times saved; schedule regen: {regen.Error}";
        }

        RequestClose?.Invoke(this, true);
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
        _context?.Dispose();
    }
}
