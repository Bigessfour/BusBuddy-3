using System.Collections.ObjectModel;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Loads grades, schools, and routes for the students grid dropdowns.</summary>
public sealed class StudentsReferenceDataCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsReferenceDataCoordinator>();

    private readonly IBusBuddyDbContextFactory _contextFactory;

    public StudentsReferenceDataCoordinator(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task LoadAsync(
        ObservableCollection<string> availableGrades,
        ObservableCollection<Destination> availableSchools,
        ObservableCollection<string> availableRoutes,
        List<Destination> schoolCatalog,
        List<RouteModel> routeCatalog)
    {
        availableGrades.Clear();
        foreach (var grade in StudentGradeCatalog.All)
        {
            availableGrades.Add(grade);
        }

        using var context = _contextFactory.CreateDbContext();
        availableSchools.Clear();
        schoolCatalog.Clear();
        var schoolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var destService = App.ServiceProvider?.GetService<IDestinationService>();
            var destinations = destService is not null
                ? await destService.GetActiveSchoolsAsync().ConfigureAwait(true)
                : await context.Destinations
                    .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
                    .ToListAsync()
                    .ConfigureAwait(true);
            foreach (var d in destinations)
            {
                schoolCatalog.Add(d);
                schoolNames.Add(d.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Destination school catalog unavailable — falling back to student.School values");
        }

        var fromStudents = await context.Students
            .Where(s => !string.IsNullOrEmpty(s.School))
            .Select(s => s.School!)
            .Distinct()
            .ToListAsync()
            .ConfigureAwait(true);
        foreach (var school in fromStudents)
        {
            if (schoolNames.Add(school))
            {
                schoolCatalog.Add(new Destination { DestinationId = 0, Name = school });
            }
        }

        foreach (var school in schoolCatalog.OrderBy(s => s.Name))
        {
            availableSchools.Add(school);
        }

        availableRoutes.Clear();
        routeCatalog.Clear();
        var routes = await context.Routes
            .Where(r => r.IsActive)
            .OrderBy(r => r.RouteName)
            .ToListAsync()
            .ConfigureAwait(true);
        var routeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes)
        {
            routeCatalog.Add(route);
            if (!string.IsNullOrWhiteSpace(route.RouteName) && routeNames.Add(route.RouteName))
            {
                availableRoutes.Add(route.RouteName);
            }
        }

        Logger.Information(
            "Reference data loaded: {GradeCount} grades, {SchoolCount} schools, {RouteCount} routes",
            availableGrades.Count,
            availableSchools.Count,
            availableRoutes.Count);
    }
}
