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
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.Utilities;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;
using System.Windows; // For System.Windows.Point used by Syncfusion MapPolyline
using System.Collections.Generic; // For generic collections
using System.Linq; // For LINQ operations
using System.Windows.Media; // For VisualTreeHelper
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
        private bool _clerkOverrideBusy;
        private string _statusMessage = "Ready";
        private byte[]? _latestMapSnapshotPng; // Holds last captured map snapshot (PNG bytes) for PDF embedding
        private IAsyncRelayCommand _applyClerkOverrideRelay = null!;
        private IAsyncRelayCommand _bulkPlotRelay = null!;
        private IAsyncRelayCommand _optimizeRelay = null!;
        private bool _suppressRouteSelectionPlot;
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
        /// Direct paint hook set by <c>MapView</c>. A transient <c>Unloaded</c> can drop
        /// <see cref="RouteLineUpdated"/> without a second <c>Loaded</c>; this still paints.
        /// </summary>
        internal Action<System.Collections.Generic.IReadOnlyList<Point>>? RouteLinePaint { get; set; }

        /// <summary>
        /// Raised when a print of the current route map has been requested.
        /// </summary>
        public event EventHandler? PrintRequested;

        /// <summary>Bound map view captures the live SfMap into <see cref="LatestMapSnapshotPng"/>.</summary>
        public event EventHandler? CaptureSnapshotRequested;

        /// <summary>Ask the bound map view to write <see cref="LatestMapSnapshotPng"/> (no print dialog).</summary>
        public void RequestMapSnapshot() => CaptureSnapshotRequested?.Invoke(this, EventArgs.Empty);

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
                scopeFactory,
                (lat, lon, names, label, ids) => PlotStop(lat, lon, names, label, studentIds: ids),
                ResolveDepotMarker);
            _ = geocodingService;

            RefreshMapCommand = new AsyncRelayCommand(RefreshMapAsync);
            ExportRouteDataCommand = new AsyncRelayCommand(ExportRouteDataAsync);
            ZoomInCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ZoomOut());

            // Commands referenced by XAML (map toolbar)
            CenterOnStopsCommand = new AsyncRelayCommand(CenterOnStopsAsync);
            ShowRoutesCommand = new AsyncRelayCommand(ShowRoutesAsync);
            _optimizeRelay = new AsyncRelayCommand(OptimizeStopOrderAsync, () => SelectedRoute is not null);
            OptimizeStopOrderCommand = _optimizeRelay;
            ShowSchoolsCommand = new AsyncRelayCommand(ShowSchoolsAsync);
            PlotPickupStopsCommand = new AsyncRelayCommand(PlotPickupStopsAsync);
            ResetViewCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => ResetView());

            // Print current route map/directions
            PrintRouteMapsCommand = new BusBuddy.WPF.Commands.RelayCommand(_ => OnPrintRequested(), _ => true);

            _bulkPlotRelay = new AsyncRelayCommand(
                BulkPlotEligibleStudentsAsync,
                () => SelectedRoute is not null);
            BulkPlotEligibleStudentsCommand = _bulkPlotRelay;
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
        /// Markers to display on the map (students, school, etc.).
        /// </summary>
        public ObservableCollection<MapMarker> MapMarkers { get; private set; } = new();

        /// <summary>Schools and catalog stops that were not plotted because coordinates are not validated.</summary>
        public ObservableCollection<string> NeedsValidation { get; } = new();

        public string NeedsValidationSummary { get; private set; } = "Checking schools and catalog stops…";

        /// <summary>Assigned bus number for the selected route. Not a live GPS ping.</summary>
        public string SelectedRouteBusLabel { get; private set; } = "Select a route to show its bus";

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
            // Snapshot: ResetView / plot can mutate MapMarkers while zoom PropertyChanged is in flight.
            foreach (var marker in MapMarkers.ToList())
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
                if (!SetProperty(ref _selectedRoute, value))
                {
                    return;
                }

                NotifyBulkPlotCanExecute();
                _optimizeRelay?.NotifyCanExecuteChanged();
                NotifyClerkOverrideCanExecute();
                RefreshBusLabel();
                if (!_suppressRouteSelectionPlot)
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

        public ICommand RefreshMapCommand { get; private set; } = null!;
        public ICommand ExportRouteDataCommand { get; private set; } = null!;
        public ICommand ZoomInCommand { get; private set; } = null!;
        public ICommand ZoomOutCommand { get; private set; } = null!;

        // Additional commands referenced in XAML
        public ICommand CenterOnStopsCommand { get; private set; } = null!;
        public ICommand ShowRoutesCommand { get; private set; } = null!;
        public ICommand OptimizeStopOrderCommand { get; private set; } = null!;
        public ICommand ShowSchoolsCommand { get; private set; } = null!;
        public ICommand PlotPickupStopsCommand { get; private set; } = null!;
        public ICommand ResetViewCommand { get; private set; } = null!;
        public ICommand PrintRouteMapsCommand { get; private set; } = null!;
        public ICommand BulkPlotEligibleStudentsCommand { get; private set; } = null!;
        public ICommand ApplyClerkOverrideCommand { get; private set; } = null!;

        #endregion

        #region Private Methods

        private async Task<bool> LoadRoutesAsync()
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
                return true;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error loading routes for the map");
                ReportMapDataFailure(ex, "load routes for the district map");
                return false;
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
                StatusMessage = $"Selected: {RouteDistrictMapLabels.ListCaption(SelectedRoute)}";
                _ = UpdateMapForRouteAsync(SelectedRoute);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error handling route selection change");
            }
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

        private void NotifyBulkPlotCanExecute() =>
            _bulkPlotRelay?.NotifyCanExecuteChanged();

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

                _trail.BeginDraw();
                if (moved > 0)
                {
                    await UpdateMapForRouteAsync(target).ConfigureAwait(true);
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

                // Restore district base overlay (schools/pickups/depot — not all student homes).
                await _layers.LoadDistrictBaseLayersAsync();
                await RefreshNeedsValidationAsync().ConfigureAwait(true);

                if (SelectedRoute is not null)
                {
                    Logger.Information("Refreshing map for route: {RouteName}", SelectedRoute.RouteName ?? "Unknown");
                    await UpdateMapForRouteAsync(SelectedRoute, refreshDrivePath: true);
                }
                else
                {
                    StatusMessage = "Map refreshed — select a route for roster homes and trail";
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error refreshing map");
                ReportMapDataFailure(ex, "refresh the district map");
            }
            finally
            {
                IsMapLoading = false;
            }
        }

        /// <summary>
        /// One load after routes: depot → schools → active pickups → students with stored coords (no network).
        /// Do not auto-select a route or refresh a drive path — clerk pick is the path writer.
        /// </summary>
        private async Task InitializeMapDataAsync()
        {
            Logger.Information("InitializeMapDataAsync starting — routes then district layers (no auto trail)");
            try
            {
                var routesLoaded = await LoadRoutesAsync();

                var seeded = await _layers.LoadDistrictBaseLayersAsync();
                await RefreshNeedsValidationAsync().ConfigureAwait(true);

                if (MapMarkers.Count > 0)
                {
                    CenterOnMarkers();
                }
                else
                {
                    var (lat, lon, zoom) = await ResolveDistrictCameraAsync();
                    SetMapView(lat, lon, zoom);
                }

                if (!routesLoaded)
                {
                    return;
                }

                StatusMessage =
                    $"Map ready — {seeded.Schools} school(s), {seeded.Pickups} pickup(s), {seeded.Depots} depot(s). Select a route for homes and path.";
                Logger.Information(
                    "InitializeMapDataAsync completed Routes={RouteCount} Markers={MarkerCount} Schools={Schools} Pickups={Pickups} Students={Students} Depots={Depots} Trail={HasTrail}",
                    Routes.Count,
                    MapMarkers.Count,
                    seeded.Schools,
                    seeded.Pickups,
                    seeded.Students,
                    seeded.Depots,
                    false);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "InitializeMapDataAsync failed");
            }
        }

        private BusBuddy.Core.Services.IStudentService? ResolveStudentService(IServiceScope? scope) =>
            _studentService ?? scope?.ServiceProvider.GetService<BusBuddy.Core.Services.IStudentService>();

        /// <summary>
        /// Plots assigned riders on <see cref="SelectedRoute"/> (stored pickup/home GPS only — no geocode).
        /// </summary>
        private async Task BulkPlotEligibleStudentsAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route to plot assigned students";
                UserToast.Warning("Pick a route in the list, then press Plot Students.", "Plot Students");
                return;
            }

            try
            {
                StatusMessage = "Plotting assigned students...";
                ClearRouteRosterFromMap();
                var plotted = await _layers.PlotAssignedStudentsForRouteAsync(SelectedRoute).ConfigureAwait(true);
                var routeName = SelectedRoute.RouteName ?? "route";
                StatusMessage = plotted == 0
                    ? $"No assigned students with stored locations on {routeName}"
                    : $"Plotted {plotted} assigned student location(s) on {routeName}";
                if (plotted > 0)
                {
                    CenterOnMarkers();
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Plot assigned students failed");
                StatusMessage = "Plot students failed — see logs";
            }
        }

        private async Task UpdateMapForRouteAsync(RouteModel? route, bool refreshDrivePath = false)
        {
            var generation = _trail.BeginDraw();
            var routeName = route?.RouteName ?? "Unknown";
            try
            {
                IReadOnlyList<RouteStop> publishedStops = Array.Empty<RouteStop>();
                if (route is not null)
                {
                    await EnsureRouteWaypointsAsync(route).ConfigureAwait(true);
                    publishedStops = await LoadPublishedStopsAsync(route).ConfigureAwait(true);
                    if (!_trail.IsCurrent(generation))
                    {
                        return;
                    }
                }

                var validatedPublished = publishedStops.Where(s => s.HasValidatedCoordinates).ToList();

                MapRouteTrailPersist persist = default;
                async Task<bool> TryRefreshDrivePathAsync()
                {
                    if (route is null || string.IsNullOrWhiteSpace(route.WaypointsJson))
                    {
                        return false;
                    }

                    var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
                    if (payload.Stops.Count < 2 && payload.Points.Count < 2)
                    {
                        return false;
                    }

                    persist = await _trail.RefreshStoredPathAsync(route).ConfigureAwait(true);
                    return persist.Computed;
                }

                if (!refreshDrivePath && route is not null && validatedPublished.Count < 2)
                {
                    var rosterStops = RouteWaypointSerializer.ParseStops(route.WaypointsJson);
                    if (rosterStops.Count >= 2 && _routingService is not null)
                    {
                        refreshDrivePath = true;
                    }
                }

                if (refreshDrivePath && route is not null)
                {
                    await TryRefreshDrivePathAsync().ConfigureAwait(true);
                    if (!_trail.IsCurrent(generation))
                    {
                        return;
                    }
                }

                if (!_trail.IsCurrent(generation))
                {
                    return;
                }

                var plot = MapRouteTrail.Build(route, validatedPublished.Count);
                var renderableLine = plot.Line
                    .Where(p => IsPlottableCoordinate(p.X, p.Y))
                    .ToList();
                plot = MapRouteTrail.Build(route, validatedPublished.Count, renderableLine.Count);

                if (!refreshDrivePath
                    && route is not null
                    && renderableLine.Count < 2
                    && validatedPublished.Count >= 2
                    && _routingService is not null)
                {
                    Logger.Information(
                        "Auto-refreshing drive path — published stops={Published} renderable line={Line} RouteId={RouteId}",
                        validatedPublished.Count,
                        renderableLine.Count,
                        route.RouteId);
                    if (await TryRefreshDrivePathAsync().ConfigureAwait(true))
                    {
                        plot = MapRouteTrail.Build(route, validatedPublished.Count);
                        renderableLine = plot.Line
                            .Where(p => IsPlottableCoordinate(p.X, p.Y))
                            .ToList();
                        plot = MapRouteTrail.Build(route, validatedPublished.Count, renderableLine.Count);
                    }
                }

                ClearRouteRosterFromMap();
                await UpdatePolylineAsync(renderableLine);
                if (!_trail.IsCurrent(generation))
                {
                    return;
                }

                ClearRouteWaypointMarkers();
                if (validatedPublished.Count > 0)
                {
                    foreach (var stop in validatedPublished.OrderBy(s => s.StopOrder))
                    {
                        var name = string.IsNullOrWhiteSpace(stop.StopName)
                            ? $"Stop {stop.StopOrder}"
                            : stop.StopName;
                        PlotStop(
                            (double)stop.Latitude!.Value,
                            (double)stop.Longitude!.Value,
                            null,
                            MapMarkerLabels.ForRouteStop(name),
                            MapMarkerLabels.Kind.Waypoint);
                    }
                }
                else
                {
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
                }

                if (renderableLine.Count >= 2)
                {
                    CenterOnPoints(renderableLine);
                }
                else if (validatedPublished.Count > 0)
                {
                    CenterOnPoints(validatedPublished.Select(s =>
                        new Point((double)s.Latitude!.Value, (double)s.Longitude!.Value)));
                }
                else if (plot.Markers.Count > 0)
                {
                    CenterOnPoints(plot.Markers.Select(s => new Point(s.Latitude, s.Longitude)));
                }

                if (route is not null)
                {
                    await _layers.PlotAssignedStudentsForRouteAsync(route).ConfigureAwait(true);
                }

                if (!_trail.IsCurrent(generation))
                {
                    return;
                }

                var busLabel = FormatBusLabel(route);
                StatusMessage = persist.Computed && !persist.Persisted && !string.IsNullOrWhiteSpace(persist.Message)
                    ? persist.Message
                    : string.IsNullOrWhiteSpace(busLabel)
                        ? plot.StatusMessage
                        : $"{busLabel}. {plot.StatusMessage}";
                Logger.Information(
                    "Map updated for route: {RouteName} LinePoints={Line} PublishedStops={Published} JsonMarkers={Json} Refresh={Refresh}",
                    routeName,
                    renderableLine.Count,
                    validatedPublished.Count,
                    plot.Markers.Count,
                    refreshDrivePath);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed to update map for route {RouteName}", routeName);
            }
        }

        /// <summary>
        /// Published stops win. When the route has no geocoded stop list, rebuild the trail from assigned homes.
        /// </summary>
        private async Task EnsureRouteWaypointsAsync(RouteModel route)
        {
            try
            {
                var published = await LoadPublishedStopsAsync(route).ConfigureAwait(true);
                var validatedCount = published.Count(s => s.HasValidatedCoordinates);
                if (validatedCount >= 2)
                {
                    var payload = RouteWaypointSerializer.ParsePayload(route.WaypointsJson);
                    var jsonStopCount = payload.Stops.Count > 0
                        ? payload.Stops.Count
                        : payload.MarkerStops.Count;
                    if (string.IsNullOrWhiteSpace(route.WaypointsJson) || jsonStopCount != validatedCount)
                    {
                        if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
                        {
                            Logger.Information(
                                "WaypointsJson stop count {JsonStops} != published {PublishedStops} RouteId={RouteId} — rebuilding from published stops",
                                jsonStopCount,
                                validatedCount,
                                route.RouteId);
                        }

                        await RebuildRouteWaypointsAsync(route).ConfigureAwait(true);
                    }

                    return;
                }

                // No published stop list. An older polyline must not hide the assigned homes.
                Logger.Information(
                    "RouteId={RouteId} has no published stops — rebuilding the trail from assigned homes",
                    route.RouteId);
                await RebuildRouteWaypointsAsync(route).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(route.WaypointsJson))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Sync waypoints with published stops failed RouteId={RouteId}", route.RouteId);
            }

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

            await RebuildRouteWaypointsAsync(route).ConfigureAwait(true);
        }

        private async Task RebuildRouteWaypointsAsync(RouteModel route)
        {
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

        private async Task<IReadOnlyList<RouteStop>> LoadPublishedStopsAsync(RouteModel route)
        {
            try
            {
                using var scope = _scopeFactory?.CreateScope();
                var routes = scope?.ServiceProvider.GetService<IRouteService>();
                if (routes is null)
                {
                    return Array.Empty<RouteStop>();
                }

                var result = await routes.GetRouteStopsAsync(route.RouteId).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    return Array.Empty<RouteStop>();
                }

                var slot = RouteSession.ToAssignmentSlot(route);
                var roster = await routes.GetStudentsForRouteAsync(route.RouteId, slot).ConfigureAwait(true);
                var students = roster.IsSuccess && roster.Value is not null
                    ? roster.Value
                    : new List<BusBuddy.Core.Models.Student>();
                return AssignedRouteStops.ForRouting(result.Value, students);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "LoadPublishedStopsAsync failed RouteId={RouteId}", route.RouteId);
                return Array.Empty<RouteStop>();
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
                Logger.Information("Export Route requested with no selection");
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

        private void ReportMapDataFailure(Exception ex, string operation)
        {
            var detail = DatabaseUserMessage.ForOperation(ex, operation);
            StatusMessage = DatabaseUserMessage.IsConnectivityFailure(ex)
                ? DatabaseUserMessage.UnavailableShort
                : detail;
            UserToast.Warning(detail, "District Map");
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

        /// <summary>
        /// Re-open the district map on a clean base overlay, then replay the selected route.
        /// Drops homes left by another screen on the shared view model.
        /// </summary>
        public async Task OnDistrictMapSurfaceActivatedAsync()
        {
            try
            {
                var routesLoaded = true;
                if (Routes.Count == 0)
                {
                    routesLoaded = await LoadRoutesAsync().ConfigureAwait(true);
                }

                ClearRouteRosterFromMap();
                await _layers.LoadDistrictBaseLayersAsync().ConfigureAwait(true);
                await RefreshNeedsValidationAsync().ConfigureAwait(true);

                if (!routesLoaded)
                {
                    return;
                }

                if (SelectedRoute is null)
                {
                    await ClearRoutePolylineAsync().ConfigureAwait(true);
                    ClearRouteWaypointMarkers();
                    StatusMessage = "Select a route for homes and path.";
                    return;
                }

                Logger.Information(
                    "District map surface activated — replay route {RouteName}",
                    SelectedRoute.RouteName ?? "Unknown");
                var needsDriveRefresh = RouteLinePoints.Count < 2;
                await UpdateMapForRouteAsync(SelectedRoute, refreshDrivePath: needsDriveRefresh)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "OnDistrictMapSurfaceActivatedAsync failed");
            }
        }

        private async Task RefreshNeedsValidationAsync()
        {
            IReadOnlyList<string> lines;
            try
            {
                lines = await _layers.ListNeedsValidationAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Needs-validation list skipped");
                NeedsValidation.Clear();
                NeedsValidationSummary = DatabaseUserMessage.IsConnectivityFailure(ex)
                    ? DatabaseUserMessage.UnavailableShort
                    : "Could not check which schools and stops need validation.";
                OnPropertyChanged(nameof(NeedsValidationSummary));
                return;
            }

            NeedsValidation.Clear();
            foreach (var line in lines)
            {
                NeedsValidation.Add(line);
            }

            NeedsValidationSummary = lines.Count == 0
                ? "Schools and catalog stops with coordinates are plotted."
                : $"{lines.Count} place(s) need a validated address before they can be plotted.";
            OnPropertyChanged(nameof(NeedsValidationSummary));
        }

        private void RefreshBusLabel()
        {
            SelectedRouteBusLabel = SelectedRoute is null
                ? "Select a route to show its bus"
                : FormatBusLabel(SelectedRoute) is { Length: > 0 } bus
                    ? bus
                    : $"{SelectedRoute.RouteName ?? "Route"}: no bus number on this route";
            OnPropertyChanged(nameof(SelectedRouteBusLabel));
        }

        private static string FormatBusLabel(RouteModel? route)
        {
            if (route is null)
            {
                return string.Empty;
            }

            var number = route.BusNumber;
            if (string.IsNullOrWhiteSpace(number))
            {
                number = route.AMVehicle?.BusNumber ?? route.PMVehicle?.BusNumber;
            }

            return string.IsNullOrWhiteSpace(number) ? string.Empty : $"Bus {number.Trim()}";
        }

        private async Task CenterOnStopsAsync()
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
                Logger.Warning(ex, "CenterOnStops failed");
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
            var lat = (minLat + maxLat) / 2d;
            var lon = (minLon + maxLon) / 2d;
            var zoom = MapDefaults.ZoomForBounds(
                minLat,
                maxLat,
                minLon,
                maxLon,
                MapViewportSize.Width,
                MapViewportSize.Height);
            Logger.Information(
                "Map camera fit Lat={Lat:F4} Lon={Lon:F4} Zoom={Zoom} Points={Count}",
                lat,
                lon,
                zoom,
                list.Count);
            SetMapView(lat, lon, zoom);
        }

        private async Task ShowRoutesAsync()
        {
            Logger.Information("Show routes requested");
            try
            {
                if (Routes.Count == 0)
                {
                    await LoadRoutesAsync().ConfigureAwait(true);
                }

                if (SelectedRoute is null)
                {
                    StatusMessage = "Select a route, then press Show Routes";
                    UserToast.Warning("Pick a route in the list, then press Show Routes.", "Show Routes");
                    return;
                }

                StatusMessage = $"Refreshing {SelectedRoute.RouteName}...";
                await UpdateMapForRouteAsync(SelectedRoute, refreshDrivePath: true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowRoutes failed");
                StatusMessage = "Could not show routes";
            }
        }

        /// <summary>
        /// Clerk-initiated visit order. Same planner as Route Management. Start and end stay pinned.
        /// If the route has students but no published stops, those homes are published first.
        /// </summary>
        private async Task OptimizeStopOrderAsync()
        {
            if (SelectedRoute is null)
            {
                StatusMessage = "Select a route, then press Optimize Order";
                UserToast.Warning("Pick a route in the list, then press Optimize Order.", "Optimize Order");
                return;
            }

            var route = SelectedRoute;
            try
            {
                StatusMessage = $"Optimizing {route.RouteName}...";
                using var scope = _scopeFactory?.CreateScope();
                var provider = scope?.ServiceProvider ?? App.ServiceProvider;
                var routes = provider?.GetService<IRouteService>();
                var rebuild = provider?.GetService<IRouteWaypointRebuildService>();
                var optimization = provider?.GetService<IRouteOptimizationService>();
                if (routes is null || rebuild is null)
                {
                    StatusMessage = "Route services are not available";
                    return;
                }

                var published = await rebuild.PublishRosterStopsIfMissingAsync(route.RouteId).ConfigureAwait(true);
                if (published > 0)
                {
                    Logger.Information(
                        "Optimize Order published {Count} roster stops RouteId={RouteId}",
                        published,
                        route.RouteId);
                }

                var stopsResult = await routes.GetRouteStopsAsync(route.RouteId).ConfigureAwait(true);
                var stops = stopsResult.IsSuccess && stopsResult.Value is not null
                    ? stopsResult.Value.ToList()
                    : new List<RouteStop>();
                var slot = RouteSession.ToAssignmentSlot(route);
                var roster = await routes.GetStudentsForRouteAsync(route.RouteId, slot).ConfigureAwait(true);
                var students = roster.IsSuccess && roster.Value is not null
                    ? roster.Value
                    : new List<BusBuddy.Core.Models.Student>();
                var validated = AssignedRouteStops.ForRouting(stops, students)
                    .Where(s => s.HasValidatedCoordinates)
                    .ToList();
                if (optimization is null || !optimization.IsConfigured)
                {
                    StatusMessage = validated.Count >= 2
                        ? $"{route.RouteName}: {validated.Count} stops published. Route Optimization is not configured, so the order was not changed. Show Routes draws the road path."
                        : "Route Optimization is not configured, and this route does not have enough geocoded stops.";
                    await ReloadWaypointsAndDrawAsync(route).ConfigureAwait(true);
                    return;
                }

                if (validated.Count < 3)
                {
                    StatusMessage = validated.Count == 0
                        ? $"{route.RouteName} has no geocoded homes to optimize. Validate student addresses first."
                        : $"{route.RouteName} needs at least three geocoded stops to optimize order.";
                    UserToast.Warning(StatusMessage, "Optimize Order");
                    await ReloadWaypointsAndDrawAsync(route).ConfigureAwait(true);
                    return;
                }

                var ordered = await RouteStopOrderPlanner.ComputePinnedOrderAsync(
                    validated,
                    optimization,
                    route.MaxCapacity,
                    DateTime.UtcNow).ConfigureAwait(true);
                if (!ordered.IsSuccess || ordered.Value is null)
                {
                    StatusMessage = ordered.Error ?? "Route Optimization failed.";
                    UserToast.Warning(StatusMessage, "Optimize Order");
                    await ReloadWaypointsAndDrawAsync(route).ConfigureAwait(true);
                    return;
                }

                var reorder = await routes.ReorderRouteStopsAsync(
                    route.RouteId,
                    AssignedRouteStops.OrderPreservingUnroutable(stops, ordered.Value.ToList()))
                    .ConfigureAwait(true);
                if (!reorder.IsSuccess)
                {
                    StatusMessage = reorder.Error ?? "Could not save stop order.";
                    UserToast.Warning(StatusMessage, "Optimize Order");
                    return;
                }

                StatusMessage = $"{route.RouteName}: stop order optimized. Start and end stay pinned. Drawing the road path.";
                UserToast.Success(StatusMessage, "Optimize Order");
                await ReloadWaypointsAndDrawAsync(route).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Optimize Order failed RouteId={RouteId}", route.RouteId);
                StatusMessage = DatabaseUserMessage.IsConnectivityFailure(ex)
                    ? DatabaseUserMessage.UnavailableShort
                    : "Could not optimize stop order";
            }
        }

        private async Task ReloadWaypointsAndDrawAsync(RouteModel route)
        {
            try
            {
                var loaded = await _geoDataService.GetRouteGeoDataAsync(route.RouteId).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(loaded?.WaypointsJson))
                {
                    route.WaypointsJson = loaded.WaypointsJson;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Reload waypoints after optimize failed RouteId={RouteId}", route.RouteId);
            }

            await UpdateMapForRouteAsync(route, refreshDrivePath: true).ConfigureAwait(true);
        }

        /// <summary>
        /// Schools-only view: every non-school pin and the route polyline are removed, then schools with
        /// validated coordinates are plotted. Refresh restores base layers and the selected route.
        /// </summary>
        private async Task ShowSchoolsAsync()
        {
            StatusMessage = "Showing schools only...";
            Logger.Information("Show schools (schools-only view) requested");
            try
            {
                var plotted = await _layers.PlotSchoolsAsync();
                ClearMarkersExcept(MapMarkerLabels.Kind.School);
                if (plotted == 0)
                {
                    StatusMessage = "No schools with validated coordinates (needs validation)";
                    UserToast.Warning("No school has a validated address yet. Add one under Students → Schools.", "Show Schools");
                    return;
                }

                await ClearRoutePolylineAsync().ConfigureAwait(true);
                CenterOnMarkers();
                StatusMessage = $"Schools only — {plotted} school(s). Refresh restores all layers";
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "ShowSchools failed");
                ReportMapDataFailure(ex, "show schools");
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
                DatabaseUserMessage.LogFailure(Logger, ex, "PlotPickupStops failed");
                ReportMapDataFailure(ex, "show pickup stops");
            }
        }

        /// <summary>Reset camera to depot/bbox, then school, then Lamar/Wiley clerk default.</summary>
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
                // Spec toolbar: Home recenters depot — keep selected route trail and stop pins.
                _ = ResetCameraAndReplayTrailAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ResetView failed");
            }
        }

        private async Task ResetCameraAndReplayTrailAsync()
        {
            await ResetCameraToDistrictAsync().ConfigureAwait(true);
            if (RouteLinePoints.Count >= 2)
            {
                RouteLineUpdated?.Invoke(this, new RouteLineEventArgs(RouteLinePoints));
            }
        }

        private async Task ClearRoutePolylineAsync()
        {
            RouteLinePoints.Clear();
            await UpdatePolylineAsync(RouteLinePoints).ConfigureAwait(true);
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
        /// Capture a visual element (map container) into PNG bytes and store in LatestMapSnapshotPng.
        /// View code-behind can call this right after PrintRequested is raised.
        /// </summary>
        /// <param name="mapElement">FrameworkElement containing the rendered map.</param>
        public void CaptureMapSnapshot(FrameworkElement mapElement)
        {
            var bytes = MapSnapshotEncoder.TryEncode(mapElement, out var status);
            StatusMessage = status;
            if (bytes is { Length: > 0 })
            {
                LatestMapSnapshotPng = bytes;
            }
        }

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

        /// <summary>Removes route-roster homes and student-at-stop names (spec: homes only on selected route).</summary>
        private void ClearRouteRosterFromMap()
        {
            var mutated = false;
            for (var i = MapMarkers.Count - 1; i >= 0; i--)
            {
                var marker = MapMarkers[i];
                if (marker.Kind is MapMarkerLabels.Kind.Home or MapMarkerLabels.Kind.Student)
                {
                    MapMarkers.RemoveAt(i);
                    mutated = true;
                    continue;
                }

                if (marker.StudentIds.Count == 0 && marker.StudentNames.Count == 0)
                {
                    continue;
                }

                marker.StudentIds.Clear();
                marker.StudentNames.Clear();
                marker.ApplyZoomVisuals(MapZoomLevel);
                mutated = true;
            }

            if (mutated)
            {
                NotifyMapMarkersChanged();
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

            var subscribers = RouteLineUpdated?.GetInvocationList().Length ?? 0;
            Logger.Information(
                "Route line publish Points={Count} EventSubscribers={Subscribers} DirectPaint={Direct}",
                RouteLinePoints.Count,
                subscribers,
                RouteLinePaint is not null);
            RouteLineUpdated?.Invoke(this, new RouteLineEventArgs(RouteLinePoints));
            try
            {
                RouteLinePaint?.Invoke(RouteLinePoints);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Direct route line paint failed");
            }
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

        private async Task<(double Lat, double Lon, int Zoom)> ResolveDistrictCameraAsync()
        {
            using var scope = _scopeFactory?.CreateScope();
            // Prefer the injected accessor (same singleton Settings.Replace updates).
            var camera = await DistrictCameraUi.ResolveHomeAsync(
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
