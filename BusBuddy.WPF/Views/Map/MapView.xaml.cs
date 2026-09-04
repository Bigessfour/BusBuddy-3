using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Printing;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
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
        private bool _mapLayerInitialized;
        private MapViewModel? _boundViewModel;
        private MapLayer? _currentLayer;
        private SubShapeFileLayer? _routeSubLayer;
        private MapPolyline? _routePolyline;

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
                _ = Task.Run(CheckBackendConnectivityAsync);
                Logger.Information("MapView initialized");
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DataContextChanged += OnDataContextChanged;
        }

        private void MapView_Loaded(object sender, RoutedEventArgs e)
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

                ApplyDistrictImagery(DataContext as MapViewModel);
                TryResetView();
                ToggleOsmAttribution(true);
                if (MapControl is not null)
                {
                    MapControl.IsHitTestVisible = true;
                    MapControl.EnablePan = true;
                    MapControl.EnableZoom = true;
                    SyncMapControlFromViewModel(DataContext as MapViewModel);
                }

                _mapLayerInitialized = true;
                Logger.Information("Map layer ready — pan/zoom enabled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize map on Loaded");
            }
        }

        private void MapView_Unloaded(object sender, RoutedEventArgs e)
        {
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
                ApplyDistrictImagery(newViewModel);
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
            vm.ZoomInRequested += OnZoomInRequested;
            vm.ZoomOutRequested += OnZoomOutRequested;
            vm.CenterRequested += OnCenterRequested;
            vm.ViewResetRequested += OnViewResetRequested;
            vm.RouteLineUpdated += OnRouteLineUpdated;
            vm.PrintRequested += OnPrintRequested;
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
            viewModel.MapMarkersChanged -= OnMapMarkersChanged;
            viewModel.ZoomInRequested -= OnZoomInRequested;
            viewModel.ZoomOutRequested -= OnZoomOutRequested;
            viewModel.CenterRequested -= OnCenterRequested;
            viewModel.ViewResetRequested -= OnViewResetRequested;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void ApplyDistrictImagery(MapViewModel? vm)
        {
            try
            {
                var imagery = DistrictImageryLayer;
                if (imagery is null)
                {
                    Logger.Warning("DistrictImageryLayer not found in view");
                    return;
                }

                imagery.LayerType = LayerType.OSM;
                ConfigureImageryLayer(imagery, vm);
                _currentLayer = imagery;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to configure district imagery layer");
            }
        }

        private void ConfigureImageryLayer(ImageryLayer imagery, MapViewModel? vm)
        {
            if (vm is not null)
            {
                imagery.Markers = vm.MapMarkers;
                if (TryFindResource("StudentMarkerTemplate") is DataTemplate template)
                {
                    imagery.MarkerTemplate = template;
                }
            }

            if (_routeSubLayer is not null && !imagery.SubShapeFileLayers.Contains(_routeSubLayer))
            {
                imagery.SubShapeFileLayers.Add(_routeSubLayer);
            }
        }

        private void OnMapMarkersChanged(object? sender, EventArgs e) =>
            Dispatcher.Invoke(RefreshMarkersOnImageryLayer);

        private void RefreshMarkersOnImageryLayer()
        {
            try
            {
                if (DataContext is not MapViewModel vm || DistrictImageryLayer is not ImageryLayer imagery)
                {
                    return;
                }

                if (TryFindResource("StudentMarkerTemplate") is DataTemplate template)
                {
                    imagery.MarkerTemplate = template;
                }

                // Re-assign collection so Syncfusion refreshes marker visuals.
                imagery.Markers = vm.MapMarkers;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to refresh map markers on imagery layer");
            }
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

            if (e.PropertyName is nameof(MapViewModel.MapZoomLevel) or nameof(MapViewModel.MapCenter))
            {
                Dispatcher.Invoke(() => SyncMapControlFromViewModel(vm));
            }
        }

        private void SyncMapControlFromViewModel(MapViewModel? vm)
        {
            if (vm is null || MapControl is null)
            {
                return;
            }

            MapControl.ZoomLevel = vm.MapZoomLevel;
            if (_currentLayer is ImageryLayer imagery)
            {
                imagery.Center = vm.MapCenter;
            }
        }

        private void ToggleOsmAttribution(bool visible)
        {
            if (FindName("OsmAttribution") is Border overlay)
            {
                overlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ApplyCenter(double latitude, double longitude, int? zoomLevel = null)
        {
            if (DataContext is MapViewModel vm)
            {
                vm.SetMapView(latitude, longitude, zoomLevel);
            }

            if (MapControl is not null && zoomLevel.HasValue)
            {
                MapControl.ZoomLevel = zoomLevel.Value;
            }

            if (_currentLayer is ImageryLayer imagery)
            {
                imagery.Center = new Point(latitude, longitude);
            }
        }

        private void TryResetView()
        {
            try
            {
                if (MapControl is not null)
                {
                    MapControl.ZoomLevel = MapDefaults.DefaultZoomLevel;
                }

                ApplyCenter(MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude, MapDefaults.DefaultZoomLevel);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to reset map view");
            }
        }

        private async Task CheckBackendConnectivityAsync()
        {
            try
            {
                var sp = App.ServiceProvider;
                if (sp is null)
                {
                    return;
                }

                using var scope = sp.CreateScope();
                var contextFactory = scope.ServiceProvider.GetService<IBusBuddyDbContextFactory>();
                if (contextFactory is null)
                {
                    return;
                }

                using var context = contextFactory.CreateDbContext();
                var canConnect = await context.Database.CanConnectAsync();
                Logger.Information("Database connectivity check from MapView: {CanConnect}", canConnect);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Backend connectivity check failed");
            }
        }

        private void OnRouteLineUpdated(object? sender, MapViewModel.RouteLineEventArgs e)
        {
            try
            {
                if (MapControl is null)
                {
                    return;
                }

                if (_routeSubLayer is null)
                {
                    _routeSubLayer = new SubShapeFileLayer();
                    if (_currentLayer is ImageryLayer imagery)
                    {
                        imagery.SubShapeFileLayers.Add(_routeSubLayer);
                    }
                    else
                    {
                        MapControl.Layers.Add(_routeSubLayer);
                    }
                }

                _routePolyline ??= new MapPolyline
                {
                    Stroke = Brushes.Gold,
                    StrokeThickness = 3,
                };
                if (!_routeSubLayer.MapElements.Contains(_routePolyline))
                {
                    _routeSubLayer.MapElements.Add(_routePolyline);
                }

                _routePolyline.Points = new System.Collections.ObjectModel.ObservableCollection<Point>(e.Points);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed updating route polyline");
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

                if (DataContext is MapViewModel vm)
                {
                    vm.CaptureMapSnapshot(mapElement);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to print route map");
            }
        }

        private void ApplyZoom(int delta)
        {
            try
            {
                if (MapControl is null)
                {
                    return;
                }

                var current = MapControl.ZoomLevel;
                var target = Math.Clamp(current + delta, 1, 18);
                MapControl.ZoomLevel = target;
                if (DataContext is MapViewModel vm)
                {
                    vm.SetMapView(vm.MapCenter.X, vm.MapCenter.Y, target);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ApplyZoom failed");
            }
        }

        private void OnZoomInRequested(object? sender, EventArgs e) => Dispatcher.Invoke(() => ApplyZoom(+1));
        private void OnZoomOutRequested(object? sender, EventArgs e) => Dispatcher.Invoke(() => ApplyZoom(-1));
        private void OnCenterRequested(object? sender, EventArgs e) => Dispatcher.Invoke(() =>
        {
            if (DataContext is MapViewModel vm)
            {
                vm.CenterOnMarkers();
            }
        });
        private void OnViewResetRequested(object? sender, EventArgs e) => Dispatcher.Invoke(TryResetView);

        private void CenterOnCurrentMarkers()
        {
            try
            {
                if (MapControl is null || DataContext is not MapViewModel vm || vm.MapMarkers.Count == 0)
                {
                    return;
                }

                double minLat = double.MaxValue, maxLat = double.MinValue, minLon = double.MaxValue, maxLon = double.MinValue;
                foreach (var mk in vm.MapMarkers)
                {
                    if (mk.LatitudeDegrees < minLat) minLat = mk.LatitudeDegrees;
                    if (mk.LatitudeDegrees > maxLat) maxLat = mk.LatitudeDegrees;
                    if (mk.LongitudeDegrees < minLon) minLon = mk.LongitudeDegrees;
                    if (mk.LongitudeDegrees > maxLon) maxLon = mk.LongitudeDegrees;
                }

                ApplyCenter((minLat + maxLat) / 2d, (minLon + maxLon) / 2d, MapDefaults.DefaultZoomLevel);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "CenterOnCurrentMarkers failed");
            }
        }
    }
}
