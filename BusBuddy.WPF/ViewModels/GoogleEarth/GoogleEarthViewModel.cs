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
    private readonly IEligibilityService? _eligibilityService;
    /// <summary>
    /// Optional geocoder for converting addresses to coordinates.
    /// </summary>
    private readonly IGeocodingService? _geocodingService;
        // Serilog logger with enrichments for this ViewModel
        private static readonly new Serilog.ILogger Logger = Serilog.Log.ForContext<GoogleEarthViewModel>();

        private ObservableCollection<RouteModel> _routes = new();
        private RouteModel? _selectedRoute;
        private string _selectedMapLayer = "Satellite";
        private bool _isMapLoading;
        private string _statusMessage = "Ready";
    private bool _isLiveTrackingEnabled;
    private ObservableCollection<BusBuddy.Core.Models.Bus> _activeBuses = new();
    private BusBuddy.Core.Models.Bus? _selectedBus;
    private bool _districtBoundaryVisible;
    private bool _townBoundaryVisible;

        public GoogleEarthViewModel(IGeoDataService geoDataService, IEligibilityService? eligibilityService = null, IGeocodingService? geocodingService = null)
        {
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));
            _eligibilityService = eligibilityService; // optional during MVP
            _geocodingService = geocodingService; // optional until wired

            LoadRoutesCommand = new RelayCommand(async _ => await LoadRoutesAsync());
            RefreshMapCommand = new RelayCommand(async _ => await RefreshMapAsync());
            ExportRouteDataCommand = new RelayCommand(async _ => await ExportRouteDataAsync(), _ => SelectedRoute != null);
            ZoomInCommand = new RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new RelayCommand(_ => ZoomOut());

            // Commands referenced by XAML — MVP stubs with logging
            CenterOnFleetCommand = new RelayCommand(_ => CenterOnFleet());
            ShowAllBusesCommand = new RelayCommand(_ => ShowAllBuses());
            ShowRoutesCommand = new RelayCommand(_ => ShowRoutes());
            ShowSchoolsCommand = new RelayCommand(_ => ShowSchools());
            TrackSelectedBusCommand = new RelayCommand(_ => TrackSelectedBus(), _ => SelectedBus != null);
            ResetViewCommand = new RelayCommand(_ => ResetView());
            CheckEligibilityCommand = new RelayCommand(async _ => await CheckEligibilityAsync(), _ => _eligibilityService != null);

            // Reasonable defaults
            DistrictBoundaryVisible = false;
            TownBoundaryVisible = false;

            // Seed a marker for Wiley School so the map has an anchor
            MapMarkers = new ObservableCollection<MapMarker>
            {
                new MapMarker
                {
                    Label = "Wiley School RE-13JT",
                    Latitude = 38.1527,
                    Longitude = -102.7204
                }
            };
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
        /// Live tracking toggle state (bound to ButtonAdv)
        /// </summary>
        public bool IsLiveTrackingEnabled
        {
            get => _isLiveTrackingEnabled;
            set => SetProperty(ref _isLiveTrackingEnabled, value);
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
    /// Markers to display on the map (students, school, etc.).
    /// </summary>
    public ObservableCollection<MapMarker> MapMarkers { get; private set; } = new();

        /// <summary>
        /// Active buses list shown in SfDataGrid
        /// </summary>
        public ObservableCollection<BusBuddy.Core.Models.Bus> ActiveBuses
        {
            get => _activeBuses;
            set => SetProperty(ref _activeBuses, value);
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
        /// Currently selected bus in the grid
        /// </summary>
        public BusBuddy.Core.Models.Bus? SelectedBus
        {
            get => _selectedBus;
            set => SetProperty(ref _selectedBus, value);
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

        /// <summary>
        /// Toggle to show or hide the school district boundary overlay layer
        /// </summary>
        public bool DistrictBoundaryVisible
        {
            get => _districtBoundaryVisible;
            set
            {
                if (SetProperty(ref _districtBoundaryVisible, value))
                {
                    Logger.Information("District boundary overlay visibility changed: {Visible}", value);
                }
            }
        }

        /// <summary>
        /// Toggle to show or hide the town boundary overlay (Wiley town limits)
        /// </summary>
        public bool TownBoundaryVisible
        {
            get => _townBoundaryVisible;
            set
            {
                if (SetProperty(ref _townBoundaryVisible, value))
                {
                    Logger.Information("Town boundary overlay visibility changed: {Visible}", value);
                }
            }
        }

        #endregion

        #region Commands

        public ICommand LoadRoutesCommand { get; private set; } = null!;
        public ICommand RefreshMapCommand { get; private set; } = null!;
        public ICommand ExportRouteDataCommand { get; private set; } = null!;
        public ICommand ZoomInCommand { get; private set; } = null!;
        public ICommand ZoomOutCommand { get; private set; } = null!;

    // Additional commands referenced in XAML
    public ICommand CenterOnFleetCommand { get; private set; } = null!;
    public ICommand ShowAllBusesCommand { get; private set; } = null!;
    public ICommand ShowRoutesCommand { get; private set; } = null!;
    public ICommand ShowSchoolsCommand { get; private set; } = null!;
    public ICommand TrackSelectedBusCommand { get; private set; } = null!;
    public ICommand ResetViewCommand { get; private set; } = null!;
    public ICommand CheckEligibilityCommand { get; private set; } = null!;
    public ICommand AddMarkerCommand { get; private set; } = null!;

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

        // MVP stub implementations for XAML-bound commands
        private void CenterOnFleet()
        {
            StatusMessage = "Centering map on fleet...";
            Logger.Information("Center on fleet requested");
        }

        private void ShowAllBuses()
        {
            StatusMessage = "Showing all buses...";
            Logger.Information("Show all buses requested");
        }

        private void ShowRoutes()
        {
            StatusMessage = "Showing routes on map...";
            Logger.Information("Show routes requested");
        }

        private void ShowSchools()
        {
            StatusMessage = "Showing schools on map...";
            Logger.Information("Show schools requested");
        }

        private void TrackSelectedBus()
        {
            if (SelectedBus is null)
            {
                StatusMessage = "No bus selected to track";
                return;
            }
            StatusMessage = $"Tracking bus {SelectedBus.BusNumber}...";
            Logger.Information("Tracking selected bus {BusNumber}", SelectedBus.BusNumber);
        }

        private void ResetView()
        {
            StatusMessage = "Resetting map view...";
            Logger.Information("Reset view requested");
        }

        private async Task CheckEligibilityAsync()
        {
            if (_eligibilityService is null)
            {
                StatusMessage = "Eligibility service not available";
                return;
            }
            // Demo coordinates — replace with selected student location later
            var lat = 38.1544; // Wiley, CO vicinity
            var lon = -102.7177;
            try
            {
                var eligible = await _eligibilityService.IsEligibleAsync(lat, lon);
                StatusMessage = eligible ? "Eligible: In district and outside Wiley town" : "Not eligible";
                Logger.Information("Eligibility check at ({Lat}, {Lon}): {Eligible}", lat, lon, eligible);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Eligibility check failed");
                StatusMessage = $"Eligibility check error: {ex.Message}";
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        #endregion

        /// <summary>
        /// Lightweight marker model compatible with Syncfusion markers binding.
        /// </summary>
        public sealed class MapMarker
        {
            public string? Label { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }
    }

}
