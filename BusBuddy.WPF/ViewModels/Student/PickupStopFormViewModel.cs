using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

public sealed class PickupStopFormViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<PickupStopFormViewModel>();

    public const double DefaultMapLatitude = SchoolDestinationFormViewModel.DefaultMapLatitude;
    public const double DefaultMapLongitude = SchoolDestinationFormViewModel.DefaultMapLongitude;
    public const int DefaultMapZoom = SchoolDestinationFormViewModel.DefaultMapZoom;

    private readonly IPickupStopService _pickupStops;

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
    {
        _pickupStops = pickupStops ?? throw new ArgumentNullException(nameof(pickupStops));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        ClearMapPickCommand = new RelayCommand(ClearMapPick);

        StopTypeOptions = new ObservableCollection<string>(PickupStopTypes.All);
        MapMarkers = new ObservableCollection<MapViewModel.MapMarker>();
        MapCenter = new Point(DefaultMapLatitude, DefaultMapLongitude);
        MapZoomLevel = DefaultMapZoom;
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

    public ObservableCollection<MapViewModel.MapMarker> MapMarkers { get; }

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

        var label = string.IsNullOrWhiteSpace(Name) ? "Pickup stop" : Name.Trim();
        MapMarkers.Add(MapViewModel.MapMarker.FromDegrees(_latitudeValue, _longitudeValue, label));
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
            var stop = await _pickupStops.AddStopAsync(
                Name.Trim(),
                string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                (decimal)LatitudeValue,
                (decimal)LongitudeValue,
                SelectedStopType,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()).ConfigureAwait(true);

            SavedPickupStopId = stop.PickupStopId;
            Logger.Information("Pickup stop saved PickupStopId={Id} Name={Name}", stop.PickupStopId, stop.Name);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup stop save failed");
            ValidationMessage = ex.Message;
        }
    }
}
