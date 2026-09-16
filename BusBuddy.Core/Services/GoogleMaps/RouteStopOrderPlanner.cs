using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>
/// Clerk Optimize Order: pinned start/end via Route Optimization, then persist with
/// <see cref="IRouteService.ReorderRouteStopsAsync"/> (which refreshes the drive path).
/// </summary>
public static class RouteStopOrderPlanner
{
    public static async Task<Result<IReadOnlyList<int>>> ComputePinnedOrderAsync(
        IReadOnlyList<RouteStop> stops,
        IRouteOptimizationService optimization,
        int seatingCapacity,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optimization);

        if (!optimization.IsConfigured)
        {
            return Result.FailureResult<IReadOnlyList<int>>(
                "Route Optimization is not configured. Drive Path still uses Google Routes.");
        }

        var validated = stops
            .Where(s => s.HasValidatedCoordinates)
            .OrderBy(s => s.StopOrder)
            .ToList();
        if (validated.Count < 3)
        {
            return Result.FailureResult<IReadOnlyList<int>>(
                "Need at least three geocoded stops to optimize order. Use Drive Path for two-stop runs.");
        }

        var labeled = validated.Select(s => new RouteOptimizationStop
        {
            Label = s.RouteStopId.ToString(),
            Latitude = (double)s.Latitude!.Value,
            Longitude = (double)s.Longitude!.Value,
        }).ToList();

        var problem = RouteOptimizationVisitOrder.ForPinnedEnds(
            labeled,
            seatingCapacity: Math.Max(1, seatingCapacity > 0 ? seatingCapacity : 70),
            utcNow);
        var result = await optimization.OptimizeToursAsync(problem, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Result.FailureResult<IReadOnlyList<int>>(result.Error ?? "Route Optimization failed.");
        }

        var merged = RouteOptimizationVisitOrder.MergePinnedOrder(
            labeled.Select(s => s.Label).ToList(),
            result.Visits);
        var orderedIds = new List<int>(merged.Count);
        foreach (var label in merged)
        {
            if (!int.TryParse(label, out var id))
            {
                return Result.FailureResult<IReadOnlyList<int>>("Optimize Order returned an unexpected stop id.");
            }

            orderedIds.Add(id);
        }

        return Result.SuccessResult<IReadOnlyList<int>>(orderedIds);
    }
}
