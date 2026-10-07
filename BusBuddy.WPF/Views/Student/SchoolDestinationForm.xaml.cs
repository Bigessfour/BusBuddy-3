using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BusBuddy.WPF.Controls;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Maps;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class SchoolDestinationForm : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<SchoolDestinationForm>();
    private readonly SchoolDestinationFormViewModel _vm;
    private MapMarkerHost.RetryScheduler? _markerRetry;
    private Point? _pickMouseDown;
    private bool _cameraApplied;

    public SchoolDestinationForm(SchoolDestinationFormViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        // Seed controls from the VM — do not rely on Text DP bindings (they were not updating the VM).
        // In times-only edit mode the VM arrives prefilled from the campus being corrected.
        SchoolNameBox.Text = _vm.Name;
        SchoolAddressBox.AddressText = _vm.Address;
        SchoolCityBox.Text = _vm.City;
        SchoolStateBox.Text = _vm.State;
        SchoolZipBox.Text = _vm.ZipCode;
        SchoolStartPicker.Value = SchoolDestinationFormViewModel.ParseTimePickerValue(
            _vm.StartTimeText,
            new TimeSpan(8, 0, 0));
        SchoolDismissalPicker.Value = SchoolDestinationFormViewModel.ParseTimePickerValue(
            _vm.DismissalTimeText,
            new TimeSpan(15, 30, 0));
        SchoolLatBox.Value = _vm.LatitudeValue;
        SchoolLonBox.Value = _vm.LongitudeValue;

        _vm.MapMarkers.CollectionChanged += OnPickMarkersChanged;

        _vm.RequestClose += (_, result) =>
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
                // Not shown as dialog — still close
            }

            Close();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SchoolDestinationFormViewModel.LatitudeValue)
                or nameof(SchoolDestinationFormViewModel.LongitudeValue)
                or nameof(SchoolDestinationFormViewModel.HasMapPick))
            {
                var boxLat = SchoolLatBox.Value ?? 0d;
                var boxLon = SchoolLonBox.Value ?? 0d;
                if (Math.Abs(boxLat - _vm.LatitudeValue) > 0.0000001)
                {
                    SchoolLatBox.Value = _vm.LatitudeValue;
                }

                if (Math.Abs(boxLon - _vm.LongitudeValue) > 0.0000001)
                {
                    SchoolLonBox.Value = _vm.LongitudeValue;
                }
            }
        };

        Loaded += async (_, _) =>
        {
            SaveSchoolButton.IsEnabled = true;
            if (SchoolPickLayer is not null)
            {
                var ok = await MapTileBootstrap.TryApplyGoogleTilesAsync(
                    SchoolPickLayer,
                    SchoolPickAttribution,
                    SchoolPickAttributionText,
                    SchoolPickMap,
                    App.ServiceProvider,
                    SchoolPickGoogleLogo,
                    host: "SchoolPick").ConfigureAwait(true);
                if (ok)
                {
                    _ = await MapTileBootstrap.RefreshGoogleAttributionAsync(
                        SchoolPickLayer,
                        SchoolPickAttributionText,
                        SchoolPickMap,
                        App.ServiceProvider).ConfigureAwait(true);
                }
            }

            _ = Dispatcher.BeginInvoke(TryApplyCameraThenMarkers, DispatcherPriority.Loaded);
            _ = Dispatcher.BeginInvoke(TryApplyCameraThenMarkers, DispatcherPriority.ContextIdle);
        };
    }

    private void OnPickMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _ = Dispatcher.BeginInvoke(AssignPickMarkers, DispatcherPriority.Loaded);

    private void TryApplyCameraThenMarkers()
    {
        if (!_cameraApplied)
        {
            if (!MapCameraHost.TryApply(SchoolPickMap, SchoolPickLayer, _vm.MapCenter, _vm.MapZoomLevel))
            {
                ArmMarkerRetry();
                return;
            }

            _cameraApplied = true;
        }

        AssignPickMarkers();
    }

    private void AssignPickMarkers()
    {
        if (MapMarkerHost.TryAssignAndLayout(SchoolPickMap, SchoolPickLayer, _vm.MapMarkers))
        {
            _markerRetry?.Stop();
            return;
        }

        ArmMarkerRetry();
    }

    private void ArmMarkerRetry()
    {
        _markerRetry ??= new MapMarkerHost.RetryScheduler(
            Dispatcher,
            () =>
            {
                if (!_cameraApplied)
                {
                    if (!MapCameraHost.TryApply(SchoolPickMap, SchoolPickLayer, _vm.MapCenter, _vm.MapZoomLevel))
                    {
                        return false;
                    }

                    _cameraApplied = true;
                }

                return MapMarkerHost.TryAssignAndLayout(SchoolPickMap, SchoolPickLayer, _vm.MapMarkers);
            },
            retries => Logger.Warning("School pick markers still pending after {Retries} host retries", retries));
        _markerRetry.Arm();
    }

    /// <summary>
    /// Copy control text into the VM, then run save. Syncfusion Text bindings were leaving the VM empty
    /// while the UI still showed typed characters.
    /// </summary>
    private async void SaveSchoolButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PushFieldsToViewModel();
            Logger.Information(
                "Save school UI flush Name='{Name}' Address='{Address}' City='{City}' State='{State}' Zip='{Zip}' Start='{Start}' Dismissal='{Dismissal}' Lat={Lat} Lon={Lon}",
                _vm.Name, _vm.Address, _vm.City, _vm.State, _vm.ZipCode, _vm.StartTimeText, _vm.DismissalTimeText,
                _vm.LatitudeValue, _vm.LongitudeValue);

            if (_vm.SaveCommand is IAsyncRelayCommand asyncCmd)
            {
                await asyncCmd.ExecuteAsync(null).ConfigureAwait(true);
            }
            else if (_vm.SaveCommand.CanExecute(null))
            {
                _vm.SaveCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Save school click failed");
            _vm.ValidationMessage = ex.Message;
        }
    }

    private void PushAddressFieldsToViewModel()
    {
        _vm.Address = SchoolAddressBox.AddressText?.Trim() ?? string.Empty;
        _vm.City = SchoolCityBox.Text?.Trim() ?? string.Empty;
        _vm.State = SchoolStateBox.Text?.Trim() ?? string.Empty;
        _vm.ZipCode = SchoolZipBox.Text?.Trim() ?? string.Empty;
    }

    private async void ValidateSchoolAddressButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PushAddressFieldsToViewModel();
            if (_vm.ValidateAddressCommand is IAsyncRelayCommand asyncCmd)
            {
                await asyncCmd.ExecuteAsync(null).ConfigureAwait(true);
            }
            else if (_vm.ValidateAddressCommand.CanExecute(null))
            {
                _vm.ValidateAddressCommand.Execute(null);
            }

            SchoolLatBox.Value = _vm.LatitudeValue;
            SchoolLonBox.Value = _vm.LongitudeValue;
            if (_vm.HasMapPick)
            {
                MapCameraHost.TryApply(
                    SchoolPickMap,
                    SchoolPickLayer,
                    MapCameraHost.FromLatLon(_vm.LatitudeValue, _vm.LongitudeValue),
                    _vm.MapZoomLevel);
                _cameraApplied = true;
                AssignPickMarkers();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Validate school address click failed");
            _vm.ValidationMessage = ex.Message;
        }
    }

    private void PushFieldsToViewModel()
    {
        if (_vm.IsTimesOnlyEdit)
        {
            _vm.StartTimeText = SchoolDestinationFormViewModel.FormatTimeText(SchoolStartPicker.Value, string.Empty);
            _vm.DismissalTimeText = SchoolDestinationFormViewModel.FormatTimeText(SchoolDismissalPicker.Value, string.Empty);
            return;
        }

        _vm.Name = SchoolNameBox.Text?.Trim() ?? string.Empty;
        PushAddressFieldsToViewModel();
        _vm.StartTimeText = SchoolDestinationFormViewModel.FormatTimeText(SchoolStartPicker.Value, string.Empty);
        _vm.DismissalTimeText = SchoolDestinationFormViewModel.FormatTimeText(SchoolDismissalPicker.Value, string.Empty);
        _vm.LatitudeValue = SchoolLatBox.Value ?? 0d;
        _vm.LongitudeValue = SchoolLonBox.Value ?? 0d;
        if (Math.Abs(_vm.LatitudeValue) > 0.0001 || Math.Abs(_vm.LongitudeValue) > 0.0001)
        {
            _vm.ApplyMapClick(_vm.LatitudeValue, _vm.LongitudeValue);
        }
    }

    private void SchoolAddress_Applied(object sender, PlaceAddressAppliedEventArgs e)
    {
        _vm.ApplyAppliedAddress(e.Applied);
        SchoolAddressBox.AddressText = _vm.Address;
        SchoolCityBox.Text = _vm.City;
        SchoolStateBox.Text = _vm.State;
        SchoolZipBox.Text = _vm.ZipCode;
        SchoolLatBox.Value = _vm.LatitudeValue;
        SchoolLonBox.Value = _vm.LongitudeValue;
        if (_vm.HasMapPick)
        {
            MapCameraHost.TryApply(
                SchoolPickMap,
                SchoolPickLayer,
                MapCameraHost.FromLatLon(_vm.LatitudeValue, _vm.LongitudeValue),
                _vm.MapZoomLevel);
            _cameraApplied = true;
            AssignPickMarkers();
        }
    }

    private void SchoolForm_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        NumpadInputHelper.HandlePreviewKeyDown(e);
    }

    private void SchoolPickMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SchoolPickLayer is not null)
        {
            _pickMouseDown = e.GetPosition(SchoolPickLayer);
        }
    }

    private void SchoolPickMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (SchoolPickLayer is null || !_vm.CanEditSchoolDetails)
            {
                return;
            }

            var up = e.GetPosition(SchoolPickLayer);
            var down = _pickMouseDown ?? up;
            _pickMouseDown = null;
            if (!MapCameraHost.TryReadClick(SchoolPickLayer, down, up, out var lat, out var lon))
            {
                return;
            }

            _vm.ApplyMapClick(lat, lon);
            SchoolLatBox.Value = _vm.LatitudeValue;
            SchoolLonBox.Value = _vm.LongitudeValue;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "School map pick failed");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.MapMarkers.CollectionChanged -= OnPickMarkersChanged;
        _markerRetry?.Stop();
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
