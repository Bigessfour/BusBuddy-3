using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.WPF.Commands;
using CommunityToolkit.Mvvm.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;
using System.Windows; // For System.Windows.Point used by Syncfusion MapPolyline
using System.Collections.Generic; // For generic collections
using System.Linq; // For LINQ operations
using System.Windows.Media; // For VisualTreeHelper during snapshot
using System.Windows.Media.Imaging; // For RenderTargetBitmap / PngBitmapEncoder (Microsoft WPF docs: Imaging)
using System.IO; // Map snapshot PNG encoding
using BusBuddy.WPF;

namespace BusBuddy.WPF.ViewModels.Map
{
    /// <summary>
    /// ViewModel for the Syncfusion SfMap surface (Google Map Tiles + Maps Platform geo).
    /// Plots student addresses, school destinations, and route trails/waypoints.
    /// Fleet GPS / AVL is not wired — the map shows a status line instead of live-tracking chrome.
    /// </summary>
    public class MapViewModel : BaseViewModel
    {
        private readonly IGeoDataService _geoDataService;
        private readonly IRoutingService? _routingService;
        private readonly BusBuddy.Core.Services.PdfReportService _pdfReportService = new(); // Lightweight stateless service
        private readonly BusBuddy.Core.Services.IStudentService? _studentService; // If available for pulling students
        private readonly IServiceScopeFactory? _scopeFactory;
        private readonly IDistrictSettingsAccessor? _districtSettings;
        private readonly MapRouteTrail _trail;
        private readonly MapDistrictLayers _layers;
        // Serilog logger with enrichments for this ViewModel
        private static readonly new Serilog.ILogger Logger = Serilog.Log.ForContext<MapViewModel>();

        private ObservableCollection<RouteModel> _routes = new();
        private RouteModel? _selectedRoute;
        private MapMarker? _selectedMarker;
        private bool _isMapLoading;
        private bool _eligibilityPdfBusy;
        private bool _clerkOverrideBusy;
        private string _statusMessage = "Ready";
        private byte[]? _latestMapSnapshotPng; // Holds last captured map snapshot (PNG bytes) for PDF embedding
        private byte[]? _lastGeneratedEligibilityPdf;
        private IAsyncRelayCommand _generateEligibilityPdfRelay = null!;
        private IAsyncRelayCommand _applyClerkOverrideRelay = null!;
        /// <summary>Lamar/Wiley clerk default per <c>specs/maps.md</c> — not the US-centroid overview.</summary>
        private const double DistrictDefaultLatitude = 38.0872;
        private const double DistrictDefaultLongitude = -102.6208;
        private Point _mapCenter = new(DistrictDefaultLatitude, DistrictDefaultLongitude);
        private int _mapZoomLevel = MapDefaults.DistrictZoomLevel;
        private Size _mapViewportSize = new(MapDefaults.DefaultViewportWidth, MapDefaults.DefaultViewportHeight);
        private const string RouteWaypointPrefix = MapRouteTrail.WaypointPrefix;

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

        // Map interaction events (view listens and applies actual SfMap changes).
        // Zoom and center flow through MapZoomLevel/MapCenter PropertyChanged, not events.
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

        public MapViewModel(
            IGeoDataService geoDataService,
            IGeocodingService? geocodingService = null,
            BusBuddy.Core.Services.IStudentService? studentService = null,
            IServiceScopeFactory? scopeFactory = null,
            IRoutingService? routingService = null,
            IPickupStopService? pickupStops = null,
            IDestinationService? destinations = null,
            IDistrictSettingsAccessor? districtSettings = null)
        {
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));
            _routingService = routingService;
            _studentService = studentService;
            _scopeFactory = scopeFactory;
            _districtSettings = districtSettings;
            _trail = new MapRouteTrail(_routingService, _scopeFactory);
            _layers = new MapDistrictLayers(
                pickupStops,
                destinations,
                studentService,
                geocodingService,
                scopeFactory,
                (lat, lon, names, label, ids) => PlotStop(lat, lon, names, label, studentIds: ids),
                ResolveDepotMarker);

            LoadRoutesCommand = new AsyncRelayCommand(LoadRoutesAsync);
            RefreshMapCommand = new AsyncRelayCommand(RefreshMapAsync);
            ExportRouteDataCommand = new AsyncRelayCommand(ExportRouteDataAsync);
            ZoomInCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ZoomOut());

            // Commands referenced by XAML (map toolbar)
            CenterOnFleetCommand = new AsyncRelayCommand(CenterOnFleetAsync);
            ShowRoutesCommand = new AsyncRelayCommand(ShowRoutesAsync);
            ShowSchoolsCommand = new AsyncRelayCommand(ShowSchoolsAsync);
            PlotPickupStopsCommand = new AsyncRelayCommand(PlotPickupStopsAsync);
            ResetViewCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ResetView());

            // Print current route map/directions
            PrintRouteMapsCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => OnPrintRequested(), _ => true);

            // Eligibility route PDF generation
            _generateEligibilityPdfRelay = new AsyncRelayCommand(
                GenerateEligibilityRoutePdfAndPreviewAsync,
                () => !_eligibilityPdfBusy);
            GenerateEligibilityRoutePdfCommand = _generateEligibilityPdfRelay;

            // Add marker (stop) plotting command. Accepts parameter forms documented in AddMarkerFromParam.
            AddMarkerCommand = new BusBuddy.WPF.Commands.RelayCommand(p => AddMarkerFromParam(p));
            BulkPlotEligibleStudentsCommand = new AsyncRelayCommand(BulkPlotEligibleStudentsAsync);
            _applyClerkOverrideRelay = new AsyncRelayCommand(
                ApplyClerkOverrideFromMapAsync,
                CanApplyClerkOverrideFromMap);
            ApplyClerkOverrideCommand = _applyClerkOverrideRelay;

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
                $"{result.UnclusteredStudentIds.Count} unclustered — select a student pin and a Draft-* route, then Move to selected route";

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
        /// Center point for the imagery layer (latitude = X, longitude = Y per Syncfusion).
        /// Public setter required for TwoWay ZoomLevel/Center bindings.
        /// </summary>
        public Point MapCenter
        {
            get => _mapCenter;
            set => SetProperty(ref _mapCenter, value);
        }

        /// <summary>
        /// Zoom level bound to SfMap.ZoomLevel (TwoWay). Clamped to the imagery layer's 1..19 range so a
        /// wheel zoom never round-trips to a different value. Camera is Center + ZoomLevel only — the
        /// Syncfusion <c>Radius</c> fit is not used (it doubles the bounds and re-fits on every resize).
        /// </summary>
        public int MapZoomLevel
        {
            get => _mapZoomLevel;
            set
            {
                if (SetProperty(ref _mapZoomLevel, MapDefaults.ClampZoom(value)))
                {
                    OnPropertyChanged(nameof(ShowDetailLabels));
                    RefreshMarkerZoomVisuals();
                }
            }
        }

        /// <summary>
        /// Place captions (school / pickup / depot / route stop) render from
        /// <see cref="MapDefaults.DetailLabelZoomLevel"/> up; household captions (home / student)
        /// wait for <see cref="MapDefaults.HomeLabelZoomLevel"/> (see <see cref="MapMarkerLabels.ShowsCaption"/>).
        /// Templates bind marker <c>ShowCaption</c> (avoids RelativeSource breaks).
        /// </summary>
        public bool ShowDetailLabels => MapDefaults.ShowsDetailLabels(MapZoomLevel);

        private void RefreshMarkerZoomVisuals()
        {
            foreach (var marker in MapMarkers)
            {
                marker.ApplyZoomVisuals(MapZoomLevel);
            }
        }

        /// <summary>
        /// SfMap pixel size reported by the view (SizeChanged) so span fits use the real viewport.
        /// Falls back to <see cref="MapDefaults.DefaultViewportWidth"/> x <see cref="MapDefaults.DefaultViewportHeight"/>.
        /// </summary>
        public Size MapViewportSize
        {
            get => _mapViewportSize;
            set
            {
                if (value.IsEmpty || value.Width <= 0 || value.Height <= 0
                    || double.IsNaN(value.Width) || double.IsNaN(value.Height))
                {
                    return;
                }

                SetProperty(ref _mapViewportSize, value);
            }
        }

        /// <summary>
        /// Updates map center and optional zoom for the TwoWay SfMap / ImageryLayer bindings.
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

        /// <summary>Legend rows (kind name + pin colour) for the left panel — single source in <see cref="MapMarkerLabels"/>.</summary>
        public IReadOnlyList<MapMarkerLabels.LegendEntry> MarkerLegend => MapMarkerLabels.Legend;

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
                    OnSelectedRouteChanged();
                }
            }
        }

        /// <summary>Pin the clerk last clicked on the District Map (student override source).</summary>
        public MapMarker? SelectedMarker
        {
            get => _selectedMarker;
            private set => SetProperty(ref _selectedMarker, value);
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
        public ICommand ShowRoutesCommand { get; private set; } = null!;
        public ICommand ShowSchoolsCommand { get; private set; } = null!;
        public ICommand PlotPickupStopsCommand { get; private set; } = null!;
        public ICommand ResetViewCommand { get; private set; } = null!;
        public ICommand AddMarkerCommand { get; private set; } = null!;
        public ICommand PrintRouteMapsCommand { get; private set; } = null!;
        public ICommand GenerateEligibilityRoutePdfCommand { get; private set; } = null!; // New command to trigger eligibility PDF generation
        public ICommand BulkPlotEligibleStudentsCommand { get; private set; } = null!;
        public ICommand ApplyClerkOverrideCommand { get; private set; } = null!;

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

        private void OnSelectedRouteChanged()
        {
            NotifyClerkOverrideCanExecute();
            try
            {
                if (SelectedRoute is null)
                {
                    Logger.Information("Selected route cleared");
                    StatusMessage = "Select a route to display";
                    _ = UpdateMapForRouteAsync(null);
                    return;
                }

                Logger.Information("Selected route changed to: {RouteName}", SelectedRoute.RouteName ?? "Unknown");
                StatusMessage = $"Selected: {SelectedRoute.RouteName ?? "Unknown Route"}";
                _ = UpdateMapForRouteAsync(SelectedRoute);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error handling route selection change");
            }
        }

        /// <summary>
        /// Sets the combo selection without starting a second draw (init / Show Routes await the plot).
        /// </summary>
        private void BindSelectedRoute(RouteModel? route)
        {
            _selectedRoute = route;
            OnPropertyChanged(nameof(SelectedRoute));
            NotifyClerkOverrideCanExecute();
        }

        /// <summary>Called from <c>ImageryLayer.MarkerSelected</c> after Syncfusion unwraps the pin.</summary>
        public void SelectMapMarker(MapMarker? marker)
        {
            SelectedMarker = marker;
            NotifyClerkOverrideCanExecute();
            if (marker is null)
            {
                return;
            }

            if (marker.StudentIds.Count == 1)
            {
                StatusMessage = "Student pin selected — pick a route and Move to selected route";
                return;
            }

            if (marker.StudentIds.Count > 1)
            {
                StatusMessage =
                    $"{marker.StudentIds.Count} students on this pin — Move to selected route moves all of them";
            }
        }

        private bool CanApplyClerkOverrideFromMap() =>
            !_clerkOverrideBusy
            && SelectedRoute is not null
            && SelectedMarker is not null
            && SelectedMarker.StudentIds.Count > 0;

        private void NotifyClerkOverrideCanExecute() =>
            _applyClerkOverrideRelay?.NotifyCanExecuteChanged();

        /// <summary>
        /// Moves the selected student pin onto <see cref="SelectedRoute"/> via
        /// <see cref="IRouteDeterminationService.ApplyClerkOverrideAsync"/> (spec 008 FR-009).
        /// Slot comes from the selected row's AM/PM identity, not a third session model.
        /// </summary>
        private async Task ApplyClerkOverrideFromMapAsync()
        {
            var target = SelectedRoute;
            var marker = SelectedMarker;
            if (target is null || marker is null || marker.StudentIds.Count == 0)
            {
                StatusMessage = "Select a student pin and a destination route, then Move to selected route.";
                return;
            }

            if (_scopeFactory is null)
            {
                StatusMessage = "Route planner unavailable";
                return;
            }

            _clerkOverrideBusy = true;
            NotifyClerkOverrideCanExecute();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var planner = scope.ServiceProvider.GetService<IRouteDeterminationService>();
                var students = scope.ServiceProvider.GetService<IStudentService>();
                if (planner is null)
                {
                    StatusMessage = "Route planner unavailable";
                    return;
                }

                var assignmentSlot = RouteSession.ToAssignmentSlot(target);
                var kind = assignmentSlot == RouteTimeSlot.PM
                    ? RouteTimeSlotKind.PM
                    : RouteTimeSlotKind.AM;

                var moved = 0;
                string? firstError = null;
                foreach (var studentId in marker.StudentIds.Distinct())
                {
                    var student = students is null
                        ? null
                        : await students.GetStudentByIdAsync(studentId).ConfigureAwait(true);
                    if (student is not null
                        && StudentRouteAssignment.Matches(student, target, assignmentSlot))
                    {
                        continue;
                    }

                    var fromId = 0;
                    if (student is not null)
                    {
                        fromId = assignmentSlot == RouteTimeSlot.PM
                            ? student.PmRouteId ?? 0
                            : student.AmRouteId ?? 0;
                    }

                    var result = await planner.ApplyClerkOverrideAsync(
                            studentId,
                            fromId,
                            target.RouteId,
                            kind,
                            "District Map")
                        .ConfigureAwait(true);
                    if (result.Success)
                    {
                        moved++;
                    }
                    else
                    {
                        firstError ??= result.Error ?? $"Could not move student {studentId}";
                    }
                }

                if (moved > 0 && firstError is null)
                {
                    StatusMessage = $"Moved {moved} rider(s) onto {target.RouteName}";
                }
                else if (moved > 0)
                {
                    StatusMessage = $"Moved {moved}; others failed: {firstError}";
                }
                else if (firstError is not null)
                {
                    StatusMessage = firstError;
                }
                else
                {
                    StatusMessage = $"Already on {target.RouteName}";
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Clerk map override failed");
                StatusMessage = "Could not apply route override";
            }
            finally
            {
                _clerkOverrideBusy = false;
                NotifyClerkOverrideCanExecute();
            }
        }

        private async Task RefreshMapAsync()
        {
            try
            {
                IsMapLoading = true;
                StatusMessage = "Refreshing map...";

                // Restore the full district overlay (undoes Show Schools' schools-only view; same-kind pins merge).
                await _layers.LoadDistrictLayersAsync();

                if (SelectedRoute is not null)
                {
                    Logger.Information("Refreshing map for route: {RouteName}", SelectedRoute.RouteName ?? "Unknown");
                    await UpdateMapForRouteAsync(SelectedRoute, refreshDrivePath: true);
                }
                else
                {
                    await LoadAllRoutesOnMapAsync();
                    StatusMessage = "Map refreshed";
                }
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

        /// <summary>
        /// One load after routes: depot → schools → active pickups → students with stored coords (no network),
        /// then refresh the selected route drive path.
        /// </summary>
        private async Task InitializeMapDataAsync()
        {
            Logger.Information("InitializeMapDataAsync starting — routes, district layers, then drive path");
            try
            {
                await LoadRoutesAsync();

                // Fixed order — no geocode on this path.
                var depots = _layers.PlotDepotPins();
                var schools = await _layers.PlotSchoolsAsync();
                var pickups = await _layers.PlotPickupsAsync();
                var students = await _layers.PlotStoredStudentsAsync();
                var seeded = new MapLayerSeedCounts(schools, pickups, students, depots);

                var routeWithTrail = Routes.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.WaypointsJson));
                if (routeWithTrail is not null)
                {
                    // Drive path last — owns camera when a trail exists.
                    BindSelectedRoute(routeWithTrail);
                    await UpdateMapForRouteAsync(routeWithTrail, refreshDrivePath: true);
                }
                else if (MapMarkers.Count > 0)
                {
                    CenterOnMarkers();
                }
                else
                {
                    var (lat, lon, zoom) = await ResolveDistrictCameraAsync();
                    SetMapView(lat, lon, zoom);
                }

                StatusMessage =
                    $"Map ready — {seeded.Schools} school(s), {seeded.Pickups} pickup(s), {seeded.Students} student(s), {seeded.Depots} depot(s)";
                Logger.Information(
                    "InitializeMapDataAsync completed Routes={RouteCount} Markers={MarkerCount} Schools={Schools} Pickups={Pickups} Students={Students} Depots={Depots} Trail={HasTrail}",
                    Routes.Count,
                    MapMarkers.Count,
                    seeded.Schools,
                    seeded.Pickups,
                    seeded.Students,
                    seeded.Depots,
                    routeWithTrail is not null);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "InitializeMapDataAsync failed");
            }
        }

        private BusBuddy.Core.Services.IStudentService? ResolveStudentService(IServiceScope? scope) =>
            _studentService ?? scope?.ServiceProvider.GetService<BusBuddy.Core.Services.IStudentService>();

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

                BindSelectedRoute(withWaypoints);
                // Refresh drive path via Google Routes when keyed (fail-open to stored geometry).
                await UpdateMapForRouteAsync(withWaypoints, refreshDrivePath: true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "LoadAllRoutesOnMapAsync overlay failed");
            }
        }

        /// <summary>
        /// Loads all students, geocodes missing home coordinates, and plots markers.
        /// Catalog pickup stops win over home GPS.
        /// </summary>
        private async Task BulkPlotEligibleStudentsAsync()
        {
            try
            {
                StatusMessage = "Loading students...";
                var result = await _layers.BulkPlotStudentsAsync();
                StatusMessage = result.Status;
                if (result.Plotted > 0)
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

        private async Task UpdateMapForRouteAsync(RouteModel? route, bool refreshDrivePath = false)
        {
            var generation = _trail.BeginDraw();
            var routeName = route?.RouteName ?? "Unknown";
            try
            {
                if (route is not null)
                {
                    await EnsureRouteWaypointsAsync(route).ConfigureAwait(true);
                    if (!_trail.IsCurrent(generation))
                    {
                        return;
                    }
                }

                MapRouteTrailPersist persist = default;
                if (refreshDrivePath && route is not null && !string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    persist = await _trail.RefreshStoredPathAsync(route).ConfigureAwait(true);
                    if (!_trail.IsCurrent(generation))
                    {
                        return;
                    }
                }

                if (!_trail.IsCurrent(generation))
                {
                    return;
                }

                var plot = MapRouteTrail.Build(route);
                await UpdatePolylineAsync(plot.Line);
                if (!_trail.IsCurrent(generation))
                {
                    return;
                }

                ClearRouteWaypointMarkers();
                for (var i = 0; i < plot.Markers.Count; i++)
                {
                    var stop = plot.Markers[i];
                    PlotStop(
                        stop.Latitude,
                        stop.Longitude,
                        null,
                        MapRouteTrail.MarkerLabel(i, plot.Markers.Count),
                        MapMarkerLabels.Kind.Waypoint);
                }

                if (plot.Line.Count >= 2)
                {
                    CenterOnPoints(plot.Line);
                }
                else if (plot.Markers.Count > 0)
                {
                    CenterOnPoints(plot.Markers.Select(s => new Point(s.Latitude, s.Longitude)));
                }

                StatusMessage = persist.Computed && !persist.Persisted && !string.IsNullOrWhiteSpace(persist.Message)
                    ? persist.Message
                    : plot.StatusMessage;
                Logger.Information(
                    "Map updated for route: {RouteName} LinePoints={Line} Stops={Stops} Refresh={Refresh}",
                    routeName,
                    plot.Line.Count,
                    plot.Markers.Count,
                    refreshDrivePath);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed to update map for route {RouteName}", routeName);
            }
        }

        /// <summary>
        /// Stop-derived JSON is persisted in <see cref="IGeoDataService"/>. If the column is still empty
        /// at draw time, rebuild from assigned students (never overwrite stored geometry).
        /// </summary>
        private async Task EnsureRouteWaypointsAsync(RouteModel route)
        {
            if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
            {
                return;
            }

            try
            {
                var loaded = await _geoDataService.GetRouteGeoDataAsync(route.RouteId).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(loaded?.WaypointsJson))
                {
                    route.WaypointsJson = loaded.WaypointsJson;
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Ensure waypoints via GeoDataService failed RouteId={RouteId}", route.RouteId);
            }

            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var rebuild = scope?.ServiceProvider.GetService<IRouteWaypointRebuildService>();
                if (rebuild is null)
                {
                    return;
                }

                var json = await rebuild.RebuildAndPersistAsync(route.RouteId).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    route.WaypointsJson = json;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Ensure waypoints via rebuild failed RouteId={RouteId}", route.RouteId);
            }
        }

        /// <summary>
        /// Export Route is always clickable. Without a selection the clerk gets told what to do instead of
        /// a greyed-out button; with one, the Save dialog opens and the outcome lands in a toast.
        /// </summary>
        private async Task ExportRouteDataAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route to export";
                UserToast.Warning("Pick a route in the Route list, then press Export Route.", "Export Route");
                return;
            }

            try
            {
                var result = await MapRouteExporter
                    .ExportSelectedAsync(_geoDataService, SelectedRoute)
                    .ConfigureAwait(true);
                StatusMessage = result.Message;
                if (result.Success)
                {
                    UserToast.Success(result.Message, "Export Route");
                }
                else if (!result.Cancelled)
                {
                    UserToast.Warning(result.Message, "Export Route");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Route GeoJSON export failed RouteId={RouteId}", SelectedRoute?.RouteId);
                StatusMessage = "Could not export route";
                UserToast.Error("Could not export the route — see logs.", "Export Route");
            }
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

        private void ZoomIn() => StepZoom(+1);

        private void ZoomOut() => StepZoom(-1);

        /// <summary>Zoom around the current center; only <see cref="MapZoomLevel"/> changes so one tile reload runs.</summary>
        private void StepZoom(int delta)
        {
            var next = MapDefaults.ClampZoom(MapZoomLevel + delta);
            if (next == MapZoomLevel)
            {
                StatusMessage = delta > 0 ? "Already at maximum zoom" : "Already at minimum zoom";
                return;
            }

            MapZoomLevel = next;
            StatusMessage = $"Zoom level {next}";
            Logger.Debug("Map zoom {Direction} to {Zoom}", delta > 0 ? "in" : "out", next);
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

                var (lat, lon, zoom) = await ResolveDistrictCameraAsync();
                SetMapView(lat, lon, zoom);
                StatusMessage = "Centered on district — fleet GPS is not enabled";
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
            var list = points
                .Where(p => IsPlottableCoordinate(p.X, p.Y))
                .ToList();
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

            // Fit against the real SfMap viewport (view reports it); SfMap has no fit-bounds API.
            SetMapView(
                (minLat + maxLat) / 2d,
                (minLon + maxLon) / 2d,
                MapDefaults.ZoomForBounds(
                    minLat,
                    maxLat,
                    minLon,
                    maxLon,
                    MapViewportSize.Width,
                    MapViewportSize.Height));
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

        /// <summary>
        /// Schools-only view: every non-school pin is removed, then schools with validated coordinates are
        /// (re)plotted and fitted. Refresh restores the full district overlay.
        /// </summary>
        private async Task ShowSchoolsAsync()
        {
            StatusMessage = "Showing schools only...";
            Logger.Information("Show schools (schools-only view) requested");
            try
            {
                ClearMarkersExcept(MapMarkerLabels.Kind.School);
                var plotted = await _layers.PlotSchoolsAsync();
                if (plotted == 0)
                {
                    StatusMessage = "No schools with validated coordinates (needs validation)";
                    UserToast.Warning("No school has a validated address yet. Add one under Students → Schools.", "Show Schools");
                    return;
                }

                CenterOnMarkers();
                StatusMessage = $"Schools only — {plotted} school(s). Refresh restores all layers";
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowSchools failed");
                StatusMessage = "Could not show schools";
            }
        }

        /// <summary>
        /// Boarding points the clerk publishes: catalog pickup stops plus the stops on every route in the combo.
        /// Generated routes carry their stops as <c>RouteStop</c> rows, so a district with an empty catalog
        /// still gets pins here instead of a silent no-op.
        /// </summary>
        private async Task PlotPickupStopsAsync()
        {
            StatusMessage = "Showing pickup stops...";
            Logger.Information("Plot pickup stops requested");
            try
            {
                var catalog = await _layers.PlotPickupsAsync();
                var published = await _layers.PlotRouteStopsAsync(Routes.Select(r => r.RouteId).ToList());
                var plotted = catalog + published;
                if (plotted == 0)
                {
                    StatusMessage = "No pickup stops with validated coordinates yet";
                    UserToast.Warning(
                        "No catalog stop or route stop has validated coordinates. Add stops under Students → Pickup Stops, or generate routes first.",
                        "Plot Pickup Stops");
                    return;
                }

                CenterOnMarkers();
                StatusMessage = $"Showing {plotted} pickup stop(s) — {catalog} catalog, {published} on published routes";
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "PlotPickupStops failed");
                StatusMessage = "Could not show pickup stops";
            }
        }

        /// <summary>Reset camera to school, then depot/bbox, then Lamar/Wiley clerk default.</summary>
        public async Task ResetCameraToDistrictAsync()
        {
            try
            {
                var (lat, lon, zoom) = await ResolveDistrictCameraAsync();
                SetMapView(lat, lon, zoom);
                ViewResetRequested?.Invoke(this, EventArgs.Empty);
                StatusMessage = IsDistrictDefaultCamera(lat, lon, zoom)
                    ? "District map — Lamar/Wiley area (add a school or bus barn in Settings to refine)"
                    : "Map view reset";
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ResetCameraToDistrict failed");
                StatusMessage = "Could not reset map";
            }
        }

        /// <summary>
        /// After Settings persist depot/bbox: replot the DEPOT pin and recenter.
        /// Never leaves the camera on the MapDefaults US-centroid overview.
        /// </summary>
        public async Task ApplyDistrictSettingsAsync()
        {
            try
            {
                ClearDepotMarkers();
                var depotCount = _layers.PlotDepotPins();
                await ResetCameraToDistrictAsync().ConfigureAwait(true);
                Logger.Information(
                    "District map refreshed after Settings write DepotMarkers={DepotCount} CenterLat={Lat:F4} CenterLon={Lon:F4} Zoom={Zoom}",
                    depotCount,
                    MapCenter.X,
                    MapCenter.Y,
                    MapZoomLevel);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ApplyDistrictSettings failed");
                StatusMessage = "Could not refresh district map after Settings save";
            }
        }

        private void ResetView()
        {
            StatusMessage = "Resetting map view...";
            Logger.Information("Reset view requested");
            try
            {
                RouteLinePoints.Clear();
                RouteLineUpdated?.Invoke(this, new RouteLineEventArgs(RouteLinePoints));
                ClearRouteWaypointMarkers();
                _ = ResetCameraToDistrictAsync();
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
        /// <param name="kind">Marker kind (SCH/PK/HOME/WP/DEPOT). Defaults from <paramref name="label"/> prefix.</param>
        /// <param name="studentIds">Optional roster keys so clerk override does not match pins by name.</param>
        public MapMarker PlotStop(
            double latitude,
            double longitude,
            IEnumerable<string>? studentNames = null,
            string? label = null,
            MapMarkerLabels.Kind? kind = null,
            IEnumerable<int>? studentIds = null)
        {
            if (!IsPlottableCoordinate(latitude, longitude))
            {
                Logger.Warning(
                    "Skipped unvalidated map pin at ({Lat}, {Lon}) Label={Label}",
                    latitude,
                    longitude,
                    label ?? "<none>");
                return MapMarker.FromDegrees(DistrictDefaultLatitude, DistrictDefaultLongitude, label, kind, MapZoomLevel);
            }

            var incomingKind = kind ?? MapMarkerLabels.GetKind(label);
            if (incomingKind == MapMarkerLabels.Kind.Waypoint
                && TryTagRouteStop(latitude, longitude, label) is { } tagged)
            {
                AddStudents(tagged, studentNames, studentIds);
                return tagged;
            }

            // Same kind + same spot only — never merge SCH/PK/HOME/DEPOT/WP across kinds.
            var existing = MapMarkers.FirstOrDefault(m =>
                StudentPlotLocation.SameSpot(m.LatitudeDegrees, m.LongitudeDegrees, latitude, longitude)
                && MapMarkerLabels.CanMerge(m.Kind, incomingKind));
            if (existing is null)
            {
                existing = MapMarker.FromDegrees(latitude, longitude, label, incomingKind, MapZoomLevel);
                ApplyMarkerStyle(existing, incomingKind);
                MapMarkers.Add(existing);
                Logger.Information(
                    "Added marker Kind={Kind} at ({Lat}, {Lon}) Label={Label}",
                    incomingKind,
                    latitude,
                    longitude,
                    label ?? "<auto>");
                AddStudents(existing, studentNames, studentIds);
                NotifyMapMarkersChanged();
                return existing;
            }

            var mutated = false;
            if (MapMarkerLabels.ShouldReplaceLabel(existing.Label, label, existing.Kind, incomingKind))
            {
                existing.Label = label;
                ApplyMarkerStyle(existing, incomingKind);
                mutated = true;
            }

            var beforeIds = existing.StudentIds.Count;
            var beforeNames = existing.StudentNames.Count;
            AddStudents(existing, studentNames, studentIds);
            if (existing.StudentIds.Count != beforeIds || existing.StudentNames.Count != beforeNames)
            {
                mutated = true;
            }

            if (mutated)
            {
                NotifyMapMarkersChanged();
            }

            return existing;
        }

        /// <summary>
        /// A route stop that lands on an existing school / stop / home pin becomes a sequence tag on that pin
        /// ("Lamar High School (Stop 7)") instead of a second WP marker whose caption overprints the first.
        /// Returns null when no other pin sits there, so the caller plots a standalone gold route-stop pin.
        /// </summary>
        private MapMarker? TryTagRouteStop(double latitude, double longitude, string? label)
        {
            var host = MapMarkers.FirstOrDefault(m =>
                m.Kind != MapMarkerLabels.Kind.Waypoint
                && StudentPlotLocation.SameSpot(m.LatitudeDegrees, m.LongitudeDegrees, latitude, longitude));
            if (host is null)
            {
                return null;
            }

            host.RouteStopLabel = label;
            host.ApplyZoomVisuals(MapZoomLevel);
            NotifyMapMarkersChanged();
            return host;
        }

        /// <summary>
        /// Plot a selected trip only when origin/destination coordinates are validated.
        /// No 0,0 or US-centroid fallback pins (specs/trips.md, specs/maps.md).
        /// Drive path uses Google Routes via <see cref="IRoutingService"/> — not MappingService.
        /// </summary>
        public int TryPlotTrip(TripEvent trip)
        {
            ArgumentNullException.ThrowIfNull(trip);

            var plotted = TryPlotTripPins(trip);
            _ = PlotTripPathAsync(trip);
            return plotted;
        }

        public async Task<int> TryPlotTripAsync(TripEvent trip)
        {
            ArgumentNullException.ThrowIfNull(trip);
            var plotted = TryPlotTripPins(trip);
            await PlotTripPathAsync(trip).ConfigureAwait(true);
            return plotted;
        }

        private int TryPlotTripPins(TripEvent trip)
        {
            var plotted = 0;
            plotted += TryPlotTripPlace(
                trip.OriginLocation,
                trip.OriginName,
                MapMarkerLabels.Kind.School);
            plotted += TryPlotTripPlace(
                trip.DestinationLocation,
                trip.DestinationName ?? trip.Destination,
                MapMarkerLabels.Kind.Waypoint);

            if (plotted == 0)
            {
                StatusMessage = "Trip has no validated coordinates; not plotted.";
                Logger.Information(
                    "Skipped trip pin Ticket={Ticket} — coordinates missing or unvalidated",
                    trip.ExternalTicketNo ?? trip.TripEventId.ToString());
            }

            return plotted;
        }

        private async Task PlotTripPathAsync(TripEvent trip)
        {
            try
            {
                if (_routingService is null || !trip.HasValidatedOrigin || !trip.HasValidatedDestination)
                {
                    return;
                }

                var origin = (
                    (double)trip.OriginLocation!.Latitude!.Value,
                    (double)trip.OriginLocation.Longitude!.Value);
                var dest = (
                    (double)trip.DestinationLocation!.Latitude!.Value,
                    (double)trip.DestinationLocation.Longitude!.Value);
                var path = await _routingService.ComputeDrivePathAsync(
                    origin,
                    dest,
                    Array.Empty<(double, double)>()).ConfigureAwait(true);

                IReadOnlyList<(double Latitude, double Longitude)> vertices = path.Points.Count >= 2
                    ? path.Points
                    : EncodedPolylineCodec.Decode(path.EncodedPolyline);
                if (vertices.Count < 2)
                {
                    return;
                }

                await UpdatePolylineAsync(vertices.Select(p => new Point(p.Latitude, p.Longitude)))
                    .ConfigureAwait(true);
                StatusMessage = trip.PathMiles.HasValue
                    ? $"Trip path {trip.PathMiles.Value:0.00} miles"
                    : "Trip path plotted";
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Trip path plot failed Ticket={Ticket}",
                    trip.ExternalTicketNo ?? trip.TripEventId.ToString());
            }
        }

        private int TryPlotTripPlace(Destination? place, string? name, MapMarkerLabels.Kind kind)
        {
            if (place is null || !place.HasValidatedCoordinates)
            {
                return 0;
            }

            var latitude = (double)place.Latitude!.Value;
            var longitude = (double)place.Longitude!.Value;
            if (!IsPlottableCoordinate(latitude, longitude))
            {
                return 0;
            }

            var label = kind == MapMarkerLabels.Kind.School
                ? MapMarkerLabels.ForSchool(name ?? place.Name)
                : MapMarkerLabels.WaypointPrefix + (name ?? place.Name);
            PlotStop(latitude, longitude, null, label, kind);
            return 1;
        }

        private static void ApplyMarkerStyle(MapMarker marker, MapMarkerLabels.Kind kind, int zoomLevel)
        {
            marker.Kind = kind;
            marker.ApplyZoomVisuals(zoomLevel);
        }

        private void ApplyMarkerStyle(MapMarker marker, MapMarkerLabels.Kind kind) =>
            ApplyMarkerStyle(marker, kind, MapZoomLevel);

        private static void AddStudents(MapMarker marker, IEnumerable<string>? names, IEnumerable<int>? ids = null)
        {
            var nameList = names?
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList() ?? [];
            var idList = ids?.Where(id => id > 0).Distinct().ToList() ?? [];
            if (nameList.Count == 0 && idList.Count == 0)
            {
                return;
            }

            var count = Math.Max(nameList.Count, idList.Count);
            for (var i = 0; i < count; i++)
            {
                var name = i < nameList.Count
                    ? nameList[i]
                    : $"Student {idList[i]}";
                int? id = i < idList.Count ? idList[i] : null;
                marker.AddStudent(name, id);
            }
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
                    PlotStop(MapCenter.X, MapCenter.Y, null, "New Stop");
                    return;
                }

                switch (param)
                {
                    case MapMarker mm:
                        PlotStop(mm.LatitudeDegrees, mm.LongitudeDegrees, mm.StudentNames, mm.Label, studentIds: mm.StudentIds);
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

        private readonly record struct EligibilityPdfBuild(
            byte[] Pdf,
            int MappedCount,
            int Total,
            string? Blocker);

        /// <summary>
        /// Build a route PDF of students who already have a map pin (catalog stop or validated home).
        /// Stops are nearest-neighbor ordered from the bus barn. Does not mutate map markers.
        /// </summary>
        public async Task<(byte[] Pdf, int EligibleCount, int Total)> GenerateEligibilityRoutePdfAsync(
            BusBuddy.Core.Models.RouteTimeSlot slot = BusBuddy.Core.Models.RouteTimeSlot.AM)
        {
            var built = await BuildEligibilityRoutePdfAsync(slot);
            return (built.Pdf, built.MappedCount, built.Total);
        }

        private async Task<EligibilityPdfBuild> BuildEligibilityRoutePdfAsync(BusBuddy.Core.Models.RouteTimeSlot slot)
        {
            List<BusBuddy.Core.Models.Student> allStudents;
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var studentService = ResolveStudentService(scope);
                if (studentService is null)
                {
                    StatusMessage = "Student service unavailable";
                    return new EligibilityPdfBuild(Array.Empty<byte>(), 0, 0, "Student records are not available.");
                }

                allStudents = await studentService.GetAllStudentsAsync() ?? new();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed loading students for eligibility route PDF");
                StatusMessage = "Student map PDF: could not load students";
                return new EligibilityPdfBuild(
                    Array.Empty<byte>(),
                    0,
                    0,
                    "Could not load students. Check the database connection.");
            }

            if (allStudents.Count == 0)
            {
                StatusMessage = "Student map PDF: no students";
                return new EligibilityPdfBuild(Array.Empty<byte>(), 0, 0, "There are no students to map.");
            }

            IReadOnlyDictionary<int, PickupStop> pickups;
            try
            {
                pickups = await _layers.LoadPickupIndexAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Pickup catalog unavailable for student map PDF; using home pins only");
                pickups = StudentPlotLocation.Index(null);
            }

            var mappable = new List<(BusBuddy.Core.Models.Student Student, StudentPlotPoint Pin)>();
            foreach (var student in allStudents)
            {
                var pin = StudentPlotLocation.TryFromStored(student, pickups);
                if (pin is null || !IsPlottableCoordinate(pin.Value.Latitude, pin.Value.Longitude))
                {
                    continue;
                }

                mappable.Add((student, pin.Value));
            }

            if (mappable.Count == 0)
            {
                Logger.Information("No students with map pins (Total={Total})", allStudents.Count);
                StatusMessage = "Student map PDF: no map pins";
                return new EligibilityPdfBuild(
                    Array.Empty<byte>(),
                    0,
                    allStudents.Count,
                    $"None of the {allStudents.Count} students have a map pin (home or catalog stop). Validate addresses first.");
            }

            // ORDER STOPS (Nearest Neighbor heuristic) starting at the district bus barn and ending at the catalog school.
            var (startLat, startLon) = await ResolveRouteStartAnchorAsync();
            var schoolCamera = await ResolveDistrictCameraAsync();
            var remaining = mappable.ToList();
            var ordered = new List<(BusBuddy.Core.Models.Student Student, StudentPlotPoint Pin)>();
            double currentLat = startLat, currentLon = startLon;
            while (remaining.Count > 0)
            {
                var nearestIndex = 0;
                var nearestDist = double.MaxValue;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var pin = remaining[i].Pin;
                    var dist = HaversineMiles(currentLat, currentLon, pin.Latitude, pin.Longitude);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIndex = i;
                    }
                }

                var nearest = remaining[nearestIndex];
                ordered.Add(nearest);
                currentLat = nearest.Pin.Latitude;
                currentLon = nearest.Pin.Longitude;
                remaining.RemoveAt(nearestIndex);
            }

            var averageMph = Math.Max(5.0, AverageRouteSpeedMph);
            var dwellPerStop = TimeSpan.FromMinutes(Math.Max(0, DwellMinutesPerStop));
            var dwellMinutes = (int)Math.Max(0, DwellMinutesPerStop);
            var departTimeOfDay = new TimeSpan(6, 50, 0);
            var routeDay = DateTime.Today;
            var cumulative = TimeSpan.Zero;
            double totalMiles = 0.0;
            var stops = new List<BusBuddy.Core.Models.RouteStop>();
            var order = 1;
            currentLat = startLat;
            currentLon = startLon;
            foreach (var (stu, pin) in ordered)
            {
                var legMiles = HaversineMiles(currentLat, currentLon, pin.Latitude, pin.Longitude);
                totalMiles += legMiles;
                cumulative += TimeSpan.FromMinutes(legMiles / averageMph * 60.0);
                var arrival = departTimeOfDay + cumulative;
                var departure = arrival + dwellPerStop;
                cumulative += dwellPerStop;
                var stopName = pin.AtPickup && !string.IsNullOrWhiteSpace(pin.PickupName)
                    ? $"{stu.StudentName ?? "Student"} @ {pin.PickupName}"
                    : stu.StudentName ?? "(Student)";
                stops.Add(new BusBuddy.Core.Models.RouteStop
                {
                    RouteId = -1,
                    StopOrder = order++,
                    StopName = stopName,
                    Latitude = (decimal)pin.Latitude,
                    Longitude = (decimal)pin.Longitude,
                    ScheduledArrival = arrival,
                    ScheduledDeparture = departure,
                    EstimatedArrivalTime = routeDay.Add(arrival),
                    EstimatedDepartureTime = routeDay.Add(departure),
                    StopDuration = dwellMinutes,
                    CreatedDate = DateTime.UtcNow
                });
                currentLat = pin.Latitude;
                currentLon = pin.Longitude;
            }

            var backLegMiles = HaversineMiles(currentLat, currentLon, schoolCamera.Lat, schoolCamera.Lon);
            totalMiles += backLegMiles;
            cumulative += TimeSpan.FromMinutes(backLegMiles / averageMph * 60.0);
            var arrivalBack = departTimeOfDay + cumulative;

            var route = new RouteModel
            {
                RouteId = -1,
                RouteName = $"Student Map {routeDay:MMM d}",
                Date = routeDay,
                IsActive = true,
                WaypointsJson = RouteWaypointSerializer.FromPairs(
                    ordered.Select(x => (x.Pin.Latitude, x.Pin.Longitude)))
            };

            var bus = new BusBuddy.Core.Models.Bus
            {
                BusNumber = "17",
                SeatingCapacity = 84,
                Status = "Active"
            };

            var roster = ordered.Select(x => x.Student).ToList();
            byte[] pdf;
            try
            {
                pdf = _pdfReportService.GenerateRouteSummaryReport(
                    route,
                    stops,
                    roster,
                    bus,
                    null,
                    slot,
                    LatestMapSnapshotPng);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "PDF generation failed for eligibility route");
                pdf = Array.Empty<byte>();
            }

            if (pdf.Length == 0 || !Views.Reports.PdfPreviewWindow.IsPdfPayload(pdf))
            {
                StatusMessage = "Student map PDF: generation failed";
                return new EligibilityPdfBuild(
                    Array.Empty<byte>(),
                    roster.Count,
                    allStudents.Count,
                    "The student map PDF could not be created.");
            }

            Logger.Information(
                "Student map PDF generated Mapped={Mapped} Total={Total} Stops={Stops} Miles~{Miles:F1} ETA-Back={EtaBack} HasSnapshot={HasSnapshot}",
                roster.Count,
                allStudents.Count,
                stops.Count,
                totalMiles,
                arrivalBack,
                LatestMapSnapshotPng is { Length: > 0 });
            StatusMessage = $"Student map PDF: {stops.Count} stops ~{totalMiles:F1} mi";
            return new EligibilityPdfBuild(pdf, roster.Count, allStudents.Count, null);
        }

        /// <summary>
        /// Builds the student map PDF and opens it in the in-app viewer. Public so MainWindow
        /// can trigger it without hosting MapView.
        /// </summary>
        public async Task GenerateEligibilityRoutePdfAndPreviewAsync()
        {
            if (_eligibilityPdfBusy)
            {
                return;
            }

            _eligibilityPdfBusy = true;
            _generateEligibilityPdfRelay.NotifyCanExecuteChanged();
            try
            {
                StatusMessage = "Generating student map PDF...";
                var built = await BuildEligibilityRoutePdfAsync(BusBuddy.Core.Models.RouteTimeSlot.AM);
                if (built.Blocker is not null)
                {
                    Logger.Information(
                        "Student map PDF skipped: {Blocker} Mapped={Mapped} Total={Total}",
                        built.Blocker,
                        built.MappedCount,
                        built.Total);
                    UserToast.Warning(built.Blocker, "Student Map PDF");
                    return;
                }

                _lastGeneratedEligibilityPdf = built.Pdf;
                StatusMessage = $"Student map PDF: {built.MappedCount} of {built.Total} students with map pins";
                ShowEligibilityPdfPreview(built.Pdf);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Eligibility PDF wrapper failed");
                StatusMessage = "Student map PDF error";
                UserToast.Error($"Could not generate the student map PDF: {ex.Message}", "Student Map PDF");
            }
            finally
            {
                _eligibilityPdfBusy = false;
                _generateEligibilityPdfRelay.NotifyCanExecuteChanged();
            }
        }

        public void PreviewLastEligibilityPdf()
        {
            var pdf = _lastGeneratedEligibilityPdf;
            if (pdf is not { Length: > 0 } || !Views.Reports.PdfPreviewWindow.IsPdfPayload(pdf))
            {
                UserToast.Info("Generate a student map PDF first.", "Student Map PDF");
                return;
            }

            try
            {
                ShowEligibilityPdfPreview(pdf);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed reopening student map PDF");
                UserToast.Error($"Could not open the PDF: {ex.Message}", "Student Map PDF");
            }
        }

        private void ShowEligibilityPdfPreview(byte[] pdf)
        {
            ShowOnUi(() =>
            {
                var preview = new Views.Reports.PdfPreviewWindow(pdf, "Student Map PDF");
                DialogOwner.Assign(preview);
                preview.Show();
                preview.Activate();
            });
        }

        private static void ShowOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                Logger.Warning("No WPF dispatcher — student map PDF preview skipped");
                return;
            }

            try
            {
                if (dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UI dispatch for student map PDF failed");
                throw;
            }
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

        private void ClearDepotMarkers() => ClearMarkersOfKind(MapMarkerLabels.Kind.Depot);

        private void ClearMarkersOfKind(MapMarkerLabels.Kind kind)
        {
            for (var i = MapMarkers.Count - 1; i >= 0; i--)
            {
                if (MapMarkers[i].Kind == kind)
                {
                    MapMarkers.RemoveAt(i);
                }
            }
        }

        private void ClearRouteWaypointMarkers()
        {
            var untagged = false;
            for (var i = MapMarkers.Count - 1; i >= 0; i--)
            {
                var marker = MapMarkers[i];
                if (marker.Kind == MapMarkerLabels.Kind.Waypoint
                    || marker.Label?.StartsWith(RouteWaypointPrefix, StringComparison.Ordinal) == true)
                {
                    MapMarkers.RemoveAt(i);
                }
                else if (marker.RouteStopLabel is not null)
                {
                    marker.RouteStopLabel = null;
                    marker.ApplyZoomVisuals(MapZoomLevel);
                    untagged = true;
                }
            }

            if (untagged)
            {
                NotifyMapMarkersChanged();
            }
        }

        /// <summary>Schools-only view: drop every pin that is not a school (route line stays; it is a path, not a pin).</summary>
        private void ClearMarkersExcept(MapMarkerLabels.Kind keep)
        {
            for (var i = MapMarkers.Count - 1; i >= 0; i--)
            {
                if (MapMarkers[i].Kind != keep)
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

        private RoutingDistrictSettings? DistrictSettings =>
            _districtSettings?.Current ?? DistrictCameraUi.CurrentSettings();

        private (double Lat, double Lon, string Name)? ResolveDepotMarker()
        {
            if (!DistrictDepot.TryGetCoordinates(DistrictSettings, out var lat, out var lon))
            {
                return null;
            }

            return (lat, lon, DistrictDepot.GetDisplayName(DistrictSettings));
        }

        private async Task<(double Lat, double Lon)> ResolveRouteStartAnchorAsync()
        {
            if (DistrictDepot.TryGetCoordinates(DistrictSettings, out var lat, out var lon))
            {
                return (lat, lon);
            }

            var camera = await ResolveDistrictCameraAsync();
            return (camera.Lat, camera.Lon);
        }

        private async Task<(double Lat, double Lon, int Zoom)> ResolveDistrictCameraAsync()
        {
            using var scope = _scopeFactory?.CreateScope();
            // Prefer the injected accessor (same singleton Settings.Replace updates).
            var camera = await DistrictCameraUi.ResolveAsync(
                scope?.ServiceProvider ?? App.ServiceProvider,
                _districtSettings?.Current);
            if (IsUsCentroidOverview(camera.Latitude, camera.Longitude, camera.ZoomLevel))
            {
                return (DistrictDefaultLatitude, DistrictDefaultLongitude, MapDefaults.DistrictZoomLevel);
            }

            return (camera.Latitude, camera.Longitude, camera.ZoomLevel);
        }

        private static bool IsUsCentroidOverview(double latitude, double longitude, int zoomLevel) =>
            zoomLevel == MapDefaults.UnconfiguredZoomLevel
            && Math.Abs(latitude - MapDefaults.UnconfiguredLatitude) < 0.01
            && Math.Abs(longitude - MapDefaults.UnconfiguredLongitude) < 0.01;

        private static bool IsDistrictDefaultCamera(double latitude, double longitude, int zoomLevel) =>
            zoomLevel == MapDefaults.DistrictZoomLevel
            && Math.Abs(latitude - DistrictDefaultLatitude) < 0.0001
            && Math.Abs(longitude - DistrictDefaultLongitude) < 0.0001;

        /// <summary>Reject 0,0, US-centroid overview, and out-of-range coordinates per specs/maps.md.</summary>
        private static bool IsPlottableCoordinate(double latitude, double longitude) =>
            LocationCoordinate.IsValidated(latitude, longitude);

        #endregion

        #region INotifyPropertyChanged Implementation

        #endregion

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
