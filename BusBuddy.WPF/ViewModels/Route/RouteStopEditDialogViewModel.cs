using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BusBuddy.WPF.Utilities;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// DataContext for <c>RouteStopEditDialog</c> and its nested <c>PlacesAddressBox</c>.
/// Stops only save after Places apply writes coordinates.
/// </summary>
public partial class RouteStopEditDialogViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<RouteStopEditDialogViewModel>();

    public RouteStopEditDialogViewModel(string? stopName = null, string? stopAddress = null, decimal? latitude = null, decimal? longitude = null)
    {
        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(Cancel);
        StopName = stopName ?? string.Empty;
        StopAddress = stopAddress ?? string.Empty;
        Latitude = latitude;
        Longitude = longitude;
        RefreshCoordinateStatus();
        RefreshSaveToolTip();
    }

    public event Action<bool>? CloseRequested;

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    [ObservableProperty]
    private string stopName = string.Empty;

    [ObservableProperty]
    private string stopAddress = string.Empty;

    [ObservableProperty]
    private decimal? latitude;

    [ObservableProperty]
    private decimal? longitude;

    [ObservableProperty]
    private string coordinateStatus = string.Empty;

    [ObservableProperty]
    private string saveToolTip = string.Empty;

    public bool HasValidatedCoordinates => Latitude.HasValue && Longitude.HasValue;

    partial void OnStopNameChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        RefreshSaveToolTip();
    }

    partial void OnLatitudeChanged(decimal? value)
    {
        RefreshCoordinateStatus();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnLongitudeChanged(decimal? value)
    {
        RefreshCoordinateStatus();
        SaveCommand.NotifyCanExecuteChanged();
    }

    public void ApplyAddress(PlaceAddressApplier.AppliedAddress applied)
    {
        StopAddress = applied.SingleLine();
        Latitude = applied.Latitude.HasValue ? (decimal)applied.Latitude.Value : null;
        Longitude = applied.Longitude.HasValue ? (decimal)applied.Longitude.Value : null;
        Logger.Information(
            "Route stop address applied HasCoordinates={HasCoordinates}",
            HasValidatedCoordinates);
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(StopName) && HasValidatedCoordinates;

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(StopName))
        {
            Logger.Warning("Route stop save blocked — empty stop name");
            MessageBox.Show("Stop name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!HasValidatedCoordinates)
        {
            Logger.Warning("Route stop save blocked — no validated coordinates for {StopName}", StopName.Trim());
            MessageBox.Show(
                "Choose the address from the suggestion list so the stop gets map coordinates.\n\n"
                    + "A stop without validated coordinates cannot be added to a route.",
                "Address not validated",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Logger.Information(
            "Route stop saved Name={StopName} Address={Address} Lat={Latitude} Lon={Longitude}",
            StopName.Trim(),
            StopAddress.Trim(),
            Latitude,
            Longitude);
        CloseRequested?.Invoke(true);
    }

    private void Cancel()
    {
        Logger.Information("Route stop edit cancelled");
        CloseRequested?.Invoke(false);
    }

    private void RefreshCoordinateStatus()
    {
        CoordinateStatus = HasValidatedCoordinates
            ? $"Located at {Latitude!.Value:0.#####}, {Longitude!.Value:0.#####}"
            : "Pick the address from the suggestion list so the stop can be mapped.";
        RefreshSaveToolTip();
    }

    private void RefreshSaveToolTip()
    {
        if (string.IsNullOrWhiteSpace(StopName))
        {
            SaveToolTip = "Enter a stop name.";
            return;
        }

        SaveToolTip = HasValidatedCoordinates
            ? "Save this stop with validated map coordinates."
            : "Choose the address from the Places suggestion list to enable Save.";
    }
}
