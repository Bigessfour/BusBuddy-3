using BusBuddy.Core.Data;
using BusBuddy.Core.Models.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BusBuddy.Core.Services;

public sealed partial class TripEventService
{
    /// <summary>
    /// Clerk columns written by the trip editor and the board import.
    /// Path miles, created audit, and navigations stay off this write.
    /// </summary>
    private static readonly string[] BoardColumns =
    [
        nameof(TripEvent.Type),
        nameof(TripEvent.CustomType),
        nameof(TripEvent.POCName),
        nameof(TripEvent.LeaveTime),
        nameof(TripEvent.ReturnTime),
        nameof(TripEvent.VehicleId),
        nameof(TripEvent.DriverId),
        nameof(TripEvent.RouteId),
        nameof(TripEvent.StudentCount),
        nameof(TripEvent.Destination),
        nameof(TripEvent.TripNotes),
        nameof(TripEvent.Status),
        nameof(TripEvent.ExternalTicketNo),
        nameof(TripEvent.SchoolYear),
        nameof(TripEvent.RequestingSchool),
        nameof(TripEvent.GroupOrActivity),
        nameof(TripEvent.TripDate),
        nameof(TripEvent.PickupTime),
        nameof(TripEvent.ReturnClockTime),
        nameof(TripEvent.ReturnIsNextDay),
        nameof(TripEvent.OriginName),
        nameof(TripEvent.OriginLocationId),
        nameof(TripEvent.DestinationName),
        nameof(TripEvent.DestinationLocationId),
        nameof(TripEvent.PlannedHeadcount),
        nameof(TripEvent.ActualHeadcount),
        nameof(TripEvent.PlannedMiles),
        nameof(TripEvent.LinkedTripId),
        nameof(TripEvent.IsMultiAsset),
        nameof(TripEvent.IsOvernightPending),
        nameof(TripEvent.AssignedBusNumber),
        nameof(TripEvent.UpdatedDate)
    ];

    private static void CopyBoardOntoTracked(BusBuddyDbContext context, TripEvent existing, TripEvent incoming)
    {
        var previousStatus = TripStatus.Normalize(existing.Status);
        var previousBoard = Snapshot(existing);
        var entry = context.Entry(existing);
        var tripEventId = existing.TripEventId;
        var createdDate = existing.CreatedDate;
        var createdBy = existing.CreatedBy;
        var pathMiles = existing.PathMiles;
        var updatedBy = existing.UpdatedBy;

        entry.CurrentValues.SetValues(incoming);
        existing.TripEventId = tripEventId;
        existing.CreatedDate = createdDate;
        existing.CreatedBy = createdBy;
        existing.PathMiles = pathMiles;
        existing.UpdatedBy = updatedBy;
        existing.RouteId = null;
        ApplyLeaveReturn(existing);
        existing.UpdatedDate = DateTime.UtcNow;
        ApplyBoardStatus(existing, previousStatus, previousBoard, isNew: false);
        MarkBoardColumns(entry);
    }

    private static void MarkBoardColumns(EntityEntry<TripEvent> entry)
    {
        var board = new HashSet<string>(BoardColumns, StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (board.Contains(property.Metadata.Name))
            {
                continue;
            }

            property.CurrentValue = property.OriginalValue;
            property.IsModified = false;
        }
    }
}
