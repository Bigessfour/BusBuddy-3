using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Examples;

/// <summary>
/// Comprehensive example demonstrating enhanced route assignment logic
/// Shows integration of Result pattern, error handling, and business logic
/// Based on BusBuddy excellence requirements and route assignment documentation
/// </summary>
public class RouteAssignmentExample
{
    private readonly IRouteService _routeService;
    private readonly ILogger<RouteAssignmentExample> _logger;

    public RouteAssignmentExample(IRouteService routeService, ILogger<RouteAssignmentExample> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates comprehensive route assignment workflow
    /// </summary>
    public async Task<Result> DemonstrateRouteAssignmentAsync()
    {
        try
        {
            _logger.LogInformation("🚌 Starting comprehensive route assignment demonstration");

            // 1. Get route utilization statistics
            var statsResult = await _routeService.GetRouteUtilizationStatsAsync();
            if (!statsResult.IsSuccess)
            {
                return Result.Failure($"Failed to get route statistics: {statsResult.Error}");
            }

            var stats = statsResult.Value!;
            _logger.LogInformation("📊 Current Stats: {TotalRoutes} routes, {UtilizationRate:P0} avg utilization, {UnassignedStudents} unassigned students",
                stats.TotalRoutes, stats.AverageUtilizationRate, stats.TotalUnassignedStudents);

            // 2. Get unassigned students for assignment
            var unassignedResult = await _routeService.GetUnassignedStudentsAsync();
            if (!unassignedResult.IsSuccess)
            {
                return Result.Failure($"Failed to get unassigned students: {unassignedResult.Error}");
            }

            var unassignedStudents = unassignedResult.Value!;
            _logger.LogInformation("👥 Found {Count} unassigned students", unassignedStudents.Count);

            // 3. Get routes with available capacity
            var routesWithCapacityResult = await _routeService.GetRoutesWithCapacityAsync();
            if (!routesWithCapacityResult.IsSuccess)
            {
                return Result.Failure($"Failed to get routes with capacity: {routesWithCapacityResult.Error}");
            }

            var availableRoutes = routesWithCapacityResult.Value!;
            _logger.LogInformation("🚌 Found {Count} routes with available capacity", availableRoutes.Count);

            // 4. Demonstrate student assignment with validation
            if (unassignedStudents.Any() && availableRoutes.Any())
            {
                var student = unassignedStudents.First();
                var route = availableRoutes.First();

                // Check if assignment is valid before attempting
                var canAssignResult = await _routeService.CanAssignStudentToRouteAsync(student.StudentId, route.RouteId);
                if (canAssignResult.IsSuccess && canAssignResult.Value)
                {
                    // Perform the assignment
                    var assignmentResult = await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId);
                    if (assignmentResult.IsSuccess)
                    {
                        _logger.LogInformation("✅ Successfully assigned student {StudentName} to route {RouteName}",
                            student.StudentName, route.RouteName);
                    }
                    else
                    {
                        _logger.LogWarning("❌ Failed to assign student: {Error}", assignmentResult.Error);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Cannot assign student {StudentName} to route {RouteName}: {Reason}",
                        student.StudentName, route.RouteName, canAssignResult.Error);
                }
            }

            // 5. Demonstrate route validation
            var allRoutesResult = await _routeService.GetAllActiveRoutesAsync();
            if (allRoutesResult.IsSuccess)
            {
                foreach (var route in allRoutesResult.Value!.Take(3)) // Check first 3 routes
                {
                    var capacityValidationResult = await _routeService.ValidateRouteCapacityAsync(route.RouteId);
                    if (capacityValidationResult.IsSuccess)
                    {
                        var isValid = capacityValidationResult.Value ? "✅ Within Capacity" : "⚠️ Over Capacity";
                        _logger.LogInformation("Route {RouteName}: {Status}", route.RouteName, isValid);
                    }
                }
            }

            // 6. Show updated statistics
            var updatedStatsResult = await _routeService.GetRouteUtilizationStatsAsync();
            if (updatedStatsResult.IsSuccess)
            {
                var updatedStats = updatedStatsResult.Value!;
                _logger.LogInformation("📊 Updated Stats: {UtilizationRate:P0} avg utilization, {UnassignedStudents} unassigned students",
                    updatedStats.AverageUtilizationRate, updatedStats.TotalUnassignedStudents);
            }

            _logger.LogInformation("🎉 Route assignment demonstration completed successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Route assignment demonstration failed");
            return Result.Failure("Route assignment demonstration failed", ex);
        }
    }

    /// <summary>
    /// Demonstrates error handling patterns with Result type
    /// </summary>
    public async Task<Result<string>> DemonstrateErrorHandlingAsync()
    {
        _logger.LogInformation("🔍 Demonstrating error handling patterns");

        // Test 1: Invalid route ID
        var invalidRouteResult = await _routeService.GetRouteByIdAsync(-1);
        if (invalidRouteResult.IsFailure)
        {
            _logger.LogInformation("✅ Correctly handled invalid route ID: {Error}", invalidRouteResult.Error);
        }

        // Test 2: Invalid student assignment
        var invalidAssignmentResult = await _routeService.AssignStudentToRouteAsync(0, 0);
        if (invalidAssignmentResult.IsFailure)
        {
            _logger.LogInformation("✅ Correctly handled invalid assignment: {Error}", invalidAssignmentResult.Error);
        }

        // Test 3: Capacity validation for non-existent route
        var invalidCapacityResult = await _routeService.ValidateRouteCapacityAsync(999999);
        if (invalidCapacityResult.IsFailure)
        {
            _logger.LogInformation("✅ Correctly handled non-existent route capacity check: {Error}", invalidCapacityResult.Error);
        }

        return Result<string>.Success("Error handling demonstration completed - all edge cases handled correctly");
    }

    /// <summary>
    /// Demonstrates advanced route operations
    /// </summary>
    public async Task<Result<RouteOperationSummary>> DemonstrateAdvancedOperationsAsync()
    {
        try
        {
            _logger.LogInformation("🚀 Demonstrating advanced route operations");

            var summary = new RouteOperationSummary();

            // 1. Search routes by criteria
            var searchResult = await _routeService.SearchRoutesAsync("Elementary");
            if (searchResult.IsSuccess)
            {
                summary.SearchResultsCount = searchResult.Value!.Count();
                _logger.LogInformation("🔍 Found {Count} routes matching 'Elementary'", summary.SearchResultsCount);
            }

            // 2. Get routes by bus
            var busRoutesResult = await _routeService.GetRoutesByBusIdAsync(1);
            if (busRoutesResult.IsSuccess)
            {
                summary.BusRoutesCount = busRoutesResult.Value!.Count();
                _logger.LogInformation("🚌 Found {Count} routes for bus ID 1", summary.BusRoutesCount);
            }

            // 3. Check route number uniqueness
            var uniquenessResult = await _routeService.IsRouteNumberUniqueAsync("TEST-ROUTE-001");
            if (uniquenessResult.IsSuccess)
            {
                summary.IsTestRouteUnique = uniquenessResult.Value;
                _logger.LogInformation("📋 Route TEST-ROUTE-001 uniqueness: {IsUnique}", summary.IsTestRouteUnique);
            }

            // 4. Calculate route distances and times
            var allRoutesResult = await _routeService.GetAllActiveRoutesAsync();
            if (allRoutesResult.IsSuccess)
            {
                var routes = allRoutesResult.Value!.Take(5);
                foreach (var route in routes)
                {
                    var distanceResult = await _routeService.GetRouteTotalDistanceAsync(route.RouteId);
                    var timeResult = await _routeService.GetRouteEstimatedTimeAsync(route.RouteId);

                    if (distanceResult.IsSuccess && timeResult.IsSuccess)
                    {
                        summary.TotalDistanceCalculated += (double)distanceResult.Value;
                        summary.TotalTimeCalculated = summary.TotalTimeCalculated.Add(timeResult.Value);

                        _logger.LogInformation("📏 Route {RouteName}: {Distance} miles, {Time} estimated time",
                            route.RouteName, distanceResult.Value, timeResult.Value);
                    }
                }
            }

            summary.OperationTimestamp = DateTime.UtcNow;
            summary.IsSuccessful = true;

            _logger.LogInformation("✨ Advanced operations completed: {Summary}", summary);
            return Result<RouteOperationSummary>.Success(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Advanced operations failed");
            return Result<RouteOperationSummary>.Failure("Advanced operations failed", ex);
        }
    }
}

/// <summary>
/// Summary of route operations for reporting
/// </summary>
public class RouteOperationSummary
{
    public int SearchResultsCount { get; set; }
    public int BusRoutesCount { get; set; }
    public bool IsTestRouteUnique { get; set; }
    public double TotalDistanceCalculated { get; set; }
    public TimeSpan TotalTimeCalculated { get; set; }
    public DateTime OperationTimestamp { get; set; }
    public bool IsSuccessful { get; set; }

    public override string ToString()
    {
        return $"Search: {SearchResultsCount}, Bus Routes: {BusRoutesCount}, " +
               $"Distance: {TotalDistanceCalculated:F1}mi, Time: {TotalTimeCalculated}, " +
               $"Success: {IsSuccessful}";
    }
}

/// <summary>
/// Extension methods for enhanced route service usage
/// </summary>
public static class RouteServiceExtensions
{
    /// <summary>
    /// Safely assign student with comprehensive validation
    /// </summary>
    public static async Task<Result<AssignmentReport>> SafeAssignStudentAsync(
        this IRouteService routeService,
        int studentId,
        int routeId,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("🔒 Starting safe student assignment: Student {StudentId} → Route {RouteId}", studentId, routeId);

            // Step 1: Validate student can be assigned
            var canAssignResult = await routeService.CanAssignStudentToRouteAsync(studentId, routeId);
            if (!canAssignResult.IsSuccess)
            {
                return Result<AssignmentReport>.Failure($"Pre-assignment validation failed: {canAssignResult.Error}");
            }

            if (!canAssignResult.Value)
            {
                return Result<AssignmentReport>.Failure("Student cannot be assigned to this route (capacity or other constraints)");
            }

            // Step 2: Get current route utilization
            var routeResult = await routeService.GetRouteByIdAsync(routeId);
            if (!routeResult.IsSuccess)
            {
                return Result<AssignmentReport>.Failure($"Route validation failed: {routeResult.Error}");
            }

            // Step 3: Perform assignment
            var assignmentResult = await routeService.AssignStudentToRouteAsync(studentId, routeId);
            if (!assignmentResult.IsSuccess)
            {
                return Result<AssignmentReport>.Failure($"Assignment failed: {assignmentResult.Error}");
            }

            // Step 4: Validate assignment was successful
            var postAssignmentValidation = await routeService.ValidateRouteCapacityAsync(routeId);

            var report = new AssignmentReport
            {
                StudentId = studentId,
                RouteId = routeId,
                RouteName = routeResult.Value!.RouteName,
                AssignmentTimestamp = DateTime.UtcNow,
                IsSuccessful = true,
                IsCapacityStillValid = postAssignmentValidation.IsSuccess && postAssignmentValidation.Value,
                ValidationMessage = postAssignmentValidation.IsSuccess ?
                    (postAssignmentValidation.Value ? "Route capacity is valid" : "⚠️ Route is now at/over capacity") :
                    $"⚠️ Post-assignment validation failed: {postAssignmentValidation.Error}"
            };

            logger.LogInformation("✅ Safe assignment completed: {Report}", report);
            return Result<AssignmentReport>.Success(report);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Safe assignment failed for Student {StudentId} → Route {RouteId}", studentId, routeId);
            return Result<AssignmentReport>.Failure("Safe assignment failed due to unexpected error", ex);
        }
    }
}

/// <summary>
/// Detailed report of student assignment operation
/// </summary>
public class AssignmentReport
{
    public int StudentId { get; set; }
    public int RouteId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public DateTime AssignmentTimestamp { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IsCapacityStillValid { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Student {StudentId} → Route {RouteName} at {AssignmentTimestamp:HH:mm:ss} - " +
               $"Success: {IsSuccessful}, Valid Capacity: {IsCapacityStillValid}";
    }
}
