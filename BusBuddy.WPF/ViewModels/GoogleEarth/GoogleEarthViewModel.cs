using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.WPF.Commands; // Use local RelayCommand instead
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Models;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;

namespace BusBuddy.WPF.ViewModels.GoogleEarth
{
    /// <summary>
    /// ViewModel for Google Earth integration view
    /// Manages map layers, route visualization, and geographic data
    /// </summary>
    public class GoogleEarthViewModel : BaseViewModelMvp
    {
        private readonly IGeoDataService _geoDataService;
        // Serilog logger with enrichments for this ViewModel
        private static readonly new Serilog.ILogger Logger = Serilog.Log.ForContext<GoogleEarthViewModel>();

        private ObservableCollection<RouteModel> _routes = new();
        private RouteModel? _selectedRoute;
        private string _selectedMapLayer = "Satellite";
        private bool _isMapLoading;
        private string _statusMessage = "Ready";

        public GoogleEarthViewModel(IGeoDataService geoDataService)
        {
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));

            LoadRoutesCommand = new RelayCommand(async _ => await LoadRoutesAsync());
            RefreshMapCommand = new RelayCommand(async _ => await RefreshMapAsync());
            ExportRouteDataCommand = new RelayCommand(async _ => await ExportRouteDataAsync(), _ => SelectedRoute != null);
            ZoomInCommand = new RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new RelayCommand(_ => ZoomOut());
        }

        #region Properties

        /// <summary>
        /// Indicates if the map is currently loading
        /// </summary>
        public bool IsMapLoading
        {
            get => _isMapLoading;
            set => SetProperty(ref _isMapLoading, value);
        }

        /// <summary>
        /// Currently selected map layer (Satellite, Terrain, etc.)
        /// </summary>
        public string SelectedMapLayer
        {
            get => _selectedMapLayer;
            set
            {
                if (SetProperty(ref _selectedMapLayer, value))
                {
                    OnMapLayerChanged();
                }
            }
        }

        /// <summary>
        /// Current status of the map system
        /// </summary>
        public new string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Collection of routes to display on the map
        /// </summary>
        public ObservableCollection<RouteModel> Routes
        {
            get => _routes;
            set => SetProperty(ref _routes, value);
        }

        /// <summary>
        /// Currently selected route for detailed view
        /// </summary>
        public RouteModel? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                if (SetProperty(ref _selectedRoute, value))
                {
                    // ((RelayCommand)ExportRouteDataCommand).NotifyCanExecuteChanged(); // Not available in MVP RelayCommand
                    OnSelectedRouteChanged();
                }
            }
        }

        /// <summary>
        /// Available map layer options
        /// </summary>
        public ObservableCollection<string> MapLayers { get; } = new()
        {
            "Satellite",
            "Terrain",
            "Roadmap",
            "Hybrid"
        };

        #endregion

        #region Commands

        public ICommand LoadRoutesCommand { get; private set; } = null!;
        public ICommand RefreshMapCommand { get; private set; } = null!;
        public ICommand ExportRouteDataCommand { get; private set; } = null!;
        public ICommand ZoomInCommand { get; private set; } = null!;
        public ICommand ZoomOutCommand { get; private set; } = null!;

        #endregion

        #region Private Methods

        private async Task LoadRoutesAsync()
        {
            try
            {
                IsMapLoading = true;
                StatusMessage = "Loading routes...";

                Logger.Information("Loading routes for Google Earth integration");

                var routes = await _geoDataService.GetRoutesWithGeoDataAsync();

                Routes.Clear();
                foreach (var route in routes)
                {
                    Routes.Add(route);
                }

                StatusMessage = $"Loaded {routes.Count} routes";
                Logger.Information("Successfully loaded {Count} routes", routes.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading routes for Google Earth");
                StatusMessage = "Error loading routes";
                ShowError("Failed to load routes for Google Earth integration");
            }
            finally
            {
                IsMapLoading = false;
            }
        }

        private void OnMapLayerChanged()
        {
            Logger.Information("Map layer changed to: {Layer}", SelectedMapLayer);
            StatusMessage = $"Switched to {SelectedMapLayer} view";
        }

        private void OnSelectedRouteChanged()
        {
            if (SelectedRoute is null)
            {
                return;
            }

            try
            {
                Logger.Information("Selected route changed to: {RouteName}", SelectedRoute.RouteName ?? "Unknown");
                StatusMessage = $"Selected: {SelectedRoute.RouteName ?? "Unknown Route"}";

                // Trigger map update for selected route
                _ = Task.Run(async () => await UpdateMapForRouteAsync(SelectedRoute.RouteName ?? "Unknown"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling route selection change");
            }
        }

        private async Task RefreshMapAsync()
        {
            try
            {
                IsMapLoading = true;
                StatusMessage = "Refreshing map...";

                if (SelectedRoute is not null)
                {
                    Logger.Information("Refreshing map for route: {RouteName}", SelectedRoute.RouteName ?? "Unknown");
                    await UpdateMapForRouteAsync(SelectedRoute.RouteName ?? "Unknown");
                }
                else
                {
                    await LoadAllRoutesOnMapAsync();
                }

                StatusMessage = "Map refreshed";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error refreshing map");
                StatusMessage = "Error refreshing map";
                ShowError("Failed to refresh map display");
            }
            finally
            {
                IsMapLoading = false;
            }
        }

        private async Task LoadAllRoutesOnMapAsync()
        {
            // Create sample route data for demonstration
            var sampleRoute = new RouteModel
            {
                RouteId = 1,
                RouteName = "Sample Route 1",
                Date = DateTime.Today,
                School = "Sample School",
                IsActive = true
            };

            await UpdateMapForRouteAsync(sampleRoute.RouteName);
        }

        private async Task UpdateMapForRouteAsync(string routeName)
        {
            // Create sample route data for demonstration
            var sampleRoute = new RouteModel
            {
                RouteId = 1,
                RouteName = routeName,
                Date = DateTime.Today,
                School = "Sample School",
                IsActive = true
            };

            await Task.Delay(500); // Simulate map update
            Logger.Information("Map updated for route: {RouteName}", routeName);
        }

        private async Task ExportRouteDataAsync()
        {
            // Create sample route data for demonstration
            var sampleRoute = new RouteModel
            {
                RouteId = 1,
                RouteName = "Sample Export Route",
                Date = DateTime.Today,
                School = "Sample School",
                IsActive = true
            };

            await Task.Delay(200); // Simulate export
            Logger.Information("Route data exported for: {RouteName}", sampleRoute.RouteName);
        }

        private void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // Simple error display for now
            StatusMessage = $"Error: {message}";
        }

        private void ZoomIn()
        {
            StatusMessage = "Zooming in...";
            Logger.Information("Map zoom in requested");
            // TODO: Implement zoom in functionality
        }

        private void ZoomOut()
        {
            StatusMessage = "Zooming out...";
            Logger.Information("Map zoom out requested");
            // TODO: Implement zoom out functionality
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        #endregion
    }

}
