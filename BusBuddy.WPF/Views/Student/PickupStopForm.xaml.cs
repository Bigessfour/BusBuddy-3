using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BusBuddy.Core.Models;
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

public partial class PickupStopForm : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<PickupStopForm>();
    private readonly PickupStopFormViewModel _vm;
    private MapMarkerHost.RetryScheduler? _markerRetry;

    public PickupStopForm(PickupStopFormViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        StopTypeCombo.SelectedItem = _vm.SelectedStopType;
        StopLatBox.Value = _vm.LatitudeValue;
        StopLonBox.Value = _vm.LongitudeValue;

        _vm.MapMarkers.CollectionChanged += OnPickMarkersChanged;
        StopPickMap.SizeChanged += OnPickMapSizeChanged;

        _vm.RequestClose += (_, result) =>
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
                // Not shown as dialog
            }

            Close();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PickupStopFormViewModel.LatitudeValue)
                or nameof(PickupStopFormViewModel.LongitudeValue))
            {
                if (Math.Abs((StopLatBox.Value ?? 0d) - _vm.LatitudeValue) > 0.0000001)
                {
                    StopLatBox.Value = _vm.LatitudeValue;
                }

                if (Math.Abs((StopLonBox.Value ?? 0d) - _vm.LongitudeValue) > 0.0000001)
                {
                    StopLonBox.Value = _vm.LongitudeValue;
                }
            }
        };

        Loaded += async (_, _) =>
        {
            SaveStopButton.IsEnabled = true;
            if (StopPickLayer is not null)
            {
                var ok = await MapTileBootstrap.TryApplyGoogleTilesAsync(
                    StopPickLayer,
                    StopPickAttribution,
                    StopPickAttributionText,
                    StopPickMap,
                    App.ServiceProvider,
                    StopPickGoogleLogo,
                    host: "StopPick").ConfigureAwait(true);
                if (ok)
                {
                    _ = await MapTileBootstrap.RefreshGoogleAttributionAsync(
                        StopPickLayer,
                        StopPickAttributionText,
                        StopPickMap,
                        App.ServiceProvider).ConfigureAwait(true);
                }
            }

            _ = Dispatcher.BeginInvoke(AssignPickMarkers, DispatcherPriority.Loaded);
            _ = Dispatcher.BeginInvoke(AssignPickMarkers, DispatcherPriority.ContextIdle);
        };
    }

    private void OnPickMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _ = Dispatcher.BeginInvoke(AssignPickMarkers, DispatcherPriority.Loaded);

    private void OnPickMapSizeChanged(object sender, SizeChangedEventArgs e) => AssignPickMarkers();

    private void AssignPickMarkers()
    {
        if (MapMarkerHost.TryAssignAndLayout(StopPickMap, StopPickLayer, _vm.MapMarkers))
        {
            _markerRetry?.Stop();
            return;
        }

        _markerRetry ??= new MapMarkerHost.RetryScheduler(
            Dispatcher,
            () => MapMarkerHost.TryAssignAndLayout(StopPickMap, StopPickLayer, _vm.MapMarkers),
            retries => Logger.Warning("Pickup stop pick markers still pending after {Retries} host retries", retries));
        _markerRetry.Arm();
    }

    private async void SaveStopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PushFieldsToViewModel();
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
            Logger.Warning(ex, "Save pickup stop click failed");
            _vm.ValidationMessage = ex.Message;
        }
    }

    private void PushFieldsToViewModel()
    {
        _vm.Name = StopNameBox.Text?.Trim() ?? string.Empty;
        _vm.Address = StopAddressBox.AddressText?.Trim() ?? string.Empty;
        _vm.SelectedStopType = StopTypeCombo.SelectedItem as string ?? PickupStopTypes.Corner;
        _vm.Notes = StopNotesBox.Text?.Trim() ?? string.Empty;
        _vm.LatitudeValue = StopLatBox.Value ?? 0d;
        _vm.LongitudeValue = StopLonBox.Value ?? 0d;
        if (Math.Abs(_vm.LatitudeValue) > 0.0001 || Math.Abs(_vm.LongitudeValue) > 0.0001)
        {
            _vm.ApplyMapClick(_vm.LatitudeValue, _vm.LongitudeValue);
        }
    }

    private void StopAddress_Applied(object sender, PlaceAddressAppliedEventArgs e)
    {
        if (!e.Applied.Latitude.HasValue || !e.Applied.Longitude.HasValue)
        {
            return;
        }

        _vm.ApplyMapClick(e.Applied.Latitude.Value, e.Applied.Longitude.Value);
        StopLatBox.Value = _vm.LatitudeValue;
        StopLonBox.Value = _vm.LongitudeValue;
        if (StopPickLayer is not null)
        {
            StopPickLayer.Center = new Point(_vm.LatitudeValue, _vm.LongitudeValue);
        }
    }

    private void PickupStopForm_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        NumpadInputHelper.HandlePreviewKeyDown(e);
    }

    private void StopPickMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (StopPickLayer is null)
            {
                return;
            }

            var pos = e.GetPosition(StopPickMap);
            var geo = StopPickLayer.GetLatLonFromPoint(pos);
            var lon = geo.X;
            var lat = geo.Y;
            if (lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                return;
            }

            _vm.ApplyMapClick(lat, lon);
            StopLatBox.Value = _vm.LatitudeValue;
            StopLonBox.Value = _vm.LongitudeValue;
            StopPickLayer.Center = new Point(lat, lon);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup stop map pick failed");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.MapMarkers.CollectionChanged -= OnPickMarkersChanged;
        StopPickMap.SizeChanged -= OnPickMapSizeChanged;
        _markerRetry?.Stop();
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
