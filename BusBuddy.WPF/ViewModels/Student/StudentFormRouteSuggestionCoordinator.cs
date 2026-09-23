using System.Windows.Media;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Proposes AM/PM route names for a student from the active route catalog, preferring routes whose
/// name matches the student's city. Suggestions are written onto the student for the clerk to accept
/// or overwrite; they are never persisted here, and they never create a route that does not exist.
/// </summary>
public sealed class StudentFormRouteSuggestionCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormRouteSuggestionCoordinator>();

    private const int MaxSuggestions = 2;

    private readonly Func<StudentModel> _student;
    private readonly StudentFormValidationCoordinator _validation;

    public StudentFormRouteSuggestionCoordinator(
        Func<StudentModel> student,
        StudentFormValidationCoordinator validation)
    {
        _student = student;
        _validation = validation;
    }

    public async Task SuggestRoutesAsync()
    {
        var student = _student();
        try
        {
            Logger.Information("Starting route suggestion for student StudentId={StudentId}", student.StudentId);

            if (string.IsNullOrWhiteSpace(student.HomeAddress))
            {
                _validation.SetGlobalError("Please enter a home address before requesting route suggestions.");
                return;
            }

            _validation.IsValidating = true;
            _validation.SetStatus("Analyzing address with AI...", Brushes.Orange);

            var suggested = await GetSuggestedRoutesAsync(student.City).ConfigureAwait(true);
            if (suggested.Count == 0)
            {
                _validation.SetStatus("⚠️ No optimal routes found for this location", Brushes.Orange);
                Logger.Information("Route suggestion returned no candidates");
                return;
            }

            StudentRouteAssignment.SetSlot(student, RouteTimeSlot.AM, suggested[0]);
            if (suggested.Count > 1)
            {
                StudentRouteAssignment.SetSlot(student, RouteTimeSlot.PM, suggested[1]);
            }

            _validation.SetStatus($"✓ AI suggested {suggested.Count} optimal routes", Brushes.Green);
            Logger.Information("Route suggestion completed with {Count} candidate(s)", suggested.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during route suggestion");
            _validation.SetGlobalError($"AI route suggestion failed: {ex.Message}");
            _validation.SetStatus("❌ AI suggestion failed", Brushes.Red);
        }
        finally
        {
            _validation.IsValidating = false;
        }
    }

    /// <summary>
    /// City-matched active routes first, then any active routes. Suggestions carry the route key
    /// so save treats <c>AMRoute</c>/<c>PMRoute</c> as the display mirror of that key.
    /// </summary>
    private static async Task<List<RouteModel>> GetSuggestedRoutesAsync(string? city)
    {
        var sp = App.ServiceProvider;
        if (sp is null)
        {
            return [];
        }

        using var scope = sp.CreateScope();
        var routeService = scope.ServiceProvider.GetService<IRouteService>();
        if (routeService is null)
        {
            return [];
        }

        var result = await routeService.GetAllActiveRoutesAsync();
        if (!result.IsSuccess || result.Value is null)
        {
            return [];
        }

        var routes = result.Value
            .Where(r => r.RouteId > 0 && !string.IsNullOrWhiteSpace(r.RouteName))
            .ToList();

        var cityMatches = string.IsNullOrWhiteSpace(city)
            ? []
            : routes.Where(r => r.RouteName.Contains(city, StringComparison.OrdinalIgnoreCase))
                .Take(MaxSuggestions)
                .ToList();

        return cityMatches.Count > 0 ? cityMatches : routes.Take(MaxSuggestions).ToList();
    }
}
