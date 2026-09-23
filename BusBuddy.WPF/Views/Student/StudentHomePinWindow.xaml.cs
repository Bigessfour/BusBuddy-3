using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Maps;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class StudentHomePinWindow : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<StudentHomePinWindow>();
    private readonly StudentHomePinViewModel _vm;
    private MapMarkerHost.RetryScheduler? _markerRetry;
    private Point? _pickMouseDown;
    private bool _cameraApplied;
    private bool _restoringCenter;
    private bool _cameraSettled;
    private int _oceanRestores;

    public StudentHomePinWindow(StudentHomePinViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        _vm.MapMarkers.CollectionChanged += OnPickMarkersChanged;
        HomePickLayer.CenterChanged += OnHomePickCenterChanged;

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

            TryApplyCameraThenMarkers();
            _ = Dispatcher.BeginInvoke(TryApplyCameraThenMarkers, DispatcherPriority.Loaded);
            _ = Dispatcher.BeginInvoke(TryApplyCameraThenMarkers, DispatcherPriority.ContextIdle);
        };
    }

    private DataTemplateSelector? PickMarkerTemplates =>
        TryFindResource("DistrictMarkerTemplateSelector") as DataTemplateSelector;

    /// <summary>
    /// ZoomMap can pan the imagery to 0,0 (featureless blue ocean) after the pickup center is set.
    /// Pull that jump back while the window is opening. A later clerk pan is left alone.
    /// </summary>
    private void OnHomePickCenterChanged(object? sender, CenterChangedEventArgs e)
    {
        if (_cameraSettled || _restoringCenter || HomePickLayer is null)
        {
            return;
        }

        var (lat, lon) = MapCameraHost.ToLatLon(HomePickLayer.Center);
        if (Math.Abs(lat - _vm.MapCenter.X) < 0.01 && Math.Abs(lon - _vm.MapCenter.Y) < 0.01)
        {
            return;
        }

        var openedOnOcean = Math.Abs(_vm.MapCenter.X) < 1 && Math.Abs(_vm.MapCenter.Y) < 1;
        var jumpedToOcean = Math.Abs(lat) < 1 && Math.Abs(lon) < 1;
        if (openedOnOcean || !jumpedToOcean || _oceanRestores >= 4)
        {
            _cameraSettled = true;
            return;
        }

        _oceanRestores++;
        _restoringCenter = true;
        try
        {
            HomePickLayer.Center = _vm.MapCenter;
        }
        finally
        {
            _restoringCenter = false;
        }
    }

    private void OnPickMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _ = Dispatcher.BeginInvoke(AssignPickMarkers, DispatcherPriority.Loaded);

    private void TryApplyCameraThenMarkers()
    {
        if (!_cameraApplied)
        {
            if (!TryApplyOpeningCamera())
            {
                ArmMarkerRetry();
                return;
            }

            _cameraApplied = true;
            var settle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            settle.Tick += (_, _) =>
            {
                settle.Stop();
                _cameraSettled = true;
            };
            settle.Start();
        }

        AssignPickMarkers();
    }

    private bool TryApplyOpeningCamera()
    {
        if (!MapCameraHost.TryApply(HomePickMap, HomePickLayer, _vm.MapCenter, (int)_vm.MapZoomLevel)
            || HomePickLayer is null)
        {
            return false;
        }

        var (lat, lon) = MapCameraHost.ToLatLon(HomePickLayer.Center);
        return Math.Abs(lat - _vm.MapCenter.X) < 0.01
            && Math.Abs(lon - _vm.MapCenter.Y) < 0.01;
    }

    private void AssignPickMarkers()
    {
        if (MapMarkerHost.TryAssignAndLayout(
                HomePickMap,
                HomePickLayer,
                _vm.MapMarkers,
                PickMarkerTemplates))
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
                    if (!TryApplyOpeningCamera())
                    {
                        return false;
                    }

                    _cameraApplied = true;
                }

                return MapMarkerHost.TryAssignAndLayout(
                    HomePickMap,
                    HomePickLayer,
                    _vm.MapMarkers,
                    PickMarkerTemplates);
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
        HomePickLayer.CenterChanged -= OnHomePickCenterChanged;
        _vm.MapMarkers.CollectionChanged -= OnPickMarkersChanged;
        _markerRetry?.Stop();
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
