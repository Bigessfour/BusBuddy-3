using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class StudentHomePinWindow : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<StudentHomePinWindow>();
    private readonly StudentHomePinViewModel _vm;
    private MapMarkerHost.RetryScheduler? _markerRetry;
    private Point? _pickMouseDown;
    private bool _cameraApplied;

    public StudentHomePinWindow(StudentHomePinViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        _vm.MapMarkers.CollectionChanged += OnPickMarkersChanged;

        _vm.RequestClose += (_, result) =>
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
            }

            Close();
        };

        Loaded += async (_, _) =>
        {
            if (HomePickLayer is not null)
            {
                var ok = await MapTileBootstrap.TryApplyGoogleTilesAsync(
                    HomePickLayer,
                    HomePickAttribution,
                    HomePickAttributionText,
                    HomePickMap,
                    App.ServiceProvider,
                    HomePickGoogleLogo,
                    host: "HomePick").ConfigureAwait(true);
                if (ok)
                {
                    _ = await MapTileBootstrap.RefreshGoogleAttributionAsync(
                        HomePickLayer,
                        HomePickAttributionText,
                        HomePickMap,
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
            if (!MapCameraHost.TryApply(HomePickMap, HomePickLayer, _vm.MapCenter, (int)_vm.MapZoomLevel))
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
        if (MapMarkerHost.TryAssignAndLayout(HomePickMap, HomePickLayer, _vm.MapMarkers))
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
                    if (!MapCameraHost.TryApply(HomePickMap, HomePickLayer, _vm.MapCenter, (int)_vm.MapZoomLevel))
                    {
                        return false;
                    }

                    _cameraApplied = true;
                }

                return MapMarkerHost.TryAssignAndLayout(HomePickMap, HomePickLayer, _vm.MapMarkers);
            },
            retries => Logger.Warning("Home pick markers still pending after {Retries} host retries", retries));
        _markerRetry.Arm();
    }

    private void HomePickMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (HomePickLayer is not null)
        {
            _pickMouseDown = e.GetPosition(HomePickLayer);
        }
    }

    private void HomePickMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (HomePickLayer is null)
            {
                return;
            }

            var up = e.GetPosition(HomePickLayer);
            var down = _pickMouseDown ?? up;
            _pickMouseDown = null;
            if (!MapCameraHost.TryReadClick(HomePickLayer, down, up, out var lat, out var lon))
            {
                return;
            }

            _vm.ApplyMapClick(lat, lon);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Home pickup map pick failed");
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
