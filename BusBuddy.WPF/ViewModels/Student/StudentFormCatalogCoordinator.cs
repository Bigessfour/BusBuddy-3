using System.Windows;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Loads route, pickup-stop, and school catalogs for the student form.
/// </summary>
public sealed class StudentFormCatalogCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormCatalogCoordinator>();

    private readonly BusBuddyDbContext _context;
    private readonly Core.Models.Student _student;
    private readonly List<(string Name, bool IsSpecialNeeds)> _routeCatalog;
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _availableRoutes;
    private readonly System.Collections.ObjectModel.ObservableCollection<PickupStop> _availablePickupStops;
    private readonly System.Collections.ObjectModel.ObservableCollection<Destination> _availableSchools;
    private readonly Action<PickupStop?> _syncSelectedPickupStop;
    private readonly Action<Destination?> _syncSelectedSchool;

    public StudentFormCatalogCoordinator(
        BusBuddyDbContext context,
        Core.Models.Student student,
        List<(string Name, bool IsSpecialNeeds)> routeCatalog,
        System.Collections.ObjectModel.ObservableCollection<string> availableRoutes,
        System.Collections.ObjectModel.ObservableCollection<PickupStop> availablePickupStops,
        System.Collections.ObjectModel.ObservableCollection<Destination> availableSchools,
        Action<PickupStop?> syncSelectedPickupStop,
        Action<Destination?> syncSelectedSchool)
    {
        _context = context;
        _student = student;
        _routeCatalog = routeCatalog;
        _availableRoutes = availableRoutes;
        _availablePickupStops = availablePickupStops;
        _availableSchools = availableSchools;
        _syncSelectedPickupStop = syncSelectedPickupStop;
        _syncSelectedSchool = syncSelectedSchool;
    }

    public async Task LoadAllAsync()
    {
        try
        {
            Logger.Information("Loading form data");

            var defaultRoutes = new[]
            {
                ("Route A", false),
                ("Route B", false),
                ("Route C", false),
                ("Route D", false),
                ("Special Needs Route", true),
            };
            var dbRoutes = new List<(string Name, bool IsSpecialNeeds)>();
            try
            {
                dbRoutes = await _context.Routes
                    .Where(r => r.IsActive)
                    .Select(r => new ValueTuple<string, bool>(
                        r.RouteName,
                        r.IsSpecialNeedsRoute || r.RouteName.Contains("Special Needs")))
                    .Distinct()
                    .OrderBy(n => n.Item1)
                    .ToListAsync()
                    .ConfigureAwait(true);
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
                    Logger.Warning("No routes in database — using placeholder route names for empty catalog");
                    foreach (var (name, isSpecial) in defaultRoutes)
                    {
                        if (routeNameSet.Add(name))
                        {
                            _routeCatalog.Add((name, isSpecial));
                        }
                    }
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
                    _syncSelectedPickupStop(_availablePickupStops.FirstOrDefault(s => s.PickupStopId == stopId));
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
}
