using BusBuddy.Core.Configuration;
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
    /// <summary>Published stop list, clone, and Google drive-path refresh.</summary>
    public partial class RouteService
    {
        #region Route stops, assignments and lookups

        public async Task<Result<IEnumerable<RouteStop>>> GetRouteStopsAsync(int routeId)
        {
            try
            {
                if (routeId <= 0)
                {
                    return Result.FailureResult<IEnumerable<RouteStop>>("Invalid routeId");
                }

                var (context, dispose) = GetReadContext();
                try
                {
                    var stops = await context.RouteStops
                        .Where(rs => rs.RouteId == routeId)
                        .OrderBy(rs => rs.StopOrder)
                        .AsNoTracking()
                        .ToListAsync();
                    return Result.SuccessResult(stops.AsEnumerable());
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving stops for route {RouteId}", routeId);
                return Result.FailureResult<IEnumerable<RouteStop>>($"Error retrieving route stops: {ex.Message}");
            }
        }

        public async Task<Result<bool>> ReorderRouteStopsAsync(int routeId, List<int> orderedStopIds)
        {
            try
            {
                var (opId, sw) = StartOp("ReorderRouteStops", routeId);
                if (routeId <= 0 || orderedStopIds == null || orderedStopIds.Count == 0)
                {
                    return Result.FailureResult<bool>("Invalid input for reordering stops");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        var stops = await context.RouteStops
                            .Where(rs => rs.RouteId == routeId)
                            .OrderBy(rs => rs.StopOrder)
                            .ToListAsync();

                        // Capture original ordering snapshot for diagnostics
                        var originalOrder = stops.Select(s => new { s.RouteStopId, s.StopOrder }).ToList();
                        Logger.Debug("ReorderRouteStops pre-state RouteId={RouteId} OpId={OpId} Original={Original}",
                            routeId,
                            opId,
                            string.Join(",", originalOrder.Select(o => $"{o.RouteStopId}:{o.StopOrder}")));

                        if (stops.Count != orderedStopIds.Count)
                        {
                            return Result.FailureResult<bool>("Ordered stop IDs count does not match existing stop count for route");
                        }

                        // Ensure all IDs exist
                        var stopIdSet = stops.Select(s => s.RouteStopId).ToHashSet();
                        if (orderedStopIds.Any(id => !stopIdSet.Contains(id)))
                        {
                            return Result.FailureResult<bool>("One or more stop IDs not found for route during reorder");
                        }

                        // Assign new order by position in orderedStopIds
                        int order = 1;
                        foreach (var id in orderedStopIds)
                        {
                            var s = stops.First(st => st.RouteStopId == id);
                            if (s.StopOrder != order)
                            {
                                s.StopOrder = order;
                                s.UpdatedDate = DateTime.UtcNow;
                            }
                            order++;
                        }

                        foreach (var s in stops)
                        {
                            context.Entry(s).Property(x => x.StopOrder).IsModified = true;
                        }

                        var routeEntity = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                        if (routeEntity is not null)
                        {
                            var orderedStops = orderedStopIds
                                .Select(id => stops.First(s => s.RouteStopId == id))
                                .ToList();
                            routeEntity.WaypointsJson = RouteWaypointSerializer.FromPairs(
                                orderedStops
                                    .Where(s => RouteStop.IsValidatedCoordinate(s.Latitude, s.Longitude))
                                    .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value)));
                            context.Entry(routeEntity).Property(r => r.WaypointsJson).IsModified = true;
                        }

                        var affected = await context.SaveChangesAsync();

                        // Reload to verify persistence
                        var reloaded = await context.RouteStops
                            .Where(rs => rs.RouteId == routeId)
                            .OrderBy(rs => rs.StopOrder)
                            .Select(rs => new { rs.RouteStopId, rs.StopOrder })
                            .ToListAsync();

                        Logger.Debug("ReorderRouteStops post-state RouteId={RouteId} OpId={OpId} New={New}",
                            routeId,
                            opId,
                            string.Join(",", reloaded.Select(o => $"{o.RouteStopId}:{o.StopOrder}")));

                        var changed = !originalOrder.SequenceEqual(reloaded.Select(r => new { r.RouteStopId, r.StopOrder }));
                        if (!changed)
                        {
                            Logger.Warning("ReorderRouteStops detected no persisted change RouteId={RouteId} OpId={OpId} Affected={Affected}", routeId, opId, affected);
                        }
                        else
                        {
                            Logger.Information("Reordered {Count} stops for route {RouteId} OpId={OpId} Affected={Affected}", stops.Count, routeId, opId, affected);
                        }

                        EndOpOk("ReorderRouteStops", opId, sw, routeId, stops.Count);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        return Result.SuccessResult(changed);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error reordering stops for route {RouteId}", routeId); // basic existing log
                return Result.FailureResult<bool>($"Error reordering route stops: {ex.Message}");
            }
        }
        /// <summary>
        /// Persist updated arrival/departure timing for a set of route stops.
        /// </summary>
        public async Task<Result<bool>> UpdateRouteStopsTimingAsync(int routeId, IEnumerable<RouteStop> stops)
        {
            try
            {
                var (context, dispose) = GetWriteContext();
                try
                {
                    var stopIds = stops.Select(s => s.RouteStopId).ToList();
                    var dbStops = await context.RouteStops
                        .Where(rs => rs.RouteId == routeId && stopIds.Contains(rs.RouteStopId))
                        .ToListAsync();

                    foreach (var updated in stops)
                    {
                        var match = dbStops.FirstOrDefault(s => s.RouteStopId == updated.RouteStopId);
                        if (match != null)
                        {
                            match.ScheduledArrival = updated.ScheduledArrival;
                            match.ScheduledDeparture = updated.ScheduledDeparture;
                            match.EstimatedArrivalTime = updated.EstimatedArrivalTime;
                            match.EstimatedDepartureTime = updated.EstimatedDepartureTime;
                            match.UpdatedDate = DateTime.UtcNow;
                        }
                    }

                    await context.SaveChangesAsync();
                    Logger.Information("Updated timing for {Count} stops on RouteId={RouteId}", dbStops.Count, routeId);
                    return Result.SuccessResult(true);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating route stop timing for RouteId={RouteId}", routeId);
                return Result.FailureResult<bool>($"Error updating stop timing: {ex.Message}");
            }
        }
        public async Task<Result<RouteStop>> AddStopToRouteAsync(int routeId, RouteStop routeStop)
        {
            try
            {
                var (opId, sw) = StartOp("AddStop", routeId);
                if (routeStop is null)
                {
                    return Result.FailureResult<RouteStop>("RouteStop cannot be null");
                }

                if (routeId <= 0)
                {
                    return Result.FailureResult<RouteStop>("Invalid routeId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        // Ensure route exists
                        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId);
                        if (route == null)
                        {
                            return Result.FailureResult<RouteStop>($"Route with ID {routeId} not found");
                        }

                        if (!routeStop.HasValidatedCoordinates)
                        {
                            return Result.FailureResult<RouteStop>(
                                "Stop requires a validated location (geocoded lat/lng). Unvalidated coordinates cannot be published waypoints.");
                        }

                        // Normalize and prepare the RouteStop entity
                        routeStop.RouteId = routeId; // enforce association
                        if (routeStop.StopOrder <= 0)
                        {
                            // Determine next StopOrder
                            var maxOrder = await context.RouteStops
                                .Where(rs => rs.RouteId == routeId)
                                .Select(rs => (int?)rs.StopOrder)
                                .MaxAsync() ?? 0;
                            routeStop.StopOrder = maxOrder + 1;
                        }

                        // CreatedDate is required by the model — set explicitly
                        if (routeStop.CreatedDate == default)
                        {
                            routeStop.CreatedDate = DateTime.UtcNow;
                        }

                        NormalizeStopEstimates(routeStop);

                        // Add and save changes asynchronously (EF Core best practice)
                        await context.RouteStops.AddAsync(routeStop);
                        await context.SaveChangesAsync();

                        Logger.Information("Added stop {StopName} (ID: {RouteStopId}) to route {RouteId} OpId={OpId}",
                            routeStop.StopName, routeStop.RouteStopId, routeId, opId);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        EndOpOk("AddStop", opId, sw, routeId);

                        return Result.SuccessResult(routeStop);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error adding stop to route {RouteId}", routeId);
                var detail = ex.GetBaseException().Message;
                return Result.FailureResult<RouteStop>($"Error adding stop to route: {detail}");
            }
        }

        public async Task<Result<RouteStop>> UpdateRouteStopAsync(int routeId, RouteStop routeStop)
        {
            try
            {
                var (opId, sw) = StartOp("UpdateStop", routeId);
                if (routeStop is null || routeStop.RouteStopId <= 0)
                {
                    return Result.FailureResult<RouteStop>("RouteStop id is required");
                }

                if (routeId <= 0)
                {
                    return Result.FailureResult<RouteStop>("Invalid routeId");
                }

                if (!routeStop.HasValidatedCoordinates)
                {
                    return Result.FailureResult<RouteStop>(
                        "Stop requires a validated location (geocoded lat/lng). Unvalidated coordinates cannot be published waypoints.");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        var existing = await context.RouteStops
                            .FirstOrDefaultAsync(rs => rs.RouteStopId == routeStop.RouteStopId && rs.RouteId == routeId);
                        if (existing is null)
                        {
                            return Result.FailureResult<RouteStop>(
                                $"Stop with ID {routeStop.RouteStopId} not found for route {routeId}");
                        }

                        existing.StopName = routeStop.StopName?.Trim() ?? string.Empty;
                        existing.StopAddress = routeStop.StopAddress?.Trim() ?? string.Empty;
                        existing.Latitude = routeStop.Latitude;
                        existing.Longitude = routeStop.Longitude;
                        NormalizeStopEstimates(existing);

                        await context.SaveChangesAsync();
                        Logger.Information(
                            "Updated stop {StopName} (ID: {RouteStopId}) on route {RouteId} OpId={OpId}",
                            existing.StopName,
                            existing.RouteStopId,
                            routeId,
                            opId);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        EndOpOk("UpdateStop", opId, sw, routeId);
                        return Result.SuccessResult(existing);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating stop on route {RouteId}", routeId);
                return Result.FailureResult<RouteStop>($"Error updating route stop: {ex.GetBaseException().Message}");
            }
        }

        public async Task<Result<bool>> RemoveStopFromRouteAsync(int routeId, int stopId)
        {
            try
            {
                var (opId, sw) = StartOp("RemoveStop", routeId);
                if (routeId <= 0 || stopId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or stopId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        // Ensure route exists
                        var route = await context.Routes.FindAsync(routeId);
                        if (route == null)
                        {
                            Logger.Error("RemoveStop failed — route {RouteId} not found", routeId);
                            return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                        }

                        // Find the stop scoped to this route
                        var stop = await context.RouteStops
                            .FirstOrDefaultAsync(rs => rs.RouteStopId == stopId && rs.RouteId == routeId);

                        if (stop == null)
                        {
                            Logger.Error("RemoveStop failed — stop {StopId} not found for route {RouteId}", stopId, routeId);
                            return Result.FailureResult<bool>($"Stop with ID {stopId} not found for route {routeId}");
                        }

                        context.RouteStops.Remove(stop);
                        await context.SaveChangesAsync();

                        var remaining = await context.RouteStops
                            .AsTracking()
                            .Where(rs => rs.RouteId == routeId)
                            .OrderBy(rs => rs.StopOrder)
                            .ThenBy(rs => rs.RouteStopId)
                            .ToListAsync();
                        var order = 1;
                        foreach (var remainingStop in remaining)
                        {
                            if (remainingStop.StopOrder != order)
                            {
                                remainingStop.StopOrder = order;
                                remainingStop.UpdatedDate = DateTime.UtcNow;
                                context.Entry(remainingStop).Property(s => s.StopOrder).IsModified = true;
                            }

                            order++;
                        }

                        if (remaining.Count > 0)
                        {
                            await context.SaveChangesAsync();
                        }

                        Logger.Information("Removed stop {StopId} from route {RouteId} OpId={OpId}", stopId, routeId, opId);
                        await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                        EndOpOk("RemoveStop", opId, sw, routeId);
                        return Result.SuccessResult(true);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error removing stop {StopId} from route {RouteId}", stopId, routeId);
                return Result.FailureResult<bool>($"Error removing stop from route: {ex.Message}");
            }
        }

        // ReorderRouteStopsAsync implemented earlier (single implementation retained)

        public async Task<Result<Route>> CloneRouteAsync(int sourceRouteId, DateTime newDate, string? newRouteName = null)
        {
            try
            {
                var (opId, sw) = StartOp("CloneRoute", sourceRouteId);
                if (sourceRouteId <= 0)
                {
                    return Result.FailureResult<Route>("Invalid source route id");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    return await InTransactionAsync(context, async () =>
                    {
                        var source = await context.Routes.AsNoTracking()
                            .FirstOrDefaultAsync(r => r.RouteId == sourceRouteId);
                        if (source is null)
                        {
                            return Result.FailureResult<Route>($"Route with ID {sourceRouteId} not found");
                        }

                        var stops = await context.RouteStops.AsNoTracking()
                            .Where(s => s.RouteId == sourceRouteId)
                            .OrderBy(s => s.StopOrder)
                            .ToListAsync();

                        var cloneDate = newDate == default
                            ? DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc)
                            : newDate.Date;
                        var cloneName = string.IsNullOrWhiteSpace(newRouteName)
                            ? $"Copy of {source.RouteName}"
                            : newRouteName.Trim();
                        if (await RouteNameExistsOnDateAsync(context, cloneName, cloneDate).ConfigureAwait(false))
                        {
                            return Result.FailureResult<Route>(
                                $"A route with name '{cloneName}' already exists for {cloneDate:yyyy-MM-dd}");
                        }

                        var clone = new Route
                        {
                            Date = cloneDate,
                            RouteName = cloneName,
                            Description = source.Description,
                            IsActive = false,
                            School = source.School,
                            Session = source.Session,
                            IsSpecialNeedsRoute = source.IsSpecialNeedsRoute,
                            RouteDescription = source.RouteDescription,
                            Boundaries = source.Boundaries,
                            Path = source.Path,
                            WaypointsJson = source.WaypointsJson,
                            Distance = source.Distance,
                            EstimatedDuration = source.EstimatedDuration,
                            AMVehicleId = source.AMVehicleId,
                            PMVehicleId = source.PMVehicleId,
                            AMDriverId = source.AMDriverId,
                            PMDriverId = source.PMDriverId,
                            StopCount = stops.Count,
                            StudentCount = 0
                        };

                        await context.Routes.AddAsync(clone);
                        await context.SaveChangesAsync();

                        foreach (var stop in stops)
                        {
                            await context.RouteStops.AddAsync(new RouteStop
                            {
                                RouteId = clone.RouteId,
                                StopName = stop.StopName,
                                StopAddress = stop.StopAddress,
                                Latitude = stop.Latitude,
                                Longitude = stop.Longitude,
                                StopOrder = stop.StopOrder,
                                ScheduledArrival = stop.ScheduledArrival,
                                ScheduledDeparture = stop.ScheduledDeparture,
                                StopDuration = stop.StopDuration,
                                Status = stop.Status,
                                Notes = stop.Notes,
                                CreatedDate = DateTime.UtcNow,
                                EstimatedArrivalTime = stop.EstimatedArrivalTime,
                                EstimatedDepartureTime = stop.EstimatedDepartureTime
                            });
                        }

                        if (stops.Count > 0)
                        {
                            await context.SaveChangesAsync();
                        }

                        Logger.Information(
                            "Cloned route {SourceId} to {CloneId} ({CloneName}) with {StopCount} stops OpId={OpId}",
                            sourceRouteId, clone.RouteId, clone.RouteName, stops.Count, opId);
                        EndOpOk("CloneRoute", opId, sw, clone.RouteId);
                        return Result.SuccessResult(clone);
                    });
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error cloning route {RouteId}", sourceRouteId);
                return Result.FailureResult<Route>($"Error cloning route: {ex.Message}");
            }
        }

        #endregion

        #region Published path refresh

        /// <summary>
        /// Fills the non-nullable estimate columns so an unset stop does not persist <see cref="DateTime.MinValue"/>.
        /// These are published wall-clock face times at the stop (07:00 means 7am there), not UTC instants.
        /// </summary>
        private static void NormalizeStopEstimates(RouteStop routeStop)
        {
            var arrival = routeStop.ScheduledArrival == default
                ? DefaultStopArrival
                : routeStop.ScheduledArrival;
            var departure = routeStop.ScheduledDeparture == default
                ? arrival + TimeSpan.FromMinutes(RoutingDistrictSettings.DefaultStopDwellMinutes)
                : routeStop.ScheduledDeparture;

            if (routeStop.EstimatedArrivalTime == default)
            {
                routeStop.EstimatedArrivalTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.Add(arrival), DateTimeKind.Utc);
            }

            if (routeStop.EstimatedDepartureTime == default)
            {
                routeStop.EstimatedDepartureTime = DateTime.SpecifyKind(DateTime.UtcNow.Date.Add(departure), DateTimeKind.Utc);
            }
        }

        private static readonly TimeSpan DefaultStopArrival = TimeSpan.FromHours(7);

        public async Task<Result<DrivePathRefreshResult>> RefreshDrivePathAsync(int routeId)
        {
            if (routeId <= 0)
            {
                return Result.FailureResult<DrivePathRefreshResult>("Invalid routeId");
            }

            try
            {
                var (context, dispose) = GetWriteContext();
                try
                {
                    var refresh = await RefreshPublishedPathAsync(context, routeId).ConfigureAwait(false);
                    if (refresh is null)
                    {
                        return Result.FailureResult<DrivePathRefreshResult>($"Route with ID {routeId} not found");
                    }

                    return Result.SuccessResult(refresh);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error refreshing drive path for RouteId={RouteId}", routeId);
                return Result.FailureResult<DrivePathRefreshResult>($"Error refreshing drive path: {ex.Message}");
            }
        }

        /// <summary>
        /// Rebuilds <see cref="Route.WaypointsJson"/> from ordered validated stops, then refreshes
        /// the Google drive path. Fail-open when routing is unavailable.
        /// </summary>
        private async Task<DrivePathRefreshResult?> RefreshPublishedPathAsync(BusBuddyDbContext context, int routeId)
        {
            var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId).ConfigureAwait(false);
            if (route is null)
            {
                return null;
            }

            var coords = await context.RouteStops
                .Where(s => s.RouteId == routeId)
                .OrderBy(s => s.StopOrder)
                .ToListAsync()
                .ConfigureAwait(false);
            var assigned = await context.Students.AsNoTracking()
                .Where(s => s.Active)
                .WhereOnRoute(route)
                .ToListAsync()
                .ConfigureAwait(false);
            var routable = AssignedRouteStops.ForRouting(coords, assigned);
            if (routable.Count != coords.Count)
            {
                Logger.Information(
                    "Drive path omitted unassigned student stops RouteId={RouteId} Kept={Kept} Omitted={Omitted}",
                    routeId,
                    routable.Count,
                    coords.Count - routable.Count);
            }

            var validated = routable
                .Where(s => RouteStop.IsValidatedCoordinate(s.Latitude, s.Longitude))
                .Select(s => ((double)s.Latitude!.Value, (double)s.Longitude!.Value))
                .ToList();
            route.WaypointsJson = RouteWaypointSerializer.FromPairs(validated);
            if (validated.Count < 2)
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
                return DrivePathRefreshResult.Skip(
                    "Need at least two geocoded stops. Open Manage Route and add validated stops first.");
            }

            var refresh = await RouteDrivePathRefresher
                .TryRefreshAsync(_routingService, route)
                .ConfigureAwait(false);
            if (!refresh.Success && !refresh.Skipped)
            {
                Logger.Warning(
                    "Drive path refresh after stop change skipped RouteId={RouteId}: {Message}",
                    routeId,
                    refresh.Message);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
            return refresh;
        }

        #endregion
    }
}
