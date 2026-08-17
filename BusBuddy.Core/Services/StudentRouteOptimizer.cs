using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Fills active routes from unassigned students (AM then PM), then asks
    /// <see cref="GrokGlobalAPI"/> for commentary. Ollama is the default AI path;
    /// a mock result is used when the model is unavailable (spec 004).
    /// Drive-path / Maps routing is intentionally not required — seat assignment must
    /// succeed even when <see cref="Interfaces.IRoutingService"/> is missing or fails.
    /// </summary>
    public sealed class StudentRouteOptimizer : IStudentRouteOptimizer
    {
        private static readonly ILogger Logger = Log.ForContext<StudentRouteOptimizer>();
        private readonly IRouteService _routeService;
        private readonly GrokGlobalAPI? _grok;
        private readonly Interfaces.IRoutingService? _routingService;

        public StudentRouteOptimizer(
            IRouteService routeService,
            GrokGlobalAPI? grok = null,
            Interfaces.IRoutingService? routingService = null)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _grok = grok;
            _routingService = routingService;
        }

        public async Task<StudentRouteOptimizeResult> OptimizeUnassignedAsync()
        {
            Logger.Information("OptimizeUnassignedAsync started");
            var routesResult = await _routeService.GetAllActiveRoutesAsync();
            if (!routesResult.IsSuccess || routesResult.Value is null)
            {
                Logger.Warning("Optimize aborted — could not load active routes: {Error}", routesResult.Error);
                return new StudentRouteOptimizeResult
                {
                    Status = routesResult.Error ?? "Could not load active routes."
                };
            }

            var routes = routesResult.Value.ToList();
            if (routes.Count == 0)
            {
                Logger.Information("Optimize aborted — no active routes");
                return new StudentRouteOptimizeResult
                {
                    Status = "No active routes to assign."
                };
            }

            var before = await CountUnassignedAsync();
            Logger.Information("Optimize loaded ActiveRoutes={RouteCount} UnassignedBefore={Unassigned}", routes.Count, before);
            if (before == 0)
            {
                Logger.Information("Optimize skipped — all active students already have AM and PM routes");
                return new StudentRouteOptimizeResult
                {
                    Status = "All active students already have AM and PM routes."
                };
            }

            var assigned = 0;
            foreach (var route in routes.OrderBy(r => r.RouteName, StringComparer.OrdinalIgnoreCase))
            {
                assigned += await AutoAssignSlotAsync(route.RouteId, RouteTimeSlot.AM);
                assigned += await AutoAssignSlotAsync(route.RouteId, RouteTimeSlot.PM);
            }

            // Fail-open: never block assignment results on Routes API errors.
            try
            {
                if (_routingService is not null)
                {
                    Logger.Debug("Optional routing service present; drive-path refresh is owned by map UI");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Routing side-effect skipped after optimize — assignments kept");
            }

            var remaining = await CountUnassignedAsync();
            var ai = await TryGetAiCommentaryAsync(assigned, remaining, routes);

            var status = assigned == 0
                ? "No students could be assigned (routes may be at capacity)."
                : $"Assigned {assigned} route slot(s); {remaining} student(s) still unassigned.";

            if (!string.IsNullOrWhiteSpace(ai.Summary))
            {
                status = $"{status} {ai.Summary}";
            }

            Logger.Information(
                "Route optimize finished AssignedSlots={Assigned} Remaining={Remaining} MockAi={Mock}",
                assigned, remaining, ai.UsedMock);

            return new StudentRouteOptimizeResult
            {
                AssignedCount = assigned,
                RemainingUnassigned = remaining,
                Status = status,
                AiSummary = ai.Summary,
                UsedMockAi = ai.UsedMock
            };
        }

        private async Task<int> AutoAssignSlotAsync(int routeId, RouteTimeSlot slot)
        {
            var result = await _routeService.AutoAssignStudentsAsync(routeId, slot);
            if (!result.IsSuccess || result.Value is null)
            {
                Logger.Warning("Auto-assign skipped for route {RouteId} {Slot}: {Error}", routeId, slot, result.Error);
                return 0;
            }

            Logger.Debug("Auto-assigned {Count} students to route {RouteId} {Slot}", result.Value.Count, routeId, slot);
            return result.Value.Count;
        }

        private async Task<int> CountUnassignedAsync()
        {
            var result = await _routeService.GetUnassignedStudentsAsync();
            return result.IsSuccess && result.Value is not null ? result.Value.Count : 0;
        }

        private async Task<(string? Summary, bool UsedMock)> TryGetAiCommentaryAsync(
            int assigned,
            int remaining,
            IReadOnlyList<Route> routes)
        {
            if (_grok is null)
            {
                return (null, false);
            }

            try
            {
                var result = await _grok.OptimizeRoutesAsync(new RouteOptimizationRequest
                {
                    RouteId = "fleet",
                    StudentsServed = assigned,
                    CurrentPerformance = $"{assigned} slots assigned; {remaining} students still unassigned",
                    TargetMetrics = "Fill active routes within capacity; keep Wiley riders on named routes",
                    Constraints = routes.Select(r => r.RouteName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()!
                });

                var usedMock = string.Equals(result.AIModel, "Mock-AI", StringComparison.OrdinalIgnoreCase);
                var firstLine = result.OptimizationSuggestions?
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    return (null, usedMock);
                }

                if (firstLine.Length > 160)
                {
                    firstLine = firstLine[..157] + "...";
                }

                return (usedMock ? $"AI (mock): {firstLine}" : $"AI: {firstLine}", usedMock);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "AI commentary skipped after route assignment");
                return (null, false);
            }
        }
    }
}
