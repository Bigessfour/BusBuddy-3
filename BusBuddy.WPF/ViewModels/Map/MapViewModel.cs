using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.WPF.Commands;
using CommunityToolkit.Mvvm.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;
using System.Text.Json; // Microsoft .NET docs: System.Text.Json for JSON serialization/deserialization
using System.Windows; // For System.Windows.Point used by Syncfusion MapPolyline
using System.Windows.Threading;
using System.Collections.Generic; // For generic collections
using System.Linq; // For LINQ operations
using System.Windows.Media; // For VisualTreeHelper during snapshot
using System.Windows.Media.Imaging; // For RenderTargetBitmap / PngBitmapEncoder (Microsoft WPF docs: Imaging)
using System.IO; // For saving generated eligibility PDF to disk
using BusBuddy.WPF;

namespace BusBuddy.WPF.ViewModels.Map
{
    /// <summary>
    /// ViewModel for the Syncfusion SfMap surface (OpenStreetMap + Maps Platform geocoding).
    /// Plots student addresses, school destinations, and route trails/waypoints.
    /// Live fleet GPS tracking is deferred and stays off.
    /// </summary>
    public class MapViewModel : BaseViewModel
    {
        private readonly IGeoDataService _geoDataService;
        /// <summary>
        /// Optional geocoder for converting addresses to coordinates.
        /// </summary>
        private readonly IGeocodingService? _geocodingService;
        private readonly IRoutingService? _routingService;
        private readonly BusBuddy.Core.Services.PdfReportService _pdfReportService = new(); // Lightweight stateless service
        private readonly BusBuddy.Core.Services.IStudentService? _studentService; // If available for pulling students
        private readonly IBusService? _busService;
        private readonly IServiceScopeFactory? _scopeFactory;
        // Serilog logger with enrichments for this ViewModel
        private static readonly new Serilog.ILogger Logger = Serilog.Log.ForContext<MapViewModel>();

        private ObservableCollection<RouteModel> _routes = new();
        private RouteModel? _selectedRoute;
        private string _selectedMapLayer = "OpenStreetMap";
        private bool _isMapLoading;
        private string _statusMessage = "Ready";
        private bool _isLiveTrackingEnabled;
        private int _trackingIntervalIndex = 1;
        private DispatcherTimer? _liveTrackingTimer;
        private static readonly TimeSpan[] TrackingIntervals =
        [
            TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
    ];
        private ObservableCollection<BusBuddy.Core.Models.Bus> _activeBuses = new();
        private BusBuddy.Core.Models.Bus? _selectedBus;
        private BusBuddy.WPF.Commands.RelayCommand? _trackSelectedBusRelay;
        private byte[]? _latestMapSnapshotPng; // Holds last captured map snapshot (PNG bytes) for PDF embedding
        private Point _mapCenter = new(MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude);
        private int _mapZoomLevel = MapDefaults.DefaultZoomLevel;
        private const string RouteWaypointPrefix = "WP ";

        /// <summary>
        /// Points representing the currently selected route polyline — consumed by view to draw MapPolyline.
        /// </summary>
        public ObservableCollection<Point> RouteLinePoints { get; } = new();

        /// <summary>
        /// Raised when route line points are updated and the view should redraw the polyline layer.
        /// </summary>
        public event EventHandler<RouteLineEventArgs>? RouteLineUpdated;

        /// <summary>
        /// Raised when a print of the current route map has been requested.
        /// </summary>
        public event EventHandler? PrintRequested;

        // Map interaction events (view listens and applies actual SfMap changes)
        public event EventHandler? ZoomInRequested;
        public event EventHandler? ZoomOutRequested;
        public event EventHandler? CenterRequested;
        public event EventHandler? ViewResetRequested;
        public event EventHandler? MapMarkersChanged;

        /// <summary>
        /// Latest captured map snapshot in PNG format (used for embedding into route PDF exports).
        /// A separate capturing routine in the View should set this after rendering a visual to a RenderTargetBitmap and encoding to PNG.
        /// </summary>
        public byte[]? LatestMapSnapshotPng
        {
            get => _latestMapSnapshotPng;
            set => SetProperty(ref _latestMapSnapshotPng, value);
        }

        public MapViewModel(IGeoDataService geoDataService, IGeocodingService? geocodingService = null, BusBuddy.Core.Services.IStudentService? studentService = null, IBusService? busService = null, IServiceScopeFactory? scopeFactory = null, IRoutingService? routingService = null)
        {
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));
            _geocodingService = geocodingService; // optional until wired
            _routingService = routingService;
            _studentService = studentService;
            _busService = busService;
            _scopeFactory = scopeFactory;

            LoadRoutesCommand = new AsyncRelayCommand(LoadRoutesAsync);
            RefreshMapCommand = new AsyncRelayCommand(RefreshMapAsync);
            ExportRouteDataCommand = new AsyncRelayCommand(ExportRouteDataAsync, () => SelectedRoute != null);
            ZoomInCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ZoomOut());

            // Commands referenced by XAML (map toolbar)
            CenterOnFleetCommand = new AsyncRelayCommand(CenterOnFleetAsync);
            ShowAllBusesCommand = new AsyncRelayCommand(ShowAllBusesAsync);
            ShowRoutesCommand = new AsyncRelayCommand(ShowRoutesAsync);
            ShowSchoolsCommand = new AsyncRelayCommand(ShowSchoolsAsync);
            TrackSelectedBusCommand = _trackSelectedBusRelay = new BusBuddy.WPF.Commands.RelayCommand(_ => TrackSelectedBus(), _ => SelectedBus != null);
            ResetViewCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ResetView());

            // Print current route map/directions
            PrintRouteMapsCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => OnPrintRequested(), _ => true);

            // Eligibility route PDF generation
            GenerateEligibilityRoutePdfCommand = new AsyncRelayCommand(GenerateEligibilityRoutePdfAndSaveAsync);

            // Add marker (stop) plotting command. Accepts parameter forms documented in AddMarkerFromParam.
            AddMarkerCommand = new BusBuddy.WPF.Commands.RelayCommand(p => AddMarkerFromParam(p));
            BulkPlotEligibleStudentsCommand = new AsyncRelayCommand(BulkPlotEligibleStudentsAsync);

            MapMarkers = new ObservableCollection<MapMarker>();
            MapMarkers.CollectionChanged += (_, _) => NotifyMapMarkersChanged();

            _ = InitializeMapDataSafeAsync();
        }

        private async Task InitializeMapDataSafeAsync()
        {
            try
            {
                await InitializeMapDataAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Initial map data load failed");
                StatusMessage = "Map data load failed";
            }
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
        /// Live fleet GPS tracking — deferred. The toggle stays off until AVL is wired.
        /// </summary>
        public bool IsLiveTrackingEnabled
        {
            get => _isLiveTrackingEnabled;
            set
            {
                if (value)
                {
                    StatusMessage = "Fleet GPS tracking is not enabled yet";
                    Logger.Information("Live tracking requested — fleet GPS is deferred");
                    if (_isLiveTrackingEnabled)
                    {
                        _ = SetProperty(ref _isLiveTrackingEnabled, false);
                        UpdateLiveTrackingTimer();
                    }
                    else
                    {
                        OnPropertyChanged();
                    }

                    return;
                }

                if (SetProperty(ref _isLiveTrackingEnabled, false))
                {
                    UpdateLiveTrackingTimer();
                }
            }
        }

        /// <summary>
        /// ComboBoxAdv index for live-tracking refresh: 0=5s, 1=10s, 2=30s, 3=1min.
        /// </summary>
        public int TrackingIntervalIndex
        {
            get => _trackingIntervalIndex;
            set
            {
                if (SetProperty(ref _trackingIntervalIndex, value))
                {
                    UpdateLiveTrackingTimer();
                }
            }
        }

        private void UpdateLiveTrackingTimer()
        {
            _liveTrackingTimer ??= new DispatcherTimer();
            _liveTrackingTimer.Tick -= OnLiveTrackingTick;
            _liveTrackingTimer.Stop();
            if (!_isLiveTrackingEnabled)
            {
                return;
            }

            var index = Math.Clamp(_trackingIntervalIndex, 0, TrackingIntervals.Length - 1);
            _liveTrackingTimer.Interval = TrackingIntervals[index];
            _liveTrackingTimer.Tick += OnLiveTrackingTick;
            _liveTrackingTimer.Start();
        }

        private async void OnLiveTrackingTick(object? sender, EventArgs e)
        {
            if (_isMapLoading)
            {
                return;
            }

            await LoadActiveBusesAsync();
            ReplaceLiveBusMarkers();
        }

        private void ReplaceLiveBusMarkers()
        {
            for (var i = MapMarkers.Count - 1; i >= 0; i--)
            {
                if (MapMarkers[i].Label.StartsWith("Bus ", StringComparison.Ordinal))
                {
                    MapMarkers.RemoveAt(i);
                }
            }

            foreach (var bus in ActiveBuses)
            {
                if (!bus.CurrentLatitude.HasValue || !bus.CurrentLongitude.HasValue)
                {
                    continue;
                }

                MapMarkers.Add(MapMarker.FromDegrees(
                    (double)bus.CurrentLatitude.Value,
                    (double)bus.CurrentLongitude.Value,
                    $"Bus {bus.BusNumber}"));
            }
        }

        /// <summary>
        /// Currently selected map layer (OSM only until Maps tiles resume)
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
        /// Surfaces year-start draft proposals on the map status line and selects the first draft route when present.
        /// </summary>
        public void ApplyGenerationResult(BusBuddy.Core.Services.RouteDetermination.RouteGenerationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (!result.Success)
            {
                StatusMessage = result.Error ?? "Route generation failed";
                return;
            }

            var draftNames = result.Proposals
                .Select(p => p.SuggestedRouteName)
                .Where(n => n.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            StatusMessage =
                $"Draft proposals: {result.Proposals.Count} route(s), {result.AssignedStudentCount} assigned, " +
                $"{result.UnclusteredStudentIds.Count} unclustered — select a Draft-* route to review / override";

            Logger.Information(
                "Map status updated for generation OpId={OpId} Drafts={DraftCount}",
                result.OperationId,
                draftNames.Count);

            async Task SelectDraftOnUiAsync()
            {
                await LoadRoutesAsync().ConfigureAwait(true);
                var draft = Routes.FirstOrDefault(r =>
                    draftNames.Any(n => string.Equals(n, r.RouteName, StringComparison.OrdinalIgnoreCase))
                    || r.RouteName.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase));
                if (draft is not null)
                {
                    SelectedRoute = draft;
                }
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                _ = SelectDraftOnUiAsync();
            }
            else
            {
                _ = dispatcher.InvokeAsync(SelectDraftOnUiAsync);
            }
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
        /// Average travel speed in MPH for schedule estimation (configurable at runtime for refinement).
        /// </summary>
        private double _averageRouteSpeedMph = 35.0; // default rural estimate
        public double AverageRouteSpeedMph
        {
            get => _averageRouteSpeedMph;
            set => SetProperty(ref _averageRouteSpeedMph, value);
        }

        /// <summary>
        /// Dwell minutes per stop (boarding + safety). Adjustable for calibration.
        /// </summary>
        private int _dwellMinutesPerStop = 1;
        public int DwellMinutesPerStop
        {
            get => _dwellMinutesPerStop;
            set => SetProperty(ref _dwellMinutesPerStop, value);
        }

        /// <summary>
        /// Markers to display on the map (students, school, etc.).
        /// </summary>
        public ObservableCollection<MapMarker> MapMarkers { get; private set; } = new();

        /// <summary>
        /// Center point for the OSM imagery layer (latitude = X, longitude = Y per Syncfusion).
        /// Public setter required for TwoWay ZoomLevel/Center bindings.
        /// </summary>
        public Point MapCenter
        {
            get => _mapCenter;
            set => SetProperty(ref _mapCenter, value);
        }

        /// <summary>
        /// Zoom level bound to SfMap.ZoomLevel.
        /// </summary>
        public int MapZoomLevel
        {
            get => _mapZoomLevel;
            set => SetProperty(ref _mapZoomLevel, Math.Clamp(value, 1, 18));
        }

        /// <summary>
        /// Updates map center and optional zoom for view bindings.
        /// </summary>
        public void SetMapView(double latitude, double longitude, int? zoomLevel = null)
        {
            MapCenter = new Point(latitude, longitude);
            if (zoomLevel.HasValue)
            {
                MapZoomLevel = zoomLevel.Value;
            }
        }

        private void NotifyMapMarkersChanged()
        {
            OnPropertyChanged(nameof(MapMarkers));
            try
            {
                MapMarkersChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MapMarkersChanged event dispatch failed");
            }
        }

        private async Task<(double Lat, double Lon)?> TryGeocodeStudentAsync(
            BusBuddy.Core.Models.Student student,
            IServiceScope? scope)
        {
            if (_geocodingService is not null)
            {
                try
                {
                    var geo = await _geocodingService.GeocodeAsync(
                        student.HomeAddress, student.City, student.State, student.Zip);
                    if (geo.HasValue)
                    {
                        return (geo.Value.latitude, geo.Value.longitude);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "IGeocodingService geocode failed for student {Id}", student.StudentId);
                }
            }

            var mapsGeo = scope?.ServiceProvider.GetService<IMapsGeoService>()
                ?? App.ServiceProvider?.GetService<IMapsGeoService>();
            if (mapsGeo is null || !mapsGeo.IsConfigured)
            {
                return null;
            }

            try
            {
                return await mapsGeo.GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "IMapsGeoService geocode failed for student {Id}", student.StudentId);
                return null;
            }
        }

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
                    // ((RelayCommand)ExportRouteDataCommand).NotifyCanExecuteChanged();
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
            set
            {
                if (SetProperty(ref _selectedBus, value))
                {
                    _trackSelectedBusRelay?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Available map layer options
        /// </summary>
        public ObservableCollection<string> MapLayers { get; } = new()
        {
            "OpenStreetMap"
        };

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
        public ICommand AddMarkerCommand { get; private set; } = null!;
        public ICommand PrintRouteMapsCommand { get; private set; } = null!;
        public ICommand GenerateEligibilityRoutePdfCommand { get; private set; } = null!; // New command to trigger eligibility PDF generation
        public ICommand BulkPlotEligibleStudentsCommand { get; private set; } = null!; // New: auto geocode + plot eligible rural students

        #endregion

        #region Private Methods

        private async Task LoadRoutesAsync()
        {
            try
            {
                IsMapLoading = true;
                StatusMessage = "Loading routes...";

                Logger.Information("Loading routes for district map");

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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error loading routes for the map");
                StatusMessage = "Error loading routes";
                ShowError("Failed to load routes for district map");
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
                _ = UpdateMapForRouteAsync(SelectedRoute.RouteName ?? "Unknown");
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error handling route selection change");
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error refreshing map");
                StatusMessage = "Error refreshing map";
                ShowError("Failed to refresh map display");
            }
            finally
            {
                IsMapLoading = false;
            }
        }

        private async Task InitializeMapDataAsync()
        {
            Logger.Information("InitializeMapDataAsync starting — loading routes and active buses");
            try
            {
                await LoadRoutesAsync();
                await LoadActiveBusesAsync();
                var routeWithTrail = Routes.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.WaypointsJson));
                if (routeWithTrail is not null)
                {
                    SelectedRoute = routeWithTrail;
                    await UpdateMapForRouteAsync(routeWithTrail.RouteName ?? "Route");
                }
                else
                {
                    var (schoolLat, schoolLon) = await ResolveSchoolAnchorAsync();
                    SetMapView(schoolLat, schoolLon, MapDefaults.SchoolZoomLevel);
                }

                Logger.Information(
                    "InitializeMapDataAsync completed Routes={RouteCount} Buses={BusCount} Markers={MarkerCount} Trail={HasTrail}",
                    Routes.Count, ActiveBuses.Count, MapMarkers.Count, routeWithTrail is not null);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "InitializeMapDataAsync failed");
            }
        }

        private IBusService? ResolveBusService(IServiceScope? scope) =>
            _busService ?? scope?.ServiceProvider.GetService<IBusService>();

        private BusBuddy.Core.Services.IStudentService? ResolveStudentService(IServiceScope? scope) =>
            _studentService ?? scope?.ServiceProvider.GetService<BusBuddy.Core.Services.IStudentService>();

        private async Task LoadActiveBusesAsync()
        {
            using var scope = _scopeFactory?.CreateScope();
            var busService = ResolveBusService(scope);
            if (busService is null)
            {
                Logger.Information("LoadActiveBusesAsync skipped — IBusService not registered");
                return;
            }

            try
            {
                var buses = await busService.GetActiveBusesAsync();
                ActiveBuses.Clear();
                var withGps = 0;
                foreach (var bus in buses)
                {
                    ActiveBuses.Add(bus);
                    if (bus.CurrentLatitude.HasValue && bus.CurrentLongitude.HasValue)
                    {
                        withGps++;
                    }
                }

                Logger.Information("Active buses loaded Count={Count} WithGps={WithGps}", ActiveBuses.Count, withGps);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "LoadActiveBusesAsync failed");
            }
        }

        private async Task LoadAllRoutesOnMapAsync()
        {
            try
            {
                if (Routes.Count == 0)
                {
                    await LoadRoutesAsync();
                }

                var withWaypoints = Routes.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.WaypointsJson));
                if (withWaypoints is null)
                {
                    StatusMessage = Routes.Count == 0 ? "No routes loaded" : "Routes have no waypoints yet";
                    return;
                }

                SelectedRoute = withWaypoints;
                await UpdateMapForRouteAsync(withWaypoints.RouteName ?? "Route");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "LoadAllRoutesOnMapAsync overlay failed");
            }
        }

        /// <summary>
        /// Automatically loads all students, geocodes missing coordinates, and plots markers.
        /// Anyone already in the system is treated as eligible — no geofence.
        /// </summary>
        private async Task BulkPlotEligibleStudentsAsync()
        {
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var studentService = ResolveStudentService(scope);
                if (studentService is null)
                {
                    StatusMessage = "Student service unavailable";
                    return;
                }
            StatusMessage = "Loading students...";
            List<BusBuddy.Core.Models.Student> students;
            try
            {
                students = await studentService.GetAllStudentsAsync();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Bulk plot: failed loading students");
                StatusMessage = "Load students failed";
                return;
            }

            if (students.Count == 0)
            {
                StatusMessage = "No students";
                return;
            }

            int geocoded = 0, eligibleCount = 0, plotted = 0;
            StatusMessage = $"Plotting {students.Count} students...";

            foreach (var stu in students)
            {
                double? lat = stu.Latitude.HasValue ? (double)stu.Latitude.Value : null;
                double? lon = stu.Longitude.HasValue ? (double)stu.Longitude.Value : null;
                if (!lat.HasValue || !lon.HasValue)
                {
                    var geo = await TryGeocodeStudentAsync(stu, scope);
                    if (geo.HasValue)
                    {
                        lat = geo.Value.Lat;
                        lon = geo.Value.Lon;
                        stu.Latitude = (decimal)lat.Value;
                        stu.Longitude = (decimal)lon.Value;
                        if (await studentService.UpdateStudentAsync(stu))
                        {
                            geocoded++;
                        }
                        else
                        {
                            Logger.Warning("Bulk plot: failed persisting geocode for student {Id}", stu.StudentId);
                        }
                    }
                }

                if (!lat.HasValue || !lon.HasValue)
                {
                    continue;
                }

                eligibleCount++;

                try
                {
                    PlotStop(lat.Value, lon.Value, new[] { stu.StudentName ?? stu.StudentNumber ?? "Student" }, stu.StudentName);
                    plotted++;
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Plot failed for student {Id}", stu.StudentId);
                }
            }

            StatusMessage = plotted == 0
                ? $"Student plotting complete — no locations ({students.Count} in DB; geocoded {geocoded})"
                : $"Student plotting complete — {plotted} locations";
            Logger.Information("Bulk plot complete InSystem={Eligible} Geocoded={Geocoded} Plotted={Plotted} Total={Total}", eligibleCount, geocoded, plotted, students.Count);
            if (plotted > 0)
            {
                CenterOnMarkers();
            }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Bulk plot students failed");
                StatusMessage = "Plot students failed — see logs";
            }
        }

        private async Task UpdateMapForRouteAsync(string routeName)
        {
            try
            {
                await Task.Delay(100); // brief yield

                // Deserialize WaypointsJson to list of Points if available
                var points = Array.Empty<Point>();
                if (SelectedRoute is not null && !string.IsNullOrWhiteSpace(SelectedRoute.WaypointsJson))
                {
                    points = await TryRefreshDrivePathAsync(SelectedRoute)
                        ?? ParseWaypointsToPoints(SelectedRoute.WaypointsJson);
                }

                await UpdatePolylineAsync(points);
                if (points.Length > 0)
                {
                    ClearRouteWaypointMarkers();
                    for (var i = 0; i < points.Length; i++)
                    {
                        var label = i == 0
                            ? RouteWaypointPrefix + "Start"
                            : i == points.Length - 1
                                ? RouteWaypointPrefix + "End"
                                : $"{RouteWaypointPrefix}Stop {i}";
                        PlotStop(points[i].X, points[i].Y, null, label);
                    }

                    CenterOnPoints(points);
                    StatusMessage = $"Route {routeName}: trail and {points.Length} waypoints";
                }
                else
                {
                    ClearRouteWaypointMarkers();
                    StatusMessage = $"Route {routeName} has no waypoints to display";
                }
                Logger.Information("Map updated for route: {RouteName} with {Count} points", routeName, points.Length);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed to update map for route {RouteName}", routeName);
            }
        }

        /// <summary>
        /// Optionally refresh road geometry via Routes API. Fail-open: returns null on any error
        /// so the map keeps using stored waypoints.
        /// </summary>
        private async Task<Point[]?> TryRefreshDrivePathAsync(RouteModel route)
        {
            var refresh = await RouteDrivePathRefresher.TryRefreshAsync(_routingService, route).ConfigureAwait(true);
            if (!refresh.Success || refresh.Path is null || refresh.Path.Points.Count == 0)
            {
                return null;
            }

            return refresh.Path.Points.Select(p => new Point(p.Latitude, p.Longitude)).ToArray();
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
            var next = Math.Clamp(MapZoomLevel + 1, 1, 18);
            SetMapView(MapCenter.X, MapCenter.Y, next);
            StatusMessage = $"Zoom level {next}";
            Logger.Debug("Map zoom in to {Zoom}", next);
        }

        private void ZoomOut()
        {
            var next = Math.Clamp(MapZoomLevel - 1, 1, 18);
            SetMapView(MapCenter.X, MapCenter.Y, next);
            StatusMessage = $"Zoom level {next}";
            Logger.Debug("Map zoom out to {Zoom}", next);
        }

        private async Task CenterOnFleetAsync()
        {
            try
            {
                if (MapMarkers.Count > 0)
                {
                    CenterOnMarkers();
                    StatusMessage = "Centered on plotted stops";
                    return;
                }

                var (schoolLat, schoolLon) = await ResolveSchoolAnchorAsync();
                SetMapView(schoolLat, schoolLon, MapDefaults.SchoolZoomLevel);
                StatusMessage = "Centered on school — fleet GPS is not enabled yet";
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "CenterOnFleet failed");
                StatusMessage = "Could not center map";
            }
        }

        /// <summary>Centers the map on the current marker set.</summary>
        public void CenterOnMarkers()
        {
            if (MapMarkers.Count == 0)
            {
                return;
            }

            CenterOnPoints(MapMarkers.Select(m => new Point(m.LatitudeDegrees, m.LongitudeDegrees)));
        }

        private void CenterOnPoints(IEnumerable<Point> points)
        {
            var list = points.ToList();
            if (list.Count == 0)
            {
                return;
            }

            double minLat = double.MaxValue, maxLat = double.MinValue, minLon = double.MaxValue, maxLon = double.MinValue;
            foreach (var pt in list)
            {
                if (pt.X < minLat) minLat = pt.X;
                if (pt.X > maxLat) maxLat = pt.X;
                if (pt.Y < minLon) minLon = pt.Y;
                if (pt.Y > maxLon) maxLon = pt.Y;
            }

            SetMapView((minLat + maxLat) / 2d, (minLon + maxLon) / 2d, MapDefaults.SchoolZoomLevel);
        }

        private async Task ShowAllBusesAsync()
        {
            StatusMessage = "Fleet GPS tracking is not enabled yet";
            Logger.Information("Show all buses skipped — fleet GPS is deferred");
            await Task.CompletedTask;
        }

        private async Task ShowRoutesAsync()
        {
            StatusMessage = "Showing routes on map...";
            Logger.Information("Show routes requested");
            try
            {
                await LoadAllRoutesOnMapAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowRoutes failed");
                StatusMessage = "Could not show routes";
            }
        }

        private async Task ShowSchoolsAsync()
        {
            StatusMessage = "Showing schools on map...";
            Logger.Information("Show schools requested");
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var destService = scope?.ServiceProvider.GetService<IDestinationService>()
                    ?? App.ServiceProvider?.GetService<IDestinationService>();

                var schools = destService is not null
                    ? await destService.GetActiveSchoolsAsync()
                    : Array.Empty<Destination>();

                var plotted = 0;
                foreach (var school in schools.Where(s => s.HasGpsCoordinates))
                {
                    PlotStop((double)school.Latitude!, (double)school.Longitude!, null, school.Name);
                    plotted++;
                }

                StatusMessage = plotted == 0
                    ? "No schools with coordinates — add a school destination"
                    : $"Showing {plotted} school(s) on map";
                if (plotted > 0)
                {
                    CenterOnMarkers();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowSchools failed");
                StatusMessage = "Could not show schools";
            }
        }

        private void TrackSelectedBus()
        {
            StatusMessage = "Fleet GPS tracking is not enabled yet";
            Logger.Information("Track selected bus skipped — fleet GPS is deferred");
        }

        private void ResetView()
        {
            StatusMessage = "Resetting map view...";
            Logger.Information("Reset view requested");
            try
            {
                RouteLinePoints.Clear();
                RouteLineUpdated?.Invoke(this, new RouteLineEventArgs(RouteLinePoints));
                SetMapView(MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude, MapDefaults.DefaultZoomLevel);
                StatusMessage = "Map view reset";
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ResetView failed");
            }
        }

        private void OnPrintRequested()
        {
            try
            {
                Logger.Information("Print route maps requested");
                PrintRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed to request printing");
            }
        }

        /// <summary>
        /// Public helper to plot (or aggregate) a stop with optional student names. Returns the marker created/updated.
        /// </summary>
        /// <param name="latitude">Latitude in decimal degrees.</param>
        /// <param name="longitude">Longitude in decimal degrees.</param>
        /// <param name="studentNames">Optional collection of student names to aggregate at this stop.</param>
        /// <param name="label">Optional explicit label (overrides auto aggregation label if provided).</param>
        public MapMarker PlotStop(double latitude, double longitude, IEnumerable<string>? studentNames = null, string? label = null)
        {
            const double mergeTolerance = 0.00005; // ~5m tolerance for aggregating to existing marker
            // Try find existing marker within tolerance
            var existing = MapMarkers.FirstOrDefault(m => Math.Abs(m.LatitudeDegrees - latitude) < mergeTolerance && Math.Abs(m.LongitudeDegrees - longitude) < mergeTolerance);
            if (existing == null)
            {
                existing = MapMarker.FromDegrees(latitude, longitude, label);
                MapMarkers.Add(existing);
                Logger.Information("Added new stop marker at ({Lat}, {Lon}) Label={Label}", latitude, longitude, label ?? "<auto>");
                if (studentNames != null)
                {
                    foreach (var name in studentNames)
                    {
                        existing.AddStudent(name);
                    }
                }

                NotifyMapMarkersChanged();
                return existing;
            }

            var mutated = false;
            if (!string.IsNullOrWhiteSpace(label))
            {
                existing.Label = label;
                mutated = true;
            }

            if (studentNames != null)
            {
                foreach (var name in studentNames)
                {
                    existing.AddStudent(name);
                }

                mutated = true;
            }

            if (mutated)
            {
                NotifyMapMarkersChanged();
            }

            return existing;
        }

        /// <summary>
        /// Command target for AddMarkerCommand. Supports parameter types:
        /// 1) MapMarker instance
        /// 2) ValueTuple(double lat, double lon, string? label)
        /// 3) string "lat,lon[,label]"
        /// 4) anonymous object with Latitude/Longitude[/Label]
        /// </summary>
        private void AddMarkerFromParam(object? param)
        {
            try
            {
                if (param is null)
                {
                    PlotStop(MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude, null, "New Stop");
                    return;
                }

                switch (param)
                {
                    case MapMarker mm:
                        PlotStop(mm.LatitudeDegrees, mm.LongitudeDegrees, mm.StudentNames, mm.Label);
                        break;
                    case ValueTuple<double, double, string?> tuple:
                        PlotStop(tuple.Item1, tuple.Item2, null, tuple.Item3);
                        break;
                    case string s:
                        {
                            var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (parts.Length >= 2 && double.TryParse(parts[0], out var lat) && double.TryParse(parts[1], out var lon))
                            {
                                string? lbl = parts.Length >= 3 ? string.Join(',', parts.Skip(2)) : null;
                                PlotStop(lat, lon, null, lbl);
                            }
                            break;
                        }
                    default:
                        {
                            // Try reflection pattern for Latitude/Longitude properties
                            var latProp = param.GetType().GetProperty("Latitude");
                            var lonProp = param.GetType().GetProperty("Longitude");
                            if (latProp?.GetValue(param) is double lat && lonProp?.GetValue(param) is double lon)
                            {
                                var labelProp = param.GetType().GetProperty("Label")?.GetValue(param) as string;
                                PlotStop(lat, lon, null, labelProp);
                            }
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed to add marker from parameter");
                StatusMessage = "Add marker failed";
            }
        }

        /// <summary>
        /// Capture a visual element (map container) into PNG bytes and store in LatestMapSnapshotPng.
        /// View code-behind can call this right after PrintRequested is raised.
        /// </summary>
        /// <param name="mapElement">FrameworkElement containing the rendered map.</param>
        public void CaptureMapSnapshot(FrameworkElement mapElement)
        {
            if (mapElement == null)
            {
                StatusMessage = "Map snapshot failed: element null";
                return;
            }

            try
            {
                // Ensure layout up to date
                mapElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                mapElement.Arrange(new Rect(mapElement.DesiredSize));
                mapElement.UpdateLayout();

                var width = (int)Math.Max(1, mapElement.ActualWidth);
                var height = (int)Math.Max(1, mapElement.ActualHeight);

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(mapElement);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var ms = new System.IO.MemoryStream();
                encoder.Save(ms);
                LatestMapSnapshotPng = ms.ToArray();
                Logger.Information("Captured map snapshot {Width}x{Height} bytes={Bytes}", width, height, LatestMapSnapshotPng.Length);
                StatusMessage = "Map snapshot captured";
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Map snapshot capture failed");
                StatusMessage = "Map snapshot error";
            }
        }

        /// <summary>
        /// Build a route PDF of students already in the system who have coordinates.
        /// For each student: create a RouteStop sequentially ordered. Bus is fixed to #17 (84 passenger) per requirement (placeholder bus object).
        /// Returns tuple(pdfBytes, countEligible, totalConsidered).
        /// </summary>
        public async Task<(byte[] Pdf, int EligibleCount, int Total)> GenerateEligibilityRoutePdfAsync(BusBuddy.Core.Models.RouteTimeSlot slot = BusBuddy.Core.Models.RouteTimeSlot.AM)
        {
            var allStudents = new List<BusBuddy.Core.Models.Student>();
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var studentService = ResolveStudentService(scope);
                if (studentService is null)
                {
                    StatusMessage = "Student service unavailable";
                    return (Array.Empty<byte>(), 0, 0);
                }

                allStudents = await studentService.GetAllStudentsAsync() ?? new();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed loading students for eligibility route PDF");
            }

            if (allStudents.Count == 0)
            {
                return (Array.Empty<byte>(), 0, 0);
            }

            var eligibleStudents = allStudents
                .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
                .ToList();

            if (eligibleStudents.Count == 0)
            {
                Logger.Information("No students with coordinates (Total={Total})", allStudents.Count);
                return (Array.Empty<byte>(), 0, allStudents.Count);
            }

            // ORDER STOPS (Nearest Neighbor heuristic) starting at the district bus barn and ending at the catalog school.
            var (startLat, startLon) = await ResolveRouteStartAnchorAsync();
            var (schoolLat, schoolLon) = await ResolveSchoolAnchorAsync();
            var remaining = eligibleStudents.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
            var ordered = new List<BusBuddy.Core.Models.Student>();
            double currentLat = startLat, currentLon = startLon;
            while (remaining.Count > 0)
            {
                BusBuddy.Core.Models.Student? nearest = null;
                double nearestDist = double.MaxValue;
                foreach (var s in remaining)
                {
                    var dist = HaversineMiles(currentLat, currentLon, (double)s.Latitude!, (double)s.Longitude!);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = s;
                    }
                }
                if (nearest == null) break;
                ordered.Add(nearest);
                currentLat = (double)nearest.Latitude!;
                currentLon = (double)nearest.Longitude!;
                remaining.Remove(nearest);
            }

            // BUILD ROUTE & STOPS WITH SCHEDULE ESTIMATION
            // Assumptions:
            //  • Departure from district bus barn (RoutingDistrict config) when configured.
            //  • Average route speed on county / rural roads: 35 mph (approximation; configurable later).
            //  • Dwell time per stop: 1 minute (boarding + safety check).
            //  • Return to catalog school after last pickup.
            var averageMph = Math.Max(5.0, AverageRouteSpeedMph); // safety floor
            var dwellPerStop = TimeSpan.FromMinutes(Math.Max(0, DwellMinutesPerStop));
            var departTimeOfDay = new TimeSpan(6, 50, 0); // 6:50 AM
            var cumulative = TimeSpan.Zero; // travel + dwell elapsed since departure
            double totalMiles = 0.0;
            var stops = new List<BusBuddy.Core.Models.RouteStop>();
            int order = 1;
            currentLat = startLat; currentLon = startLon;
            foreach (var stu in ordered)
            {
                var legMiles = HaversineMiles(currentLat, currentLon, (double)stu.Latitude!, (double)stu.Longitude!);
                totalMiles += legMiles;
                var travelMinutes = legMiles / averageMph * 60.0;
                cumulative += TimeSpan.FromMinutes(travelMinutes);
                var arrival = departTimeOfDay + cumulative;
                var departure = arrival + dwellPerStop;
                cumulative += dwellPerStop;
                stops.Add(new BusBuddy.Core.Models.RouteStop
                {
                    RouteId = -1,
                    StopOrder = order++,
                    StopName = stu.StudentName ?? "(Student)",
                    Latitude = (decimal?)stu.Latitude,
                    Longitude = (decimal?)stu.Longitude,
                    ScheduledArrival = arrival,
                    ScheduledDeparture = departure,
                    CreatedDate = DateTime.UtcNow
                });
                // Update marker with time in label
                PlotStop((double)stu.Latitude!, (double)stu.Longitude!, new[] { stu.StudentName ?? "Student" }, $"{arrival:hh\\:mm} {stu.StudentName}");
                currentLat = (double)stu.Latitude!;
                currentLon = (double)stu.Longitude!;
            }
            // Return leg to school
            var backLegMiles = HaversineMiles(currentLat, currentLon, schoolLat, schoolLon);
            totalMiles += backLegMiles;
            var backMinutes = backLegMiles / averageMph * 60.0;
            cumulative += TimeSpan.FromMinutes(backMinutes);
            var arrivalBack = departTimeOfDay + cumulative;

            // Build pseudo route (summary metrics could later be embedded in PDF template)
            var route = new RouteModel
            {
                RouteId = -1,
                RouteName = $"Eligibility Route (Auto) {DateTime.Today:MMM d}",
                Date = DateTime.Today,
                IsActive = true,
                WaypointsJson = BuildWaypointsJson(ordered)
            };

            // Placeholder bus & driver per requirement (bus #17 84 passenger). Driver left null.
            var bus = new BusBuddy.Core.Models.Bus
            {
                BusNumber = "17",
                SeatingCapacity = 84,
                Status = "Active"
            };

            byte[]? mapPng = LatestMapSnapshotPng; // may be null if user hasn't printed/captured yet
            byte[] pdf;
            try
            {
                pdf = _pdfReportService.GenerateRouteSummaryReport(route, stops, eligibleStudents, bus, null, slot, mapPng);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "PDF generation failed for eligibility route");
                pdf = Array.Empty<byte>();
            }

            Logger.Information("Student map PDF generated WithCoords={Eligible} Total={Total} Stops={Stops} Miles~{Miles:F1} ETA-Back={EtaBack}", eligibleStudents.Count, allStudents.Count, stops.Count, totalMiles, arrivalBack);
            StatusMessage = $"Student map PDF: {stops.Count} stops ~{totalMiles:F1} mi";
            return (pdf, eligibleStudents.Count, allStudents.Count);
        }

        // UI wrapper made public so MainWindow can trigger it without hosting the MapView
        public async Task GenerateEligibilityRoutePdfAndSaveAsync()
        {
            try
            {
                StatusMessage = "Generating eligibility PDF...";
                var (pdf, eligible, considered) = await GenerateEligibilityRoutePdfAsync();
                // Always ensure the PdfReports folder exists so user can find where output would go even if no data.
                var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PdfReports");
                Directory.CreateDirectory(reportsDir);

                if (pdf.Length == 0)
                {
                    try
                    {
                        var noDataNote = Path.Combine(reportsDir, "NO-DATA.txt");
                        // Overwrite each invocation to reflect latest attempt.
                        File.WriteAllText(noDataNote, $"No eligibility PDF generated at {DateTime.UtcNow:O}. Eligible={eligible} Considered={considered}. This file is created so the folder is visible.\n");
                        Logger.Information("Eligibility PDF skipped (no data). Placeholder NO-DATA.txt written to {Path}", noDataNote);
                    }
                    catch (Exception ioEx)
                    {
                        Logger.Warning(ioEx, "Failed writing NO-DATA.txt placeholder for empty eligibility PDF result");
                    }
                    StatusMessage = "Eligibility PDF: no data";
                    return;
                }

                // Persist PDFs into the dedicated folder under the app base directory: /PdfReports
                var fileName = $"EligibilityRoute-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
                var path = Path.Combine(reportsDir, fileName);
                File.WriteAllBytes(path, pdf);
                LastGeneratedEligibilityPdfPath = path;

                // Optional auto-open (default true). Uses shell execute to open in system default PDF viewer.
                if (UseInternalPdfViewer)
                {
                    try
                    {
                        // Defer to UI thread to open preview window hosting Syncfusion PdfViewerControl
                        _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => // fire-and-forget UI preview (intentional)
                        {
                            try
                            {
                                var preview = new BusBuddy.WPF.Views.Reports.PdfPreviewWindow(path);
                                preview.Show();
                            }
                            catch (Exception exWin)
                            {
                                Logger.Warning(exWin, "Failed opening internal PDF preview window");
                            }
                        }));
                    }
                    catch (Exception exInternal)
                    {
                        Logger.Warning(exInternal, "Internal viewer launch failed, falling back to external open");
                        TryExternalOpen(path);
                    }
                }
                else if (AutoOpenEligibilityPdf)
                {
                    TryExternalOpen(path);
                }

                StatusMessage = $"Saved eligibility PDF ({eligible}/{considered}) -> PdfReports\\{fileName}";
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Eligibility PDF wrapper failed");
                StatusMessage = "Eligibility PDF error";
            }
        }

        // Configuration flag: automatically open generated eligibility PDF in default viewer.
        private bool _autoOpenEligibilityPdf = true;
        public bool AutoOpenEligibilityPdf
        {
            get => _autoOpenEligibilityPdf;
            set
            {
                if (_autoOpenEligibilityPdf != value)
                {
                    _autoOpenEligibilityPdf = value;
                    OnPropertyChanged();
                }
            }
        }

        // When true, opens Syncfusion PdfViewerControl in an internal preview window after generation.
        private bool _useInternalPdfViewer = true;
        public bool UseInternalPdfViewer
        {
            get => _useInternalPdfViewer;
            set
            {
                if (_useInternalPdfViewer != value)
                {
                    _useInternalPdfViewer = value;
                    OnPropertyChanged();
                }
            }
        }

        // Holds the full path to the most recently generated eligibility PDF (for printing from MainWindow or other views)
        private string? _lastGeneratedEligibilityPdfPath;
        public string? LastGeneratedEligibilityPdfPath
        {
            get => _lastGeneratedEligibilityPdfPath;
            private set
            {
                if (_lastGeneratedEligibilityPdfPath != value)
                {
                    _lastGeneratedEligibilityPdfPath = value;
                    OnPropertyChanged();
                }
            }
        }

        private static void TryExternalOpen(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { /* non critical */ }
        }
        /// <summary>
        /// Convert WaypointsJson to a list of System.Windows.Point (Latitude, Longitude).
        /// Supports JSON in the form of [{"Latitude":..,"Longitude":..}, ...] or [[lat,lon], ...].
        /// </summary>
        private static Point[] ParseWaypointsToPoints(string json)
        {
            return RouteWaypointSerializer.Parse(json)
                .Select(p => new Point(p.Latitude, p.Longitude))
                .ToArray();
        }

        /// <summary>
        /// Compute Haversine distance in miles between two geo coordinates (double precision) — documented formula per .NET math usage.
        /// </summary>
        private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 3958.8; // Earth radius miles
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);
            double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
            double c = 2 * Math.Asin(Math.Sqrt(a));
            return R * c;
        }

        private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;

        /// <summary>
        /// Serialize ordered student coordinates to a compact JSON array [[lat,lon], ...] for persistence in Route.WaypointsJson.
        /// </summary>
        private static string BuildWaypointsJson(System.Collections.Generic.IEnumerable<BusBuddy.Core.Models.Student> ordered)
        {
            return RouteWaypointSerializer.FromPairs(
                ordered
                    .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
                    .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value)));
        }

        private void ClearRouteWaypointMarkers()
        {
            for (var i = MapMarkers.Count - 1; i >= 0; i--)
            {
                if (MapMarkers[i].Label?.StartsWith(RouteWaypointPrefix, StringComparison.Ordinal) == true)
                {
                    MapMarkers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Update internal collection and raise event so the view can draw the polyline using Syncfusion MapPolyline.
        /// </summary>
        private async Task UpdatePolylineAsync(System.Collections.Generic.IEnumerable<Point> points)
        {
            await Task.Yield();
            RouteLinePoints.Clear();
            foreach (var p in points)
            {
                RouteLinePoints.Add(p);
            }
            RouteLineUpdated?.Invoke(this, new RouteLineEventArgs(RouteLinePoints));
        }

        private async Task<(double Lat, double Lon)> ResolveRouteStartAnchorAsync()
        {
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var settings = scope?.ServiceProvider.GetService<IOptions<RoutingDistrictSettings>>()?.Value
                    ?? App.ServiceProvider?.GetService<IOptions<RoutingDistrictSettings>>()?.Value;
                if (DistrictDepot.TryGetCoordinates(settings, out var lat, out var lon))
                {
                    return (lat, lon);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "No district depot configured; using school anchor for route start");
            }

            return await ResolveSchoolAnchorAsync();
        }

        private async Task<(double Lat, double Lon)> ResolveSchoolAnchorAsync()
        {
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var dest = scope?.ServiceProvider.GetService<IDestinationService>()
                    ?? App.ServiceProvider?.GetService<IDestinationService>();
                if (dest is null)
                {
                    return (MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude);
                }

                var school = (await dest.GetActiveSchoolsAsync()).FirstOrDefault(s => s.HasGpsCoordinates);
                if (school is null)
                {
                    return (MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude);
                }

                return ((double)school.Latitude!, (double)school.Longitude!);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "No school anchor; using map fallback center");
                return (MapDefaults.FallbackLatitude, MapDefaults.FallbackLongitude);
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
            /// <summary>Syncfusion ImageryLayer marker latitude (official N/S string).</summary>
            public string Latitude { get; set; } = "0.0000N";
            /// <summary>Syncfusion ImageryLayer marker longitude (official E/W string).</summary>
            public string Longitude { get; set; } = "0.0000E";
            public double LatitudeDegrees { get; set; }
            public double LongitudeDegrees { get; set; }

            public static MapMarker FromDegrees(double latitude, double longitude, string? label = null) =>
                new()
                {
                    Label = label,
                    LatitudeDegrees = latitude,
                    LongitudeDegrees = longitude,
                    Latitude = MapCoordinateFormatter.FormatLatitude(latitude),
                    Longitude = MapCoordinateFormatter.FormatLongitude(longitude)
                };

            // Aggregated list of student names for a stop (optional)
            public System.Collections.Generic.List<string> StudentNames { get; } = new();

            /// <summary>
            /// Adds a student name to this marker and updates the label to reflect aggregation.
            /// First student sets the label to their name; multiple students show count and first few names.
            /// </summary>
            public void AddStudent(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                if (!StudentNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    StudentNames.Add(name);
                }

                if (StudentNames.Count == 1)
                {
                    Label = StudentNames[0];
                }
                else
                {
                    // Show up to 3 names then +N more
                    var preview = string.Join(", ", StudentNames.Take(3));
                    if (StudentNames.Count > 3)
                    {
                        Label = $"{StudentNames.Count} students: {preview} +{StudentNames.Count - 3} more";
                    }
                    else
                    {
                        Label = $"{StudentNames.Count} students: {preview}";
                    }
                }
            }
        }

        /// <summary>
        /// Event args carrying route polyline points.
        /// </summary>
        public sealed class RouteLineEventArgs : EventArgs
        {
            public System.Collections.Generic.IReadOnlyList<Point> Points { get; }
            public RouteLineEventArgs(System.Collections.Generic.IEnumerable<Point> points)
            {
                Points = new System.Collections.ObjectModel.ReadOnlyCollection<Point>(new System.Collections.Generic.List<Point>(points));
            }
        }
    }

}
