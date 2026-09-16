using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

public sealed class PickupStopFormViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<PickupStopFormViewModel>();

    public const int DefaultMapZoom = MapDefaults.SchoolZoomLevel;

    private readonly IPickupStopService _pickupStops;
    private readonly int? _editingStopId;

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

    public PickupStopFormViewModel(IPickupStopService pickupStops)
        : this(pickupStops, existing: null)
    {
    }

    /// <summary>Opens the form on an existing catalog stop.</summary>
    public static PickupStopFormViewModel ForEdit(IPickupStopService pickupStops, PickupStop stop) =>
        new(pickupStops, stop ?? throw new ArgumentNullException(nameof(stop)));

    private PickupStopFormViewModel(IPickupStopService pickupStops, PickupStop? existing)
    {
        _pickupStops = pickupStops ?? throw new ArgumentNullException(nameof(pickupStops));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ClearMapPickCommand = new RelayCommand(ClearMapPick);

        StopTypeOptions = new ObservableCollection<string>(PickupStopTypes.All);
        MapMarkers = new ObservableCollection<MapMarker>();
        var camera = DistrictCameraUi.Resolve();
        MapCenter = new Point(camera.Latitude, camera.Longitude);
        MapZoomLevel = camera.ZoomLevel;

        if (existing is not null)
        {
            _editingStopId = existing.PickupStopId;
            IsEditing = true;
            Name = existing.Name;
            Address = existing.Address ?? string.Empty;
            SelectedStopType = existing.StopType;
            Notes = existing.Notes ?? string.Empty;
            ApplyMapClick((double)existing.Latitude, (double)existing.Longitude);
        }
    }

    public bool IsEditing { get; }

    public string Title => IsEditing ? "Edit pickup stop" : "Add pickup stop";

    public string HeaderText => IsEditing
        ? "Edit pickup stop — correct the shared corner or block stop"
        : "Add pickup stop — shared corner or block stop for multiple students";

    public string SaveButtonLabel => IsEditing ? "Save changes" : "Save pickup stop";

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
        MapCenter = new Point(_latitudeValue, _longitudeValue);
        OnPropertyChanged(nameof(MapCenter));
    }

    private async Task SaveAsync()
    {
        ValidationMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "Stop name is required (e.g. Oak & 4th).";
            return;
        }

        if (!HasMapPick || Math.Abs(LatitudeValue) < 0.0001 && Math.Abs(LongitudeValue) < 0.0001)
        {
            ValidationMessage = "Pin the stop on the map or enter latitude and longitude.";
            return;
        }

        try
        {
            var name = Name.Trim();
            var address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim();
            var latitude = (decimal)LatitudeValue;
            var longitude = (decimal)LongitudeValue;
            var notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
            var stop = IsEditing
                ? await _pickupStops.UpdateStopAsync(
                    _editingStopId!.Value,
                    name,
                    address,
                    latitude,
                    longitude,
                    SelectedStopType,
                    notes).ConfigureAwait(true)
                : await _pickupStops.AddStopAsync(
                    name,
                    address,
                    latitude,
                    longitude,
                    SelectedStopType,
                    notes).ConfigureAwait(true);

            SavedPickupStopId = stop.PickupStopId;
            Logger.Information("Pickup stop saved PickupStopId={Id} Name={Name} Editing={Editing}",
                stop.PickupStopId, stop.Name, IsEditing);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup stop save failed");
            ValidationMessage = ex.Message;
        }
    }
}
