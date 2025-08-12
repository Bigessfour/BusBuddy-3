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
using System.Text.Json; // Microsoft .NET docs: System.Text.Json for JSON serialization/deserialization
using System.Windows; // For System.Windows.Point used by Syncfusion MapPolyline
using System.Collections.Generic; // For generic collections
using System.Linq; // For LINQ operations
using System.Windows.Media; // For VisualTreeHelper during snapshot
using System.Windows.Media.Imaging; // For RenderTargetBitmap / PngBitmapEncoder (Microsoft WPF docs: Imaging)
using System.IO; // For saving generated eligibility PDF to disk

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
    private readonly BusBuddy.Core.Services.PdfReportService _pdfReportService = new(); // Lightweight stateless service
    private readonly BusBuddy.Core.Services.IStudentService? _studentService; // If available for pulling students
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
    private byte[]? _latestMapSnapshotPng; // Holds last captured map snapshot (PNG bytes) for PDF embedding

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

        /// <summary>
        /// Latest captured map snapshot in PNG format (used for embedding into route PDF exports).
        /// A separate capturing routine in the View should set this after rendering a visual to a RenderTargetBitmap and encoding to PNG.
        /// </summary>
        public byte[]? LatestMapSnapshotPng
        {
            get => _latestMapSnapshotPng;
            set => SetProperty(ref _latestMapSnapshotPng, value);
        }

    public GoogleEarthViewModel(IGeoDataService geoDataService, IEligibilityService? eligibilityService = null, IGeocodingService? geocodingService = null, BusBuddy.Core.Services.IStudentService? studentService = null)
        {
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));
            _eligibilityService = eligibilityService; // optional during MVP
            _geocodingService = geocodingService; // optional until wired
            _studentService = studentService;

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

            // Print current route map/directions
            PrintRouteMapsCommand = new RelayCommand(_ => OnPrintRequested(), _ => true);

            // Eligibility route PDF generation
            GenerateEligibilityRoutePdfCommand = new RelayCommand(async _ => await GenerateEligibilityRoutePdfAndSaveAsync(), _ => true);

            // Add marker (stop) plotting command (MVP). Accepts parameter forms documented in AddMarkerFromParam.
            AddMarkerCommand = new RelayCommand(p => AddMarkerFromParam(p));

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
            try
            {
                // Sample route + line (placeholder until real multi-route overlay logic implemented)
                var sampleRoute = new RouteModel
                {
                    RouteId = 1,
                    RouteName = "Sample Route 1",
                    Date = DateTime.Today,
                    School = "Sample School",
                    IsActive = true
                };
                var samplePoints = new[] { new Point(38.1527, -102.7204), new Point(38.20, -102.68) };
                await UpdatePolylineAsync(samplePoints);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "LoadAllRoutesOnMapAsync sample overlay failed");
            }
        }

        /// <summary>
        /// Automatically loads all students, filters out Lamar and in-town Wiley addresses, geocodes missing coordinates, eligibility-checks, and plots markers.
        /// Pattern leverages existing IStudentService + IGeocodingService + IEligibilityService interfaces. All operations are sequential for MVP reliability.
        /// </summary>
        private async Task BulkPlotEligibleStudentsAsync()
        {
            if (_studentService is null)
            {
                StatusMessage = "Student service unavailable";
                return;
            }
            StatusMessage = "Loading students...";
            List<BusBuddy.Core.Models.Student> students;
            try
            {
                students = await _studentService.GetAllStudentsAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Bulk plot: failed loading students");
                StatusMessage = "Load students failed";
                return;
            }

            if (students.Count == 0)
            {
                StatusMessage = "No students";
                return;
            }

            // Filter: exclude Lamar anywhere in address, and exclude obvious in-town Wiley addresses (simple contains heuristic on HomeAddress / City == Wiley)
            var filtered = students.Where(s =>
                (string.IsNullOrWhiteSpace(s.HomeAddress) || !s.HomeAddress.Contains("Lamar", StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(s.City) || !s.City.Equals("Wiley", StringComparison.OrdinalIgnoreCase))
            ).ToList();

            int geocoded = 0, eligibleCount = 0, plotted = 0;
            StatusMessage = $"Filtering {filtered.Count} students...";

            foreach (var stu in filtered)
            {
                double? lat = (double?)stu.Latitude; // Student entity uses decimal?; cast carefully
                double? lon = (double?)stu.Longitude;
                if ((!lat.HasValue || !lon.HasValue) && _geocodingService != null)
                {
                    try
                    {
                        var geo = await _geocodingService.GeocodeAsync(stu.HomeAddress, stu.City, stu.State, stu.Zip);
                        if (geo.HasValue)
                        {
                            lat = geo.Value.latitude;
                            lon = geo.Value.longitude;
                            geocoded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Geocode failed for student {Id}", stu.StudentId);
                        continue; // skip
                    }
                }

                if (!lat.HasValue || !lon.HasValue)
                {
                    continue; // cannot plot
                }

                bool eligible = true; // Default to true if no service
                if (_eligibilityService != null)
                {
                    try
                    {
                        eligible = await _eligibilityService.IsEligibleAsync(lat.Value, lon.Value);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Eligibility check failed for student {Id}", stu.StudentId);
                        eligible = false;
                    }
                }
                if (!eligible)
                {
                    continue;
                }
                eligibleCount++;

                // Plot marker; label with name (or ID) — clustering handled in PlotStop
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

            StatusMessage = $"Plotted {plotted} markers (Eligible {eligibleCount}, Geocoded {geocoded})";
            Logger.Information("Bulk plot complete Eligible={Eligible} Geocoded={Geocoded} Plotted={Plotted} From={Filtered} Total={Total}", eligibleCount, geocoded, plotted, filtered.Count, students.Count);
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
                    points = ParseWaypointsToPoints(SelectedRoute.WaypointsJson);
                }

                await UpdatePolylineAsync(points);
                Logger.Information("Map updated for route: {RouteName} with {Count} points", routeName, points.Length);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update map for route {RouteName}", routeName);
            }
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
            try { ZoomInRequested?.Invoke(this, EventArgs.Empty); } catch (Exception ex) { Logger.Warning(ex, "ZoomIn event dispatch failed"); }
        }

        private void ZoomOut()
        {
            StatusMessage = "Zooming out...";
            Logger.Information("Map zoom out requested");
            try { ZoomOutRequested?.Invoke(this, EventArgs.Empty); } catch (Exception ex) { Logger.Warning(ex, "ZoomOut event dispatch failed"); }
        }

        // MVP stub implementations for XAML-bound commands
        private void CenterOnFleet()
        {
            StatusMessage = "Centering map on fleet...";
            Logger.Information("Center on fleet requested");
            try { CenterRequested?.Invoke(this, EventArgs.Empty); } catch (Exception ex) { Logger.Warning(ex, "Center event dispatch failed"); }
        }

        private void ShowAllBuses()
        {
            StatusMessage = "Showing all buses...";
            Logger.Information("Show all buses requested");
            try
            {
                // If ActiveBuses have coordinates (future), plot; placeholder logs only for MVP.
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowAllBuses failed");
            }
        }

        private void ShowRoutes()
        {
            StatusMessage = "Showing routes on map...";
            Logger.Information("Show routes requested");
            try
            {
                if (Routes.Count == 0)
                {
                    StatusMessage = "No routes loaded";
                    return;
                }
                var first = Routes[0];
                // Demo polyline: use existing Wiley anchor + small offset
                var pts = new [] { new System.Windows.Point(38.1527, -102.7204), new System.Windows.Point(38.1600, -102.7000) };
                _ = UpdatePolylineAsync(pts);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowRoutes failed");
            }
        }

        private void ShowSchools()
        {
            StatusMessage = "Showing schools on map...";
            Logger.Information("Show schools requested");
            try
            {
                // Ensure school anchor marker exists; if not, add again.
                if (!MapMarkers.Any(m => m.Label != null && m.Label.Contains("School", StringComparison.OrdinalIgnoreCase)))
                {
                    PlotStop(38.1527, -102.7204, null, "Wiley School RE-13JT");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ShowSchools failed");
            }
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
            try
            {
                RouteLinePoints.Clear();
                RouteLineUpdated?.Invoke(this, new RouteLineEventArgs(RouteLinePoints));
                StatusMessage = "Map reset";
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
                Logger.Error(ex, "Failed to request printing");
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
            const double mergeTolerance = 0.00005; // ~5m tolerance for aggregating to existing marker (MVP simple clustering)
            // Try find existing marker within tolerance
            var existing = MapMarkers.FirstOrDefault(m => Math.Abs(m.Latitude - latitude) < mergeTolerance && Math.Abs(m.Longitude - longitude) < mergeTolerance);
            if (existing == null)
            {
                existing = new MapMarker
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Label = label
                };
                MapMarkers.Add(existing);
                Logger.Information("Added new stop marker at ({Lat}, {Lon}) Label={Label}", latitude, longitude, label ?? "<auto>");
            }
            else if (!string.IsNullOrWhiteSpace(label))
            {
                existing.Label = label; // explicit override
            }

            if (studentNames != null)
            {
                foreach (var name in studentNames)
                {
                    existing.AddStudent(name);
                }
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
                    // Demo fallback – center of Wiley
                    PlotStop(38.1527, -102.7204, null, "New Stop");
                    return;
                }

                switch (param)
                {
                    case MapMarker mm:
                        PlotStop(mm.Latitude, mm.Longitude, mm.StudentNames, mm.Label);
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
                Logger.Error(ex, "Failed to add marker from parameter");
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
                Logger.Error(ex, "Map snapshot capture failed");
                StatusMessage = "Map snapshot error";
            }
        }

        /// <summary>
        /// MVP helper: Build a pseudo-route PDF consisting of all eligible students (address not containing "Lamar" and inside district but outside town) plotted as individual stops.
        /// For each student: create a RouteStop sequentially ordered. Bus is fixed to #17 (84 passenger) per requirement (placeholder bus object).
        /// Returns tuple(pdfBytes, countEligible, totalConsidered).
        /// </summary>
        public async Task<(byte[] Pdf, int EligibleCount, int Total)> GenerateEligibilityRoutePdfAsync(BusBuddy.Core.Models.RouteTimeSlot slot = BusBuddy.Core.Models.RouteTimeSlot.AM)
        {
            var allStudents = new List<BusBuddy.Core.Models.Student>();
            try
            {
                if (_studentService is null)
                {
                    StatusMessage = "Student service unavailable";
                    return (Array.Empty<byte>(), 0, 0);
                }
                // Basic fetch (assumes service has method GetAllStudentsAsync or similar pattern) — fallback to empty
                var method = _studentService.GetType().GetMethod("GetAllStudentsAsync");
                if (method is not null)
                {
                    var taskObj = method.Invoke(_studentService, null) as Task<System.Collections.Generic.List<BusBuddy.Core.Models.Student>>;
                    if (taskObj != null)
                    {
                        var result = await taskObj.ConfigureAwait(false);
                        allStudents = result ?? new();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading students for eligibility route PDF");
            }

            if (allStudents.Count == 0)
            {
                return (Array.Empty<byte>(), 0, 0);
            }

            // Filter out Lamar addresses early (case-insensitive contains)
            var considered = allStudents.Where(s => string.IsNullOrWhiteSpace(s.HomeAddress) || !s.HomeAddress.Contains("Lamar", StringComparison.OrdinalIgnoreCase)).ToList();

            var eligibleStudents = new List<BusBuddy.Core.Models.Student>();
            if (_eligibilityService != null)
            {
                foreach (var stu in considered)
                {
                    if (stu.Latitude.HasValue && stu.Longitude.HasValue)
                    {
                        try
                        {
                            var ok = await _eligibilityService.IsEligibleAsync((double)stu.Latitude.Value, (double)stu.Longitude.Value);
                            if (ok)
                            {
                                eligibleStudents.Add(stu);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Eligibility check failed for student {Id}", stu.StudentId);
                        }
                    }
                }
            }

            if (eligibleStudents.Count == 0)
            {
                Logger.Information("No eligible students after filtering (Total={Total} Considered={Considered})", allStudents.Count, considered.Count);
                return (Array.Empty<byte>(), 0, considered.Count);
            }

            // ORDER STOPS (Nearest Neighbor heuristic) starting/ending at school coordinates.
            const double schoolLat = 38.1527; // Wiley School anchor
            const double schoolLon = -102.7204;
            var remaining = eligibleStudents.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
            var ordered = new List<BusBuddy.Core.Models.Student>();
            double currentLat = schoolLat, currentLon = schoolLon;
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
            // Assumptions (documented for MVP):
            //  • Departure from school: 06:50 local time (provided requirement).
            //  • Average route speed on county / rural roads: 35 mph (approximation; configurable later).
            //  • Dwell time per stop: 1 minute (boarding + safety check).
            //  • Return directly to school after last pickup.
            //  • Distance calculation: Haversine formula (great-circle) — acceptable rural approximation for MVP.
            var averageMph = Math.Max(5.0, AverageRouteSpeedMph); // safety floor
            var dwellPerStop = TimeSpan.FromMinutes(Math.Max(0, DwellMinutesPerStop));
            var departTimeOfDay = new TimeSpan(6, 50, 0); // 6:50 AM
            var cumulative = TimeSpan.Zero; // travel + dwell elapsed since departure
            double totalMiles = 0.0;
            var stops = new List<BusBuddy.Core.Models.RouteStop>();
            int order = 1;
            currentLat = schoolLat; currentLon = schoolLon;
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
                Logger.Error(ex, "PDF generation failed for eligibility route");
                pdf = Array.Empty<byte>();
            }

            Logger.Information("Eligibility route PDF generated Eligible={Eligible} Considered={Considered} Total={Total} Stops={Stops} Miles~{Miles:F1} ETA-Back={EtaBack}", eligibleStudents.Count, considered.Count, allStudents.Count, stops.Count, totalMiles, arrivalBack);
            StatusMessage = $"Eligibility route built: {stops.Count} stops ~{totalMiles:F1} mi back {arrivalBack:hh\\:mm}";
            return (pdf, eligibleStudents.Count, considered.Count);
        }

    // UI wrapper made public so MainWindow can trigger it without hosting the GoogleEarthView
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
                        File.WriteAllText(noDataNote, $"No eligibility PDF generated at {DateTime.UtcNow:O}. Eligible={eligible} Considered={considered}. This file is created so the folder is visible.\n" );
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
                Logger.Error(ex, "Eligibility PDF wrapper failed");
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
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<Point>();
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<Point>();
                }

                var list = new System.Collections.Generic.List<Point>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    switch (el.ValueKind)
                    {
                        case JsonValueKind.Object:
                            if (el.TryGetProperty("Latitude", out var latProp) && el.TryGetProperty("Longitude", out var lonProp))
                            {
                                if (latProp.TryGetDouble(out var lat) && lonProp.TryGetDouble(out var lon))
                                {
                                    list.Add(new Point(lat, lon));
                                }
                            }
                            break;
                        case JsonValueKind.Array:
                            if (el.GetArrayLength() >= 2)
                            {
                                var lat = el[0].GetDouble();
                                var lon = el[1].GetDouble();
                                list.Add(new Point(lat, lon));
                            }
                            break;
                    }
                }
                return list.ToArray();
            }
            catch
            {
                return Array.Empty<Point>();
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

        /// <summary>
        /// Serialize ordered student coordinates to a compact JSON array [[lat,lon], ...] for persistence in Route.WaypointsJson.
        /// </summary>
        private static string BuildWaypointsJson(System.Collections.Generic.IEnumerable<BusBuddy.Core.Models.Student> ordered)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var s in ordered)
            {
                if (!(s.Latitude.HasValue && s.Longitude.HasValue)) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('[')
                  .Append(s.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(s.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                  .Append(']');
            }
            sb.Append(']');
            return sb.ToString();
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
