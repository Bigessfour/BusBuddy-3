using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.Trips;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Services;

public sealed partial class TripEventService
{
    public async Task<Result> ConfirmTripAsync(
        int tripEventId,
        bool overrideSeating,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        var trip = await context.TripEvents
            .AsTracking()
            .Include(t => t.DestinationLocation)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.TripEventId == tripEventId, cancellationToken)
            .ConfigureAwait(false);

        var refused = RefuseConfirm(trip);
        if (refused is not null)
        {
            return refused;
        }

        var sessionWarning = await SessionLoadWarningAsync(trip!, cancellationToken).ConfigureAwait(false);
        var capacity = TripBoardCapacity.Evaluate(
            trip.Vehicle,
            trip.PlannedHeadcount,
            plannedWheelchair: 0,
            HonorSeatingOverride(overrideSeating));
        if (capacity.Blocked)
        {
            return Result.Failure(capacity.Message ?? "Bus capacity would be exceeded.");
        }

        if (IsRosterOverflow(sessionWarning) && !HonorSeatingOverride(overrideSeating))
        {
            return Result.Failure(sessionWarning!);
        }

        trip.Status = TripStatus.Confirmed;
        trip.UpdatedDate = DateTime.UtcNow;
        context.Entry(trip).Property(t => t.Status).IsModified = true;
        context.Entry(trip).Property(t => t.UpdatedDate).IsModified = true;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var message = Combine(capacity.Message, sessionWarning);
        return string.IsNullOrEmpty(message) ? Result.Success() : Result.Success(message);
    }

    /// <summary>
    /// Roster overflow for the published slot. Does not write Route.AMVehicleId or PMVehicleId.
    /// </summary>
    private async Task<string?> SessionLoadWarningAsync(TripEvent trip, CancellationToken cancellationToken)
    {
        if (trip.VehicleId is not int busId || trip.RouteId is not int routeId)
        {
            return null;
        }

        var slot = trip.PickupTime is TimeSpan pickup && pickup >= TimeSpan.FromHours(12)
            ? RouteTimeSlot.PM
            : RouteTimeSlot.AM;
        var session = await _busService
            .GetSessionLoadAsync(busId, routeId, slot, cancellationToken)
            .ConfigureAwait(false);
        return session.IsSuccess ? session.Value?.OverflowWarning : null;
    }

    private static bool IsRosterOverflow(string? warning) =>
        warning?.Contains("overflow", StringComparison.OrdinalIgnoreCase) == true;

    private static string? Combine(string? planned, string? session)
    {
        if (string.IsNullOrEmpty(planned))
        {
            return session;
        }

        if (string.IsNullOrEmpty(session))
        {
            return planned;
        }

        return planned + " " + session;
    }

    private static Result? RefuseConfirm(TripEvent? trip)
    {
        if (trip is null)
        {
            return Result.Failure("Trip not found.");
        }

        if (!trip.HasValidatedDestination)
        {
            return Result.Failure("Confirmed requires a validated destination.");
        }

        if (!trip.HasTimes || (!trip.ReturnClockTime.HasValue && !trip.ReturnTime.HasValue))
        {
            return Result.Failure("Confirmed requires pickup and return times.");
        }

        if (!trip.DriverId.HasValue)
        {
            return Result.Failure("Confirmed requires an assigned driver.");
        }

        if (trip.IsMultiAsset)
        {
            return Result.Failure("Multi-asset trips cannot be confirmed as a single bus. Split assets first.");
        }

        if (!trip.VehicleId.HasValue)
        {
            return Result.Failure("Confirmed requires an assigned bus.");
        }

        if (trip.Vehicle is not null && !trip.Vehicle.IsAvailable)
        {
            return Result.Failure($"Bus {trip.Vehicle.BusNumber} is not available (Out of Service).");
        }

        return null;
    }

    private bool HonorSeatingOverride(bool overrideSeating)
    {
        if (!overrideSeating)
        {
            return false;
        }

        return _district?.Current.AllowSeatingOverride ?? true;
    }
}
