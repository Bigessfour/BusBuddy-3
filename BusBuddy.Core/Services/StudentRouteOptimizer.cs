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
    /// <see cref="OllamaAiService"/> for commentary. A mock result is used when
    /// Ollama is unavailable (spec 004).
    /// Seat assignment is deliberately independent of Maps routing: the drive path belongs to the
    /// map UI, so no routing service is taken here and a Routes API outage cannot block assignment.
    /// </summary>
    public sealed class StudentRouteOptimizer : IStudentRouteOptimizer
    {
        private static readonly ILogger Logger = Log.ForContext<StudentRouteOptimizer>();
        private readonly IRouteService _routeService;
        private readonly OllamaAiService? _ollama;

        public StudentRouteOptimizer(
            IRouteService routeService,
            OllamaAiService? ollama = null)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _ollama = ollama;
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
                Logger.Information("Optimize skipped — every active student has both an AM and a PM route");
                return new StudentRouteOptimizeResult
                {
                    Status = "All active students already have both an AM and a PM route."
                };
            }

            var assigned = 0;
            foreach (var route in routes.OrderBy(r => r.RouteName, StringComparer.OrdinalIgnoreCase))
            {
                assigned += await AutoAssignSlotAsync(route.RouteId, RouteSession.ToAssignmentSlot(route));
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

        /// <summary>
        /// Students still short at least one slot. The parameterless
        /// <see cref="IRouteService.GetUnassignedStudentsAsync()"/> only returns children missing
        /// <em>both</em> runs, so a child with an AM route and no PM route looked done and the whole
        /// optimize pass exited early reporting nothing left to do.
        /// </summary>
        private async Task<int> CountUnassignedAsync()
        {
            var am = await _routeService.GetUnassignedStudentsAsync(RouteTimeSlot.AM);
            var pm = await _routeService.GetUnassignedStudentsAsync(RouteTimeSlot.PM);
            if (!am.IsSuccess || am.Value is null || !pm.IsSuccess || pm.Value is null)
            {
                Logger.Warning(
                    "Unassigned count unavailable AM={AmError} PM={PmError}",
                    am.Error ?? "ok",
                    pm.Error ?? "ok");
                return 0;
            }

            return am.Value.Select(s => s.StudentId)
                .Union(pm.Value.Select(s => s.StudentId))
                .Count();
        }

        private async Task<(string? Summary, bool UsedMock)> TryGetAiCommentaryAsync(
            int assigned,
            int remaining,
            IReadOnlyList<Route> routes)
        {
            if (_ollama is null)
            {
                return (null, false);
            }

            try
            {
                var result = await _ollama.OptimizeRoutesAsync(new RouteOptimizationRequest
                {
                    RouteId = "fleet",
                    StudentsServed = assigned,
                    CurrentPerformance = $"{assigned} slots assigned; {remaining} students still unassigned",
                    TargetMetrics = "Fill active routes within capacity; keep riders on named routes",
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
