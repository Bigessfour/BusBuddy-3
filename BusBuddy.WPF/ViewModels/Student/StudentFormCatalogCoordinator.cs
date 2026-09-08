using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Loads the route, pickup-stop, and school catalogs for the student form, and owns which pickup
/// stop the student boards at. specs/students.md keeps <c>PickupMode</c> derived: there is no mode
/// field here, only a catalog stop or the absence of one, which means home pickup.
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
        Action<Destination?> syncSelectedSchool)
    {
        _context = context;
        _student = student;
        _routeCatalog = routeCatalog;
        _availableRoutes = availableRoutes;
        _availablePickupStops = availablePickupStops;
        _availableSchools = availableSchools;
        _syncSelectedSchool = syncSelectedSchool;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The catalog stop this student boards at. Null means the bus comes to the home.</summary>
    public PickupStop? SelectedPickupStop
    {
        get => _selectedPickupStop;
        set
        {
            if (!SetProperty(ref _selectedPickupStop, value))
            {
                return;
            }

            _student.PickupStopId = value?.PickupStopId;
            if (value is not null)
            {
                _student.BusStop = value.Name;
            }

            OnPropertyChanged(nameof(UsesHomeAsPickupStop));
            PickupStopHint = value is null
                ? "No catalog stop selected — home address will be used when generating routes."
                : $"Boarding at {value.Name}.";
        }
    }

    /// <summary>Derived, never stored: no catalog stop means home pickup.</summary>
    public bool UsesHomeAsPickupStop => !_student.PickupStopId.HasValue;

    public string PickupStopHint
    {
        get => _pickupStopHint;
        private set => SetProperty(ref _pickupStopHint, value);
    }

    /// <summary>
    /// Offers the nearest catalog stop within the district's walk radius. Requires validated
    /// coordinates: an unvalidated address has no position to measure from.
    /// </summary>
    public async Task SuggestNearestPickupStopAsync()
    {
        if (_student.Latitude is not decimal lat || _student.Longitude is not decimal lon)
        {
            PickupStopHint = "Validate the home address first to suggest a nearby catalog stop.";
            return;
        }

        var stopService = App.ServiceProvider?.GetService<IPickupStopService>();
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
            PickupStopHint = $"No catalog stop within {maxMeters:F0} m — use home as stop or add a pickup stop.";
            return;
        }

        SelectedPickupStop = nearest;
        PickupStopHint = $"Suggested {nearest.Name} (within {maxMeters:F0} m of home).";
    }

    /// <summary>Rural / driveway pickup: clear the catalog stop so routing uses the home address.</summary>
    public void UseHomeAsPickupStop()
    {
        SelectedPickupStop = null;
        _student.PickupStopId = null;
        _student.BusStop = "Home address";
        PickupStopHint = "Using home address as pickup stop (rural / driveway).";
        OnPropertyChanged(nameof(UsesHomeAsPickupStop));
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
            var stopService = App.ServiceProvider?.GetService<IPickupStopService>();
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
