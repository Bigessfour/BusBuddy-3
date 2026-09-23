using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics; // Added for Stopwatch timing (basic instrumentation)

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Route Service implementation with comprehensive route management capabilities
    /// Implements Result pattern for robust error handling and logging
    /// Route building implementation
    /// Updated: Uses IBusBuddyDbContextFactory for consistent dependency injection
    /// </summary>
    public partial class RouteService : IRouteService
    {
        private static readonly ILogger Logger = Log.ForContext<RouteService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IRouteWaypointRebuildService? _waypointRebuild;
        private readonly AssignFitnessEvaluator? _fitnessEvaluator;
        private readonly IRoutingService? _routingService;
        private readonly IBusService _busService;

        // Minimal op timing helper (basic only; can expand later)
        private static (Guid OpId, Stopwatch Sw) StartOp(string name, object? routeId = null)
        {
            var opId = Guid.NewGuid();
            var sw = Stopwatch.StartNew();
            Logger.Debug("BEGIN {Op} OpId={OpId} RouteId={RouteId}", name, opId, routeId);
            return (opId, sw);
        }
        private static void EndOpOk(string name, Guid opId, Stopwatch sw, object? routeId = null, int? count = null)
        {
            sw.Stop();
            if (count.HasValue)
                Logger.Debug("END   {Op} OpId={OpId} RouteId={RouteId} Count={Count} ElapsedMs={Ms}", name, opId, routeId, count.Value, sw.ElapsedMilliseconds);
            else
                Logger.Debug("END   {Op} OpId={OpId} RouteId={RouteId} ElapsedMs={Ms}", name, opId, routeId, sw.ElapsedMilliseconds);
        }

        public RouteService(IBusBuddyDbContextFactory contextFactory)
            : this(contextFactory, null, null, null)
        {
        }

        public RouteService(
            IBusBuddyDbContextFactory contextFactory,
            IRouteWaypointRebuildService? waypointRebuild)
            : this(contextFactory, waypointRebuild, null, null)
        {
        }

        public RouteService(
            IBusBuddyDbContextFactory contextFactory,
            IRouteWaypointRebuildService? waypointRebuild,
            AssignFitnessEvaluator? fitnessEvaluator)
            : this(contextFactory, waypointRebuild, fitnessEvaluator, null)
        {
        }

        public RouteService(
            IBusBuddyDbContextFactory contextFactory,
            IRouteWaypointRebuildService? waypointRebuild,
            AssignFitnessEvaluator? fitnessEvaluator,
            IRoutingService? routingService,
            IBusService? busService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _waypointRebuild = waypointRebuild;
            _fitnessEvaluator = fitnessEvaluator;
            _routingService = routingService;
            _busService = busService ?? new BusService(contextFactory, PassthroughBusCache.Instance);
        }

        // Context helpers: only dispose when using the concrete runtime factory
        // This prevents disposing shared in-memory contexts used by tests.
        private (BusBuddyDbContext Ctx, bool Dispose) GetReadContext()
        {
            var ctx = _contextFactory.CreateDbContext();
            var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
            return (ctx, shouldDispose);
        }

        private (BusBuddyDbContext Ctx, bool Dispose) GetWriteContext()
        {
            var ctx = _contextFactory.CreateWriteDbContext();
            var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
            return (ctx, shouldDispose);
        }

        /// <summary>
        /// Runs a multi-<c>SaveChanges</c> operation as one retriable unit, rolling back when the operation reports
        /// failure so a rejected step cannot leave partial writes committed. Npgsql is configured with
        /// <c>EnableRetryOnFailure</c>, and a retrying execution strategy rejects a transaction started outside it,
        /// so the transaction must be opened inside the delegate.
        /// https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#execution-strategies-and-transactions
        /// </summary>
        private static Task<Result<T>> InTransactionAsync<T>(
            BusBuddyDbContext context,
            Func<Task<Result<T>>> operation)
        {
            return context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);
                var result = await operation().ConfigureAwait(false);
                if (result.IsFailure)
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    return result;
                }

                await transaction.CommitAsync().ConfigureAwait(false);
                return result;
            });
        }

        /// <summary>
        /// Fills <see cref="Route.StudentCount"/> and <see cref="Route.StopCount"/> from identity keys
        /// (name fallback only when the key is null). One query each — not an N+1 per route.
        /// </summary>
        private static async Task ApplyListMetricsAsync(BusBuddyDbContext context, List<Route> routes)
        {
            if (routes.Count == 0)
            {
                return;
            }

            var routeIds = routes.Select(r => r.RouteId).ToList();
            var stopCounts = await context.RouteStops
                .AsNoTracking()
                .Where(s => routeIds.Contains(s.RouteId))
                .GroupBy(s => s.RouteId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var roster = await context.Students
                .AsNoTracking()
                .Select(s => new { s.AmRouteId, s.PmRouteId, s.AMRoute, s.PMRoute })
                .ToListAsync();

            foreach (var route in routes)
            {
                route.StopCount = stopCounts.GetValueOrDefault(route.RouteId);
                var uniqueName = routes.Count(r =>
                    string.Equals(r.RouteName, route.RouteName, StringComparison.OrdinalIgnoreCase)) == 1;
                route.StudentCount = roster.Count(s =>
                    s.AmRouteId == route.RouteId
                    || s.PmRouteId == route.RouteId
                    || (uniqueName && s.AmRouteId == null && NamesEqual(s.AMRoute, route.RouteName))
                    || (uniqueName && s.PmRouteId == null && NamesEqual(s.PMRoute, route.RouteName)));
            }
        }

        private static bool NamesEqual(string? left, string? right) =>
            !string.IsNullOrWhiteSpace(left)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
