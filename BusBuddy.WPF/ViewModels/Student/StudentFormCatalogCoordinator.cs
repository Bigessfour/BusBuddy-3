using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.WPF.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Loads the route, pickup-stop, and school catalogs for the student form, and owns which pickup
/// stop the student boards at. specs/students.md records <c>PickupMode</c> as Home vs CatalogStop
/// by whether a published stop is attached; special needs always stays Home.
/// </summary>
public sealed class StudentFormCatalogCoordinator : INotifyPropertyChanged
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormCatalogCoordinator>();

    private readonly BusBuddyDbContext _context;
    private readonly Core.Models.Student _student;
    private readonly List<(string Name, bool IsSpecialNeeds)> _routeCatalog;
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _availableRoutes;
    private readonly System.Collections.ObjectModel.ObservableCollection<PickupStop> _availablePickupStops;
    private readonly System.Collections.ObjectModel.ObservableCollection<Destination> _availableSchools;
    private readonly Action<Destination?> _syncSelectedSchool;
    private readonly IStudentService? _studentService;
    private readonly IPickupStopService? _pickupStops;

    private PickupStop? _selectedPickupStop;
    private string _pickupStopHint =
        "Assign a catalog stop near the home address, or use home as stop (rural).";

    public StudentFormCatalogCoordinator(
        BusBuddyDbContext context,
        Core.Models.Student student,
        List<(string Name, bool IsSpecialNeeds)> routeCatalog,
        System.Collections.ObjectModel.ObservableCollection<string> availableRoutes,
        System.Collections.ObjectModel.ObservableCollection<PickupStop> availablePickupStops,
        System.Collections.ObjectModel.ObservableCollection<Destination> availableSchools,
        Action<Destination?> syncSelectedSchool,
        IStudentService? studentService = null,
        IPickupStopService? pickupStops = null)
    {
        _context = context;
        _student = student;
        _routeCatalog = routeCatalog;
        _availableRoutes = availableRoutes;
        _availablePickupStops = availablePickupStops;
        _availableSchools = availableSchools;
        _syncSelectedSchool = syncSelectedSchool;
        _studentService = studentService;
        _pickupStops = pickupStops;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The catalog stop this student boards at. Null means the bus comes to the home.</summary>
    public PickupStop? SelectedPickupStop
    {
        get => _selectedPickupStop;
        set
        {
            if (value is not null && IsSpecialNeedsHomePickup)
            {
                Logger.Information(
                    "Blocked catalog stop for special-needs student — PickupMode stays Home");
                value = null;
            }

            if (!SetProperty(ref _selectedPickupStop, value))
            {
                return;
            }

            _student.PickupStopId = value?.PickupStopId;
            if (value is not null)
            {
                _student.BusStop = value.Name;
            }
            else
            {
                ApplyHomePickupFields();
            }

            OnPropertyChanged(nameof(UsesHomeAsPickupStop));
            PickupStopHint = value is null
                ? IsSpecialNeedsHomePickup
                    ? "Special needs requires home pickup on a special-needs route — catalog stop not assigned."
                    : "No catalog stop selected — home address will be used when generating routes."
                : $"Boarding at {value.Name}.";
        }
    }

    /// <summary>Derived, never stored: no catalog stop means home pickup.</summary>
    public bool UsesHomeAsPickupStop => !_student.PickupStopId.HasValue;

    /// <summary>
    /// specs/students.md: special needs forces home pickup on a special-needs route.
    /// A catalog stop must not be assigned, including via suggest-nearest after geocode.
    /// </summary>
    public bool IsSpecialNeedsHomePickup =>
        StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(_student);

    public string PickupStopHint
    {
        get => _pickupStopHint;
        private set => SetProperty(ref _pickupStopHint, value);
    }

    /// <summary>
    /// After a home geocode: hint a nearby catalog stop without assigning it, or keep Home
    /// pickup when none is in range. May hint that other Home students are nearby.
    /// </summary>
    public async Task RefreshPickupSuggestionAsync()
    {
        if (EnforceHomePickupIfSpecialNeeds())
        {
            return;
        }

        if (_student.Latitude is not decimal lat || _student.Longitude is not decimal lon
            || !LocationCoordinate.IsValidated(lat, lon))
        {
            PublishPickupHint("Validate the home address first to suggest a nearby catalog stop.");
            return;
        }

        var stopService = ResolvePickupStops();
        var maxMeters = DistrictCameraUi.CurrentSettings()?.StopSuggestMaxMeters ?? 400;
        var clusterMin = DistrictCameraUi.CurrentSettings()?.CatalogStopClusterMinHomes ?? 2;
        PickupStop? nearest = null;
        if (stopService is not null)
        {
            nearest = await stopService
                .FindNearestAsync((double)lat, (double)lon, maxMeters)
                .ConfigureAwait(true);
        }

        if (nearest is null)
        {
            PublishPickupHint(await HomePickupHintAsync(maxMeters, clusterMin).ConfigureAwait(true));
            return;
        }

        if (_student.PickupStopId is int assignedId)
        {
            var assigned = _availablePickupStops.FirstOrDefault(s => s.PickupStopId == assignedId)
                ?? nearest;
            if (assigned.PickupStopId == assignedId
                && assigned.HasValidatedCoordinates
                && IsWithinMeters((double)lat, (double)lon, assigned.Latitude, assigned.Longitude, maxMeters))
            {
                PublishPickupHint(StudentPickupHint.AssignedCatalogStillInRange(assigned.Name, maxMeters));
                return;
            }

            UseHomeAsPickupStop();
        }

        PublishPickupHint(StudentPickupHint.NearbyCatalog(nearest.Name, maxMeters));
    }

    /// <summary>
    /// Clerk chose Suggest nearest: assign the catalog stop if one is in range; otherwise Home.
    /// </summary>
    public async Task SuggestNearestPickupStopAsync()
    {
        if (EnforceHomePickupIfSpecialNeeds())
        {
            return;
        }

        if (_student.Latitude is not decimal lat || _student.Longitude is not decimal lon
            || !LocationCoordinate.IsValidated(lat, lon))
        {
            PublishPickupHint("Validate the home address first to suggest a nearby catalog stop.");
            return;
        }

        var stopService = ResolvePickupStops();
        if (stopService is null)
        {
            PickupStopHint = "Pickup stop service is not available.";
            return;
        }

        var maxMeters = DistrictCameraUi.CurrentSettings()?.StopSuggestMaxMeters ?? 400;
        var nearest = await stopService
            .FindNearestAsync((double)lat, (double)lon, maxMeters)
            .ConfigureAwait(true);
        if (nearest is null)
        {
            UseHomeAsPickupStop();
            var clusterMin = DistrictCameraUi.CurrentSettings()?.CatalogStopClusterMinHomes ?? 2;
            PickupStopHint = await HomePickupHintAsync(maxMeters, clusterMin).ConfigureAwait(true);
            return;
        }

        SelectedPickupStop = nearest;
        PickupStopHint = $"Assigned {nearest.Name} (within {maxMeters:F0} m of home).";
    }

    /// <summary>Rural / driveway pickup: clear the catalog stop so routing uses the home address.</summary>
    public void UseHomeAsPickupStop()
    {
        SelectedPickupStop = null;
        ApplyHomePickupFields();
        PickupStopHint = IsSpecialNeedsHomePickup
            ? "Special needs requires home pickup on a special-needs route — catalog stop not assigned."
            : "Using home address as pickup stop (rural / driveway).";
        OnPropertyChanged(nameof(UsesHomeAsPickupStop));
    }

    /// <summary>
    /// Clears any catalog stop when special needs is on. Returns true when home pickup was forced.
    /// </summary>
    public bool EnforceHomePickupIfSpecialNeeds()
    {
        if (!IsSpecialNeedsHomePickup)
        {
            return false;
        }

        UseHomeAsPickupStop();
        return true;
    }

    private void ApplyHomePickupFields()
    {
        _student.PickupStopId = null;
        _student.BusStop = "Home address";
    }

    private async Task<string> HomePickupHintAsync(double maxMeters, int clusterMin)
    {
        var others = 0;
        var students = ResolveStudents();
        if (students is not null
            && LocationCoordinate.IsValidated(_student.Latitude, _student.Longitude))
        {
            var nearby = await students.GetNearbyHomePickupStudentsAsync(
                    (double)_student.Latitude!,
                    (double)_student.Longitude!,
                    maxMeters,
                    excludeStudentId: _student.StudentId > 0 ? _student.StudentId : null)
                .ConfigureAwait(true);
            others = nearby?.Count ?? 0;
        }

        if (StudentPickupHint.ShouldHintCatalogCluster(others, clusterMin))
        {
            return StudentPickupHint.HomeWithCluster(others, maxMeters);
        }

        return _availablePickupStops.Count == 0
            ? StudentPickupHint.NoPublishedCatalog(maxMeters)
            : StudentPickupHint.HomeOnly(maxMeters);
    }

    private void PublishPickupHint(string text)
    {
        PickupStopHint = text;
        Logger.Information(
            "Pickup hint StudentId={StudentId} HasPin={HasPin} Text={Hint}",
            _student.StudentId,
            _student.HasValidatedHomeCoordinates,
            text);
    }

    private IPickupStopService? ResolvePickupStops() =>
        _pickupStops ?? App.ServiceProvider?.GetService<IPickupStopService>();

    private IStudentService? ResolveStudents() =>
        _studentService ?? App.ServiceProvider?.GetService<IStudentService>();

    private static bool IsWithinMeters(
        double lat,
        double lon,
        decimal stopLat,
        decimal stopLon,
        double maxMeters)
    {
        var meters = RoutePacker.HaversineMiles(lat, lon, (double)stopLat, (double)stopLon) * 1609.344;
        return meters <= maxMeters;
    }

    public async Task LoadAllAsync()
    {
        try
        {
            Logger.Information("Loading form data");

            var dbRoutes = new List<(string Name, bool IsSpecialNeeds)>();
            try
            {
                // Project in memory: Npgsql cannot translate ValueTuple + Distinct + string.Contains.
                var rows = await _context.Routes
                    .AsNoTracking()
                    .Where(r => r.IsActive)
                    .Select(r => new { r.RouteName, r.IsSpecialNeedsRoute })
                    .ToListAsync()
                    .ConfigureAwait(true);

                dbRoutes = rows
                    .Select(r => (
                        r.RouteName ?? string.Empty,
                        StudentSpecialNeedsHelper.IsSpecialNeedsRoute(r.RouteName, r.IsSpecialNeedsRoute)))
                    .Where(r => !string.IsNullOrWhiteSpace(r.Item1))
                    .Distinct()
                    .OrderBy(n => n.Item1)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to load routes from DB — falling back to defaults");
            }

            await RunOnUiAsync(() =>
            {
                _routeCatalog.Clear();
                var routeNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (name, isSpecial) in dbRoutes)
                {
                    var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && routeNameSet.Add(trimmed))
                    {
                        _routeCatalog.Add((trimmed, isSpecial));
                    }
                }

                if (dbRoutes.Count == 0)
                {
                    // No placeholders: AMRoute/PMRoute are validated against the Routes table on save,
                    // so a made-up name would either be rejected or persist a route that does not exist.
                    Logger.Warning("No active routes in database — route pickers will be empty until routes are created");
                }

                RefreshAvailableRoutes();
            }).ConfigureAwait(true);

            await LoadPickupStopsAsync().ConfigureAwait(true);
            await LoadSchoolsAsync().ConfigureAwait(true);
            await RefreshPickupSuggestionAsync().ConfigureAwait(true);

            Logger.Information(
                "Form data loaded: {RouteCount} routes, {PickupStopCount} pickup stops, {SchoolCount} schools",
                _availableRoutes.Count,
                _availablePickupStops.Count,
                _availableSchools.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading form data");
        }
    }

    public void RefreshAvailableRoutes()
    {
        var specialOnly = _student.RequiresSpecialNeedsBus
            || StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(_student);
        var names = _routeCatalog
            .Where(r => specialOnly ? r.IsSpecialNeeds : !r.IsSpecialNeeds)
            .Select(r => r.Name)
            .OrderBy(n => n)
            .ToList();

        if (names.Count == 0)
        {
            names = _routeCatalog.Select(r => r.Name).OrderBy(n => n).ToList();
        }

        _availableRoutes.Clear();
        foreach (var name in names)
        {
            _availableRoutes.Add(name);
        }

        if (specialOnly)
        {
            if (!string.IsNullOrWhiteSpace(_student.AMRoute) &&
                !_routeCatalog.Any(r => r.Name.Equals(_student.AMRoute, StringComparison.OrdinalIgnoreCase) && r.IsSpecialNeeds))
            {
                _student.AMRoute = names.FirstOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(_student.PMRoute) &&
                !_routeCatalog.Any(r => r.Name.Equals(_student.PMRoute, StringComparison.OrdinalIgnoreCase) && r.IsSpecialNeeds))
            {
                _student.PMRoute = names.FirstOrDefault();
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(_student.AMRoute) &&
                _routeCatalog.Any(r => r.Name.Equals(_student.AMRoute, StringComparison.OrdinalIgnoreCase) && r.IsSpecialNeeds))
            {
                _student.AMRoute = names.FirstOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(_student.PMRoute) &&
                _routeCatalog.Any(r => r.Name.Equals(_student.PMRoute, StringComparison.OrdinalIgnoreCase) && r.IsSpecialNeeds))
            {
                _student.PMRoute = names.FirstOrDefault();
            }
        }
    }

    public async Task LoadPickupStopsAsync()
    {
        try
        {
            var stopService = ResolvePickupStops();
            IReadOnlyList<PickupStop> stops;
            if (stopService is not null)
            {
                stops = await stopService.GetActiveStopsAsync().ConfigureAwait(true);
            }
            else
            {
                stops = await _context.PickupStops
                    .Where(s => s.Active)
                    .OrderBy(s => s.Name)
                    .ToListAsync()
                    .ConfigureAwait(true);
            }

            await RunOnUiAsync(() =>
            {
                _availablePickupStops.Clear();
                foreach (var stop in stops)
                {
                    _availablePickupStops.Add(stop);
                }

                if (_student.PickupStopId is int stopId)
                {
                    SelectedPickupStop = _availablePickupStops.FirstOrDefault(s => s.PickupStopId == stopId);
                }
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load pickup stops");
        }
    }

    public async Task LoadSchoolsAsync()
    {
        try
        {
            var destService = App.ServiceProvider?.GetService<IDestinationService>();
            IReadOnlyList<Destination> schools;
            if (destService is not null)
            {
                schools = await destService.GetActiveSchoolsAsync().ConfigureAwait(true);
            }
            else
            {
                schools = await _context.Destinations
                    .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
                    .OrderBy(d => d.Name)
                    .ToListAsync()
                    .ConfigureAwait(true);
            }

            await RunOnUiAsync(() =>
            {
                _availableSchools.Clear();
                foreach (var school in schools)
                {
                    _availableSchools.Add(school);
                }

                if (_student.DestinationId.HasValue)
                {
                    _syncSelectedSchool(_availableSchools.FirstOrDefault(s => s.DestinationId == _student.DestinationId.Value));
                }
                else if (!string.IsNullOrWhiteSpace(_student.School))
                {
                    _syncSelectedSchool(_availableSchools.FirstOrDefault(s =>
                        string.Equals(s.Name, _student.School, StringComparison.OrdinalIgnoreCase)));
                }
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load school destinations — intake school dropdown may be empty");
        }
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
