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

    public StudentHomePinWindow(StudentHomePinViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        _vm.MapMarkers.CollectionChanged += OnPickMarkersChanged;
        HomePickMap.SizeChanged += OnPickMapSizeChanged;

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

                HomePickLayer.Center = _vm.MapCenter;
                HomePickMap.ZoomLevel = (int)_vm.MapZoomLevel;
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
        if (MapMarkerHost.TryAssignAndLayout(HomePickMap, HomePickLayer, _vm.MapMarkers))
        {
            _markerRetry?.Stop();
            return;
        }

        _markerRetry ??= new MapMarkerHost.RetryScheduler(
            Dispatcher,
            () => MapMarkerHost.TryAssignAndLayout(HomePickMap, HomePickLayer, _vm.MapMarkers),
            retries => Logger.Warning("Home pick markers still pending after {Retries} host retries", retries));
        _markerRetry.Arm();
    }

    private void HomePickMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (HomePickLayer is null)
            {
                return;
            }

            var pos = e.GetPosition(HomePickMap);
            var geo = HomePickLayer.GetLatLonFromPoint(pos);
            var lon = geo.X;
            var lat = geo.Y;
            if (lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                Logger.Warning("Ignored out-of-range home pick Lat={Lat} Lon={Lon}", lat, lon);
                return;
            }

            _vm.ApplyMapClick(lat, lon);
            HomePickLayer.Center = new Point(lat, lon);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Home pickup map pick failed");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.MapMarkers.CollectionChanged -= OnPickMarkersChanged;
        HomePickMap.SizeChanged -= OnPickMapSizeChanged;
        _markerRetry?.Stop();
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
