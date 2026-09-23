using BusBuddy.Core.Configuration;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services.Trips;

/// <summary>
/// Same-day trip-to-bus suggestions via Route Optimization.
/// Does not rewrite PickupTime. Apply only assigns VehicleId on unassigned trips.
/// </summary>
public sealed class TripFleetOptimizer
{
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IRouteOptimizationService? _routeOptimization;
    private readonly IDistrictSettingsAccessor? _district;
    private readonly Func<int, DateTime, DateTime, int, Task<bool>> _busHasConflict;
    private static readonly ILogger Logger = Log.ForContext<TripFleetOptimizer>();

    public TripFleetOptimizer(
        IBusBuddyDbContextFactory contextFactory,
        IRouteOptimizationService? routeOptimization,
        IDistrictSettingsAccessor? district,
        Func<int, DateTime, DateTime, int, Task<bool>> busHasConflict)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _routeOptimization = routeOptimization;
        _district = district;
        _busHasConflict = busHasConflict ?? throw new ArgumentNullException(nameof(busHasConflict));
    }

    public async Task<TripFleetOptimizeResult> SuggestSameDayAsync(
        DateTime tripDate,
        bool applyToUnassigned = false,
        CancellationToken cancellationToken = default)
    {
        if (_routeOptimization is not { IsConfigured: true })
        {
            return new TripFleetOptimizeResult
            {
                Status = "Route Optimization is not configured (GOOGLE_MAPS_API_KEY / routeoptimization.googleapis.com)."
            };
        }

        var day = tripDate.Date;
        using var context = _contextFactory.CreateWriteDbContext();
        var trips = await context.TripEvents
            .AsTracking()
            .Include(t => t.OriginLocation)
            .Include(t => t.DestinationLocation)
            .Where(t => t.TripDate == day)
            .Where(t => t.Status != TripStatus.Cancelled && t.Status != TripStatus.Completed)
            .Where(t => !t.IsMultiAsset)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var eligible = trips
            .Where(t => t.HasValidatedOrigin && t.HasValidatedDestination)
            .ToList();
        if (eligible.Count == 0)
        {
            return new TripFleetOptimizeResult
            {
                Status = "No trips that day have validated origin and destination."
            };
        }

        var unassigned = eligible.Where(TripBoardSelection.NeedsBus).ToList();
        if (unassigned.Count == 0)
        {
            return new TripFleetOptimizeResult
            {
                Succeeded = true,
                Status = "All eligible trips that day already have a bus."
            };
        }

        var buses = await context.Buses
            .AsNoTracking()
            .Where(b => b.Status == "Active")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (buses.Count == 0)
        {
            return new TripFleetOptimizeResult
            {
                Status = "No active buses available for trip fleet optimization."
            };
        }

        var depotLat = (double)unassigned[0].OriginLocation!.Latitude!.Value;
        var depotLon = (double)unassigned[0].OriginLocation!.Longitude!.Value;
        if (DistrictDepot.TryGetCoordinates(_district?.Current, out var dLat, out var dLon))
        {
            depotLat = dLat;
            depotLon = dLon;
        }

        var shipments = unassigned.Select(t => new RouteOptimizationShipment
        {
            Label = $"trip-{t.TripEventId}",
            Pickup = new RouteOptimizationStop
            {
                Label = $"trip-{t.TripEventId}",
                Latitude = (double)t.OriginLocation!.Latitude!.Value,
                Longitude = (double)t.OriginLocation.Longitude!.Value,
            },
            Delivery = new RouteOptimizationStop
            {
                Label = $"trip-{t.TripEventId}-dest",
                Latitude = (double)t.DestinationLocation!.Latitude!.Value,
                Longitude = (double)t.DestinationLocation.Longitude!.Value,
            },
            Load = Math.Max(1, t.PlannedHeadcount ?? Math.Max(1, t.StudentCount)),
        }).ToList();

        var vehicles = buses.Select(b => new RouteOptimizationVehicle
        {
            Label = $"bus-{b.BusId}",
            StartLatitude = depotLat,
            StartLongitude = depotLon,
            EndLatitude = depotLat,
            EndLongitude = depotLon,
            Capacity = Math.Max(1, b.SeatingCapacity),
        }).ToList();

        OptimizeToursProblem problem;
        try
        {
            problem = RouteOptimizationVisitOrder.ForSameDayTrips(shipments, vehicles, day);
        }
        catch (Exception ex)
        {
            return new TripFleetOptimizeResult { Status = ex.Message };
        }

        var result = await _routeOptimization.OptimizeToursAsync(problem, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return new TripFleetOptimizeResult { Status = result.Error ?? "Route Optimization failed." };
        }

        var suggested = ParseTripToBus(result.Visits);
        var applied = 0;
        var skippedConflicts = 0;
        if (applyToUnassigned)
        {
            var reserved = trips
                .Where(t => t.VehicleId.HasValue)
                .Select(t => (BusId: t.VehicleId!.Value, Start: t.LeaveTime, End: t.EndTime))
                .ToList();

            foreach (var trip in unassigned)
            {
                if (!suggested.TryGetValue(trip.TripEventId, out var busId))
                {
                    continue;
                }

                var start = trip.LeaveTime;
                var end = trip.EndTime;
                var overlapsReserved = reserved.Any(r =>
                    r.BusId == busId && r.Start < end && start < r.End);
                if (overlapsReserved ||
                    await _busHasConflict(busId, start, end, trip.TripEventId).ConfigureAwait(false))
                {
                    skippedConflicts++;
                    continue;
                }

                trip.VehicleId = busId;
                trip.UpdatedDate = DateTime.UtcNow;
                context.Entry(trip).Property(t => t.VehicleId).IsModified = true;
                reserved.Add((busId, start, end));
                applied++;
            }

            if (applied > 0)
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        Logger.Information(
            "Trip fleet optimize Date={Date} Unassigned={Unassigned} Suggested={Suggested} Applied={Applied} SkippedConflicts={Skipped}",
            day,
            unassigned.Count,
            suggested.Count,
            applied,
            skippedConflicts);

        var conflictNote = skippedConflicts == 0
            ? string.Empty
            : $" Skipped {skippedConflicts} overlapping bus assignment(s).";
        return new TripFleetOptimizeResult
        {
            Succeeded = true,
            SuggestedCount = suggested.Count,
            AppliedCount = applied,
            Status = applied > 0
                ? $"Assigned {applied} unassigned trip(s) from Route Optimization ({suggested.Count} suggestion(s)). Pickup times were not changed.{conflictNote}"
                : $"Route Optimization suggested {suggested.Count} trip-to-bus assignment(s). Pickup times were not changed.{conflictNote}"
        };
    }

    private static Dictionary<int, int> ParseTripToBus(IEnumerable<OptimizedVisit> visits)
    {
        var suggested = new Dictionary<int, int>();
        foreach (var visit in visits.Where(v => v.IsPickup))
        {
            if (!visit.ShipmentLabel.StartsWith("trip-", StringComparison.Ordinal) ||
                !visit.VehicleLabel.StartsWith("bus-", StringComparison.Ordinal))
            {
                continue;
            }

            if (!int.TryParse(visit.ShipmentLabel["trip-".Length..], out var tripId) ||
                !int.TryParse(visit.VehicleLabel["bus-".Length..], out var busId))
            {
                continue;
            }

            suggested[tripId] = busId;
        }

        return suggested;
    }
}
