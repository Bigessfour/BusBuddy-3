using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

public sealed class PickupStopFormViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<PickupStopFormViewModel>();

    public const int DefaultMapZoom = MapDefaults.SchoolZoomLevel;

    private readonly IPickupStopService _pickupStops;
    private readonly IStudentService? _students;
    private readonly IMapsGeoService? _mapsGeo;
    private int _nameSuggestSeq;

    private string _name = string.Empty;
    private string _address = string.Empty;
    private string _selectedStopType = PickupStopTypes.Corner;
    private string _notes = string.Empty;
    private double _latitudeValue;
    private double _longitudeValue;
    private bool _hasMapPick;
    private string _validationMessage = string.Empty;
    private string _mapHint = "Click the map to pin the pickup stop (corner or block meeting point).";

    public event EventHandler<bool?>? RequestClose;

    public PickupStopFormViewModel(
        IPickupStopService pickupStops,
        IStudentService? students = null,
        IMapsGeoService? mapsGeo = null)
    {
        _pickupStops = pickupStops ?? throw new ArgumentNullException(nameof(pickupStops));
        _students = students;
        _mapsGeo = mapsGeo;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ClearMapPickCommand = new RelayCommand(ClearMapPick);

        StopTypeOptions = new ObservableCollection<string>(PickupStopTypes.All);
        MapMarkers = new ObservableCollection<MapMarker>();
        var camera = DistrictCameraUi.Resolve();
        MapCenter = new Point(camera.Latitude, camera.Longitude);
        MapZoomLevel = camera.ZoomLevel;
    }

    public string Title => "Add pickup stop";

    public ObservableCollection<string> StopTypeOptions { get; }

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

    public string SelectedStopType
    {
        get => _selectedStopType;
        set => SetProperty(ref _selectedStopType, string.IsNullOrWhiteSpace(value) ? PickupStopTypes.Corner : value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value ?? string.Empty);
    }

    public double LatitudeValue
    {
        get => _latitudeValue;
        set
        {
            if (SetProperty(ref _latitudeValue, value) && (Math.Abs(value) > 0.0001 || Math.Abs(_longitudeValue) > 0.0001))
            {
                _hasMapPick = true;
                RefreshMapMarker();
            }
        }
    }

    public double LongitudeValue
    {
        get => _longitudeValue;
        set
        {
            if (SetProperty(ref _longitudeValue, value) && (Math.Abs(value) > 0.0001 || Math.Abs(_latitudeValue) > 0.0001))
            {
                _hasMapPick = true;
                RefreshMapMarker();
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

    public int? SavedPickupStopId { get; private set; }

    /// <summary>Count of Home pickups near the new stop. Clerk assigns them; we do not auto-attach.</summary>
    public string NearbyHomePickupHint { get; private set; } = string.Empty;

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearMapPickCommand { get; }

    public void ApplyMapClick(double latitude, double longitude)
    {
        _latitudeValue = Math.Round(latitude, 6);
        _longitudeValue = Math.Round(longitude, 6);
        HasMapPick = true;
        OnPropertyChanged(nameof(LatitudeValue));
        OnPropertyChanged(nameof(LongitudeValue));
        RefreshMapMarker();
        MapHint = $"Pinned {LatitudeValue:F5}, {LongitudeValue:F5} — shared stop for students on this block.";
        Logger.Information("Pickup stop map pick Lat={Lat} Lon={Lon}", LatitudeValue, LongitudeValue);
    }

    /// <summary>
    /// Fills Name (and Address if empty) from Places or a reverse-geocoded pin. Does not overwrite a typed name.
    /// </summary>
    public bool TrySuggestName(string? street, string? formattedAddress)
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        var suggested = CatalogStopName.Suggest(street, formattedAddress);
        if (string.IsNullOrWhiteSpace(suggested))
        {
            return false;
        }

        Name = suggested;
        if (string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(formattedAddress))
        {
            Address = formattedAddress.Trim();
        }

        RefreshMapMarker();
        Logger.Information("Pickup stop name suggested Name={Name}", Name);
        return true;
    }

    public async Task SuggestNameFromMapAsync()
    {
        if (!string.IsNullOrWhiteSpace(Name) || !HasMapPick)
        {
            return;
        }

        if (TrySuggestName(null, Address))
        {
            return;
        }

        var maps = _mapsGeo ?? App.ServiceProvider?.GetService<IMapsGeoService>();
        if (maps is null || !maps.IsConfigured)
        {
            return;
        }

        var seq = System.Threading.Interlocked.Increment(ref _nameSuggestSeq);
        MapsGeocodeResult result;
        try
        {
            result = await maps.ReverseGeocodeAsync(LatitudeValue, LongitudeValue).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup stop reverse geocode failed");
            return;
        }

        if (seq != _nameSuggestSeq)
        {
            return;
        }

        if (!result.Ok)
        {
            return;
        }

        TrySuggestName(result.Street, result.FormattedAddress);
    }

    private void ClearMapPick()
    {
        _latitudeValue = 0;
        _longitudeValue = 0;
        HasMapPick = false;
        MapMarkers.Clear();
        OnPropertyChanged(nameof(LatitudeValue));
        OnPropertyChanged(nameof(LongitudeValue));
        MapHint = "Click the map to pin the pickup stop (corner or block meeting point).";
    }

    private void RefreshMapMarker()
    {
        MapMarkers.Clear();
        if (!HasMapPick)
        {
            return;
        }

        var label = MapMarkerLabels.ForPickup(string.IsNullOrWhiteSpace(Name) ? "Pickup stop" : Name.Trim());
        MapMarkers.Add(MapMarker.FromDegrees(
            _latitudeValue,
            _longitudeValue,
            label,
            MapMarkerLabels.Kind.Pickup));
    }

    private async Task SaveAsync()
    {
        ValidationMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            TrySuggestName(null, Address);
        }

        if (string.IsNullOrWhiteSpace(Name) && HasMapPick)
        {
            await SuggestNameFromMapAsync().ConfigureAwait(true);
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "Stop name is required. Pin the map or pick a Google address so we can suggest one, or type a name (for example Oak & 4th).";
            return;
        }

        if (!HasMapPick || Math.Abs(LatitudeValue) < 0.0001 && Math.Abs(LongitudeValue) < 0.0001)
        {
            ValidationMessage = "Pin the stop on the map or enter latitude and longitude.";
            return;
        }

        try
        {
            var stop = await _pickupStops.AddStopAsync(
                Name.Trim(),
                string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                (decimal)LatitudeValue,
                (decimal)LongitudeValue,
                SelectedStopType,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()).ConfigureAwait(true);

            SavedPickupStopId = stop.PickupStopId;
            NearbyHomePickupHint = await BuildNearbyHomeHintAsync().ConfigureAwait(true);
            Logger.Information("Pickup stop saved PickupStopId={Id} Name={Name}", stop.PickupStopId, stop.Name);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup stop save failed");
            ValidationMessage = ex.Message;
        }
    }

    private async Task<string> BuildNearbyHomeHintAsync()
    {
        var students = _students ?? App.ServiceProvider?.GetService<IStudentService>();
        if (students is null)
        {
            return string.Empty;
        }

        var maxMeters = DistrictCameraUi.CurrentSettings()?.StopSuggestMaxMeters ?? 400;
        var nearby = await students.GetNearbyHomePickupStudentsAsync(
                LatitudeValue,
                LongitudeValue,
                maxMeters)
            .ConfigureAwait(true);
        return StudentPickupHint.NewCatalogNearbyHomes(nearby?.Count ?? 0, maxMeters);
    }
}
