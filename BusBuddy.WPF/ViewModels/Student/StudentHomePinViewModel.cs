using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Map;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Click-to-nudge the boarding pin after Address Validation. Address text stays on the form;
/// this window only writes lat/lng. Gold pin = Google's geocode of that address; blue pin =
/// the clerk pickup (driveway / gate). specs/students.md: coordinates come from geocode + map, not typed boxes.
/// </summary>
public sealed class StudentHomePinViewModel : INotifyPropertyChanged
{
    private double _latitudeValue;
    private double _longitudeValue;
    private readonly double? _validatedLatitude;
    private readonly double? _validatedLongitude;
    private readonly PickupStop? _catalogStop;
    private bool _hasMapPick;
    private string _mapHint = "Click the driveway or gate. Do not type coordinates.";

    public StudentHomePinViewModel(
        StudentModel student,
        (double Latitude, double Longitude)? validatedAddress = null,
        PickupStop? catalogStop = null)
    {
        ArgumentNullException.ThrowIfNull(student);
        MapMarkers = new ObservableCollection<MapMarker>();
        ConfirmCommand = new RelayCommand(Confirm, () => HasMapPick);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));

        _catalogStop = catalogStop is { HasValidatedCoordinates: true } ? catalogStop : null;

        if (validatedAddress is { } google
            && LocationCoordinate.IsValidated((decimal)google.Latitude, (decimal)google.Longitude))
        {
            _validatedLatitude = google.Latitude;
            _validatedLongitude = google.Longitude;
        }

        if (student.HasValidatedHomeCoordinates)
        {
            ApplyMapClick((double)student.Latitude!, (double)student.Longitude!);
        }
        else if (_validatedLatitude is { } lat && _validatedLongitude is { } lon)
        {
            ApplyMapClick(lat, lon);
            MapHint = "Google placed this pin from the street address. Click the map to move pickup to the driveway or gate.";
        }
        else
        {
            MapCenter = new Point(38.0872, -102.6208);
            MapZoomLevel = MapDefaults.SchoolZoomLevel;
        }

        if (HasValidatedAddressPin && HasMapPick && !SameAsValidated(_latitudeValue, _longitudeValue))
        {
            MapHint = "Gold is the validated street address. Blue is the pickup. Click the map to move the blue pin.";
        }
        else if (HasValidatedAddressPin && HasMapPick)
        {
            MapHint = "Google placed this pin from Address Validation. Click the map to move it to the real pickup.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<bool>? RequestClose;

    public ObservableCollection<MapMarker> MapMarkers { get; }
    public Point MapCenter { get; private set; }
    public double MapZoomLevel { get; private set; } = 16;
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public double LatitudeValue => _latitudeValue;
    public double LongitudeValue => _longitudeValue;
    public bool HasValidatedAddressPin => _validatedLatitude is not null && _validatedLongitude is not null;

    public bool HasMapPick
    {
        get => _hasMapPick;
        private set
        {
            if (_hasMapPick == value)
            {
                return;
            }

            _hasMapPick = value;
            OnPropertyChanged();
            (ConfirmCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string MapHint
    {
        get => _mapHint;
        private set
        {
            if (_mapHint == value)
            {
                return;
            }

            _mapHint = value;
            OnPropertyChanged();
        }
    }

    public void ApplyMapClick(double latitude, double longitude)
    {
        if (!DistrictMapAnchor.IsValidLatitude(latitude) || !DistrictMapAnchor.IsValidLongitude(longitude))
        {
            return;
        }

        if (!LocationCoordinate.IsValidated((decimal)latitude, (decimal)longitude))
        {
            return;
        }

        _latitudeValue = Math.Round(latitude, 6);
        _longitudeValue = Math.Round(longitude, 6);
        HasMapPick = true;
        MapCenter = new Point(_latitudeValue, _longitudeValue);
        MapZoomLevel = 16;
        MapHint = SameAsValidated(_latitudeValue, _longitudeValue)
            ? $"Pickup matches the validated address ({_latitudeValue:F5}, {_longitudeValue:F5}). Click to nudge the driveway."
            : $"Pickup pin {_latitudeValue:F5}, {_longitudeValue:F5}. Confirm writes this point; the street address is unchanged.";
        RefreshMarkers();
        OnPropertyChanged(nameof(MapCenter));
        OnPropertyChanged(nameof(MapZoomLevel));
        OnPropertyChanged(nameof(LatitudeValue));
        OnPropertyChanged(nameof(LongitudeValue));
    }

    private bool SameAsValidated(double latitude, double longitude) =>
        HasValidatedAddressPin
        && StudentPlotLocation.SameSpot(latitude, longitude, _validatedLatitude!.Value, _validatedLongitude!.Value);

    private void Confirm()
    {
        if (!HasMapPick)
        {
            return;
        }

        RequestClose?.Invoke(this, true);
    }

    private void RefreshMarkers()
    {
        MapMarkers.Clear();
        if (HasValidatedAddressPin
            && !StudentPlotLocation.SameSpot(
                _latitudeValue,
                _longitudeValue,
                _validatedLatitude!.Value,
                _validatedLongitude!.Value))
        {
            MapMarkers.Add(MapMarker.FromDegrees(
                _validatedLatitude.Value,
                _validatedLongitude.Value,
                "WP Validated address",
                MapMarkerLabels.Kind.Waypoint));
        }

        if (_catalogStop is { HasValidatedCoordinates: true })
        {
            MapMarkers.Add(MapMarker.FromDegrees(
                (double)_catalogStop.Latitude,
                (double)_catalogStop.Longitude,
                MapMarkerLabels.ForPickup(_catalogStop.Name),
                MapMarkerLabels.Kind.Pickup));
        }

        if (HasMapPick)
        {
            MapMarkers.Add(MapMarker.FromDegrees(
                _latitudeValue,
                _longitudeValue,
                MapMarkerLabels.ForHome("Pickup"),
                MapMarkerLabels.Kind.Home));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
