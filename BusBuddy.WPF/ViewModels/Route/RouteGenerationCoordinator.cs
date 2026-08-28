using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Shared year-start / transfer fleet generate path used by Route Assignment,
/// Route Management, and the main Routes pane.
/// </summary>
internal static class RouteGenerationCoordinator
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteGenerationCoordinator));

    public readonly record struct Outcome(
        bool Invoked,
        bool Success,
        string StatusMessage,
        RouteGenerationResult? Result);

    public static async Task<Outcome> GenerateAsync(
        FleetKind fleet,
        string? preferredSchoolName,
        bool preferSchoolWithStartTime,
        IRouteDeterminationService? planner = null)
    {
        planner ??= global::BusBuddy.WPF.App.ServiceProvider?.GetService<IRouteDeterminationService>();
        if (planner is null)
        {
            return new Outcome(false, false, "Route determination service unavailable", null);
        }

        var destinations = global::BusBuddy.WPF.App.ServiceProvider?.GetService<IDestinationService>();
        if (destinations is null)
        {
            return new Outcome(false, false, "Destination service unavailable", null);
        }

        var schools = await destinations.GetActiveSchoolsAsync().ConfigureAwait(true);
        if (schools.Count == 0)
        {
            return new Outcome(
                false,
                false,
                "No active school destinations — add a school first",
                null);
        }

        Destination? school = null;
        if (!string.IsNullOrWhiteSpace(preferredSchoolName))
        {
            school = schools.FirstOrDefault(s =>
                string.Equals(s.Name, preferredSchoolName, StringComparison.OrdinalIgnoreCase));
        }

        if (preferSchoolWithStartTime)
        {
            school ??= schools.FirstOrDefault(s => s.StartTime.HasValue);
        }

        school ??= schools[0];

        var verb = fleet == FleetKind.Transfer ? "transfer routes" : "routes";
        Logger.Information(
            "Generating {Verb} for school {SchoolId} {SchoolName} fleet={Fleet}",
            verb,
            school.DestinationId,
            school.Name,
            fleet);

        var result = await planner.GenerateAndAssignAsync(
                school.DestinationId,
                RouteTimeSlotKind.Both,
                fleet)
            .ConfigureAwait(true);

        if (!result.Success)
        {
            return new Outcome(true, false, result.Error ?? "Route generation failed", result);
        }

        var status = fleet == FleetKind.Transfer
            ? $"Transfer fleet: {result.Proposals.Count} proposal(s), {result.AssignedStudentCount} assigned"
            : $"Generated {result.Proposals.Count} proposal(s), assigned {result.AssignedStudentCount} student(s)" +
              (result.Warnings.Count > 0 ? $" — {result.Warnings[0]}" : string.Empty);

        return new Outcome(true, true, status, result);
    }
}
