using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Printing;
using System.Threading.Tasks;
using System.Windows.Threading;
using BusBuddy.Core.Mapping;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Map;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Views.Map
{
    /// <summary>
    /// District map view — SfMap chrome only; business logic stays in <see cref="MapViewModel"/>.
    /// </summary>
    public partial class MapView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<MapView>();
        private SfMap? MapControl => FindName("GeoMap") as SfMap;
        private GoogleMapTilesImageryLayer? DistrictTilesLayer =>
            FindName("DistrictImageryLayer") as GoogleMapTilesImageryLayer;
        private static readonly TimeSpan AttributionDebounce = TimeSpan.FromMilliseconds(750);
        private bool _mapLayerInitialized;
        private MapViewModel? _boundViewModel;
        private MapLayer? _currentLayer;
        private DispatcherTimer? _attributionTimer;
        private MapMarkerHost.RetryScheduler? _markerHostRetry;
        private MapInteractionDiagnostics? _diagnostics;
        private bool _pendingCameraSync;
        private bool _pendingMarkerRefresh;
        private bool _placingMarkers;

        public MapView()
        {
            using (LogContext.PushProperty("ViewInitialization", "MapView"))
            {
                InitializeComponent();
                BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);

                try
                {
                    if (DataContext is null && App.ServiceProvider is not null)
                    {
                        var vmFromDi = App.ServiceProvider.GetService<MapViewModel>();
                        if (vmFromDi is not null)
                        {
                            DataContext = vmFromDi;
                            AttachViewModel(vmFromDi);
                            Logger.Debug("MapViewModel resolved from DI and set as DataContext");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to resolve MapViewModel from DI");
                }

                Unloaded += MapView_Unloaded;
                Loaded += MapView_Loaded;
                Loaded += MapView_ReattachDiagnostics;
                Logger.Information("MapView initialized");
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DataContextChanged += OnDataContextChanged;
        }

        private async void MapView_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MapView_Loaded;
            if (_mapLayerInitialized)
            {
                return;
            }

            try
            {
                if (DataContext is MapViewModel vm)
                {
                    AttachViewModel(vm);
                }

                ApplyDistrictImagery();

                if (MapControl is not null)
                {
                    ReportViewportSize(DataContext as MapViewModel);
                    SyncMapControlFromViewModel(DataContext as MapViewModel);
                }

                // Interaction trace (Map:InteractionDiagnostics / BUSBUDDY_MAP_DIAGNOSTICS) — attached before the
                // Google tile swap so the first tile burst and any camera error land in the breadcrumbs.
                _diagnostics ??= MapInteractionDiagnostics.TryAttach(
                    MapControl,
                    DistrictTilesLayer,
                    App.ServiceProvider?.GetService<Microsoft.Extensions.Configuration.IConfiguration>());

                ReplayRouteLineFromViewModel(DataContext as MapViewModel);
                if (DistrictTilesLayer is not null)
                {
                    // Google-only: await session UrlTemplate before any tile generation.
                    var googleTiles = await MapTileBootstrap.TryApplyGoogleTilesAsync(
                        DistrictTilesLayer,
                        FindName("MapAttribution") as Border,
                        FindName("MapAttributionText") as TextBlock,
                        MapControl,
                        App.ServiceProvider,
                        FindName("GoogleMapsLogo") as System.Windows.Controls.Image,
                        host: "DistrictMap").ConfigureAwait(true);
                    if (googleTiles)
                    {
                        ScheduleAttributionRefresh();
                    }

                    // Tile HttpClient downloads are async; inspect after a short settle.
                    var healthTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2),
                    };
                    healthTimer.Tick += (_, _) =>
                    {
                        healthTimer.Stop();
                        DistrictTilesLayer?.LogTileHealth("after-bootstrap+2s");
                    };
                    healthTimer.Start();
                }

                _mapLayerInitialized = true;
                _pendingMarkerRefresh = true;
                _ = Dispatcher.BeginInvoke(RefreshMarkersOnImageryLayer, DispatcherPriority.Loaded);
                _ = Dispatcher.BeginInvoke(RefreshMarkersOnImageryLayer, DispatcherPriority.ContextIdle);
                Logger.Information("Map layer ready — pan/zoom enabled");
            }
            catch (Exception ex)
            {
                _diagnostics?.RecordError("MapView.Loaded", ex);
                Logger.Error(ex, "Failed to initialize map on Loaded");
            }
        }

        /// <summary>Tab switches unload/reload the view; the one-shot Loaded handler above has already run by then.</summary>
        private void MapView_ReattachDiagnostics(object sender, RoutedEventArgs e)
        {
            if (!_mapLayerInitialized || _diagnostics is not null)
            {
                return;
            }

            _diagnostics = MapInteractionDiagnostics.TryAttach(
                MapControl,
                DistrictTilesLayer,
                App.ServiceProvider?.GetService<Microsoft.Extensions.Configuration.IConfiguration>());
        }

        private void MapView_Unloaded(object sender, RoutedEventArgs e)
        {
            _attributionTimer?.Stop();
            _markerHostRetry?.Stop();
            _diagnostics?.Dispose();
            _diagnostics = null;
            if (DistrictTilesLayer is ImageryLayer layer)
            {
                layer.MarkerSelected -= OnImageryMarkerSelected;
            }
            DetachViewModel(_boundViewModel);
            _boundViewModel = null;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MapViewModel oldViewModel)
            {
                DetachViewModel(oldViewModel);
            }

            if (e.NewValue is MapViewModel newViewModel)
            {
                AttachViewModel(newViewModel);
                ApplyDistrictImagery();
                ReplayRouteLineFromViewModel(newViewModel);
                RefreshMarkersOnImageryLayer();
            }
        }

        private void AttachViewModel(MapViewModel vm)
        {
            if (_boundViewModel == vm)
            {
                return;
            }

            DetachViewModel(_boundViewModel);
            _boundViewModel = vm;
            vm.ViewResetRequested += OnViewResetRequested;
            vm.RouteLineUpdated += OnRouteLineUpdated;
            vm.PrintRequested += OnPrintRequested;
            vm.CaptureSnapshotRequested += OnCaptureSnapshotRequested;
            vm.MapMarkersChanged += OnMapMarkersChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void DetachViewModel(MapViewModel? viewModel)
        {
            if (viewModel is null)
            {
                return;
            }

            viewModel.RouteLineUpdated -= OnRouteLineUpdated;
            viewModel.PrintRequested -= OnPrintRequested;
            viewModel.CaptureSnapshotRequested -= OnCaptureSnapshotRequested;
            viewModel.MapMarkersChanged -= OnMapMarkersChanged;
            viewModel.ViewResetRequested -= OnViewResetRequested;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void ApplyDistrictImagery()
        {
            try
            {
                var imagery = DistrictTilesLayer;
                if (imagery is null)
                {
                    Logger.Warning("DistrictImageryLayer not found in view");
                    return;
                }

                ConfigureImageryLayer(imagery);
                _currentLayer = imagery;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to configure district imagery layer");
            }
        }

        private void ConfigureImageryLayer(ImageryLayer imagery)
        {
            imagery.MarkerSelected -= OnImageryMarkerSelected;
            imagery.MarkerSelected += OnImageryMarkerSelected;
        }

        private void OnImageryMarkerSelected(object? sender, MarkerSelectedEventArgs e)
        {
            e.CanBringToTop = true;
            if (DataContext is MapViewModel vm)
            {
                vm.SelectMapMarker(MapMarkerTemplateSelector.Unwrap(e.SelectedMarker));
            }
        }

        private void ApplyMarkerTemplates(ImageryLayer imagery)
        {
            if (TryFindResource("DistrictMarkerTemplateSelector") is DataTemplateSelector selector)
            {
                imagery.MarkerTemplateSelector = selector;
                imagery.MarkerTemplate = null;
                return;
            }

            if (TryFindResource("StopMarkerTemplate") is DataTemplate template)
            {
                imagery.MarkerTemplate = template;
            }
        }

        private void OnMapMarkersChanged(object? sender, EventArgs e) =>
            Dispatcher.Invoke(RefreshMarkersOnImageryLayer);

        private void RefreshMarkersOnImageryLayer()
        {
            if (_placingMarkers)
            {
                return;
            }

            if (!TryPlaceMarkers())
            {
                ScheduleMarkerHostRetry();
            }
        }

        private bool TryPlaceMarkers()
        {
            if (_placingMarkers)
            {
                return false;
            }

            _placingMarkers = true;
            try
            {
                if (!IsLoaded || !_mapLayerInitialized)
                {
                    _pendingMarkerRefresh = true;
                    return false;
                }

                if (DataContext is not MapViewModel vm ||
                    DistrictTilesLayer is not ImageryLayer imagery)
                {
                    _pendingMarkerRefresh = true;
                    return false;
                }

                if (!CanHostMarkers() || !CanApplyLayerCenter())
                {
                    _pendingMarkerRefresh = true;
                    return false;
                }

                ApplyMarkerTemplates(imagery);

                // Re-assign collection so Syncfusion refreshes marker visuals. Do not bind Markers or
                // MarkerTemplateSelector in XAML — CustomDataSymbol.ApplyTemplate calls TransformToVisual
                // before the layer is parented (VM runtime-errors.log 2026-09-17).
                if (!MapMarkerHost.TryAssignAndLayout(MapControl, imagery, vm.MapMarkers))
                {
                    _pendingMarkerRefresh = true;
                    return false;
                }

                _pendingMarkerRefresh = false;
                _markerHostRetry?.Stop();
                return true;
            }
            catch (Exception ex)
            {
                _pendingMarkerRefresh = true;
                Logger.Warning(ex, "Failed to refresh map markers on imagery layer");
                return false;
            }
            finally
            {
                _placingMarkers = false;
            }
        }

        /// <summary>
        /// SizeChanged is not guaranteed after the first layout. Retry until TransformToVisual
        /// succeeds so pins are not stuck pending with a finished viewport
        /// (code-review 2026-09-17).
        /// </summary>
        private void ScheduleMarkerHostRetry()
        {
            _pendingMarkerRefresh = true;
            if (!IsLoaded)
            {
                _markerHostRetry?.Stop();
                return;
            }

            _markerHostRetry ??= new MapMarkerHost.RetryScheduler(
                Dispatcher,
                TryPlaceMarkers,
                retries => Logger.Warning("Map markers still pending after {Retries} host retries", retries));
            _markerHostRetry.Arm();
        }

        /// <summary>
        /// Size + HwndSource gate. Pair with <see cref="CanApplyLayerCenter"/> so templates are not
        /// applied until <c>TransformToVisual</c> between the layer and map succeeds.
        /// </summary>
        private bool CanHostMarkers()
        {
            var map = MapControl;
            if (map is null || DistrictTilesLayer is null)
            {
                return false;
            }

            if (map.ActualWidth <= 0 || map.ActualHeight <= 0)
            {
                return false;
            }

            return PresentationSource.FromVisual(map) is not null;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MapViewModel vm)
            {
                return;
            }

            if (e.PropertyName == nameof(MapViewModel.MapMarkers))
            {
                Dispatcher.Invoke(RefreshMarkersOnImageryLayer);
                return;
            }

            if (e.PropertyName == nameof(MapViewModel.MapZoomLevel))
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (MapControl is not null)
                    {
                        MapControl.ZoomLevel = vm.MapZoomLevel;
                    }

                    // MarkerTemplate visuals bind Data.MarkerSize / LabelFontSize / ShowCaption.
                    // Syncfusion CustomDataSymbol does not remeasure when those nested props change —
                    // re-assign Markers so captions shrink/hide with zoom (UTM clerk report 2026-09-11).
                    RefreshMarkersOnImageryLayer();
                    ScheduleAttributionRefresh();
                }, DispatcherPriority.Background);
                return;
            }

            if (e.PropertyName == nameof(MapViewModel.MapCenter))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_currentLayer is ImageryLayer imagery
                        && !TrySetLayerCenter(imagery, vm.MapCenter))
                    {
                        _pendingCameraSync = true;
                    }

                    ScheduleAttributionRefresh();
                });
            }
        }

        /// <summary>
        /// Debounced Map Tiles viewport request (Google copyright text for the tiles on screen).
        /// Pan/wheel raise MapCenter/MapZoomLevel many times per second; only the settled camera is billed.
        /// </summary>
        private void ScheduleAttributionRefresh()
        {
            if (DistrictTilesLayer is not { IsGoogleTilesActive: true })
            {
                return;
            }

            _attributionTimer ??= new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = AttributionDebounce,
            };
            _attributionTimer.Tick -= OnAttributionTimerTick;
            _attributionTimer.Tick += OnAttributionTimerTick;
            _attributionTimer.Stop();
            _attributionTimer.Start();
        }

        private async void OnAttributionTimerTick(object? sender, EventArgs e)
        {
            _attributionTimer?.Stop();
            if (DistrictTilesLayer is null)
            {
                return;
            }

            try
            {
                await MapTileBootstrap.RefreshGoogleAttributionAsync(
                    DistrictTilesLayer,
                    FindName("MapAttributionText") as TextBlock,
                    MapControl,
                    App.ServiceProvider).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Attribution refresh failed");
            }
        }

        /// <summary>
        /// Camera is Center + ZoomLevel only (TwoWay bindings carry wheel/drag back to the view model).
        /// Same-value sets are no-ops on the dependency properties, so this never double-loads tiles.
        /// </summary>
        private void SyncMapControlFromViewModel(MapViewModel? vm)
        {
            if (vm is null || MapControl is null)
            {
                return;
            }

            MapControl.ZoomLevel = vm.MapZoomLevel;
            if (_currentLayer is ImageryLayer imagery)
            {
                _pendingCameraSync = !TrySetLayerCenter(imagery, vm.MapCenter);
            }
        }

        private bool TrySetLayerCenter(ImageryLayer imagery, Point center)
        {
            if (!CanApplyLayerCenter())
            {
                return false;
            }

            imagery.Center = center;
            return true;
        }

        private bool CanApplyLayerCenter()
        {
            var map = MapControl;
            var layer = DistrictTilesLayer;
            if (map is null || layer is null)
            {
                return false;
            }

            if (map.ActualWidth <= 0 || map.ActualHeight <= 0)
            {
                return false;
            }

            if (PresentationSource.FromVisual(map) is null || PresentationSource.FromVisual(layer) is null)
            {
                return false;
            }

            try
            {
                _ = layer.TransformToVisual(map);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>Feeds the real SfMap pixel size to span-fit zoom (<c>MapDefaults.ZoomForBounds</c>).</summary>
        private void GeoMap_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ReportViewportSize(DataContext as MapViewModel);
            if (_pendingCameraSync)
            {
                SyncMapControlFromViewModel(DataContext as MapViewModel);
            }

            if (_pendingMarkerRefresh)
            {
                RefreshMarkersOnImageryLayer();
            }
        }

        private void ReportViewportSize(MapViewModel? vm)
        {
            if (vm is null || MapControl is null)
            {
                return;
            }

            var size = new Size(MapControl.ActualWidth, MapControl.ActualHeight);
            if (size.Width > 0 && size.Height > 0)
            {
                vm.MapViewportSize = size;
            }
        }

        private void OnRouteLineUpdated(object? sender, MapViewModel.RouteLineEventArgs e) =>
            Dispatcher.Invoke(() => ReplayRouteLine(e.Points));

        private void ReplayRouteLineFromViewModel(MapViewModel? vm)
        {
            if (vm is null)
            {
                return;
            }

            ReplayRouteLine(vm.RouteLinePoints);
        }

        private void ReplayRouteLine(IReadOnlyList<Point> points) =>
            MapRouteTrailLayer.Apply(
                RouteTrail ?? FindName("RouteTrail") as MapPolyline,
                RouteTrailLayer ?? FindName("RouteTrailLayer") as SubShapeFileLayer,
                points);

        private void OnCaptureSnapshotRequested(object? sender, EventArgs e)
        {
            if (MapControl is FrameworkElement mapElement && DataContext is MapViewModel vm)
            {
                vm.CaptureMapSnapshot(mapElement);
            }
        }

        private void OnPrintRequested(object? sender, EventArgs e)
        {
            try
            {
                if (MapControl is not FrameworkElement mapElement)
                {
                    return;
                }

                OnCaptureSnapshotRequested(sender, e);

                var printDlg = new PrintDialog();
                if (printDlg.ShowDialog() != true)
                {
                    return;
                }

                var doc = new FixedDocument();
                doc.DocumentPaginator.PageSize = new Size(printDlg.PrintableAreaWidth, printDlg.PrintableAreaHeight);

                var pageContent = new PageContent();
                var fixedPage = new FixedPage
                {
                    Width = printDlg.PrintableAreaWidth,
                    Height = printDlg.PrintableAreaHeight,
                };

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = fixedPage.Width,
                    Height = fixedPage.Height * 0.8,
                    Fill = new VisualBrush(mapElement),
                };
                FixedPage.SetLeft(rect, 0);
                FixedPage.SetTop(rect, 0);
                fixedPage.Children.Add(rect);

                var caption = new TextBlock
                {
                    Text = "Route map printout",
                    Margin = new Thickness(24, fixedPage.Height * 0.82, 24, 24),
                    FontSize = 16,
                };
                fixedPage.Children.Add(caption);

                ((IAddChild)pageContent).AddChild(fixedPage);
                doc.Pages.Add(pageContent);
                printDlg.PrintDocument(doc.DocumentPaginator, "BusBuddy Route Map");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to print route map");
            }
        }

        private void OnViewResetRequested(object? sender, EventArgs e) => Dispatcher.Invoke(() =>
            SyncMapControlFromViewModel(DataContext as MapViewModel));
    }
}
