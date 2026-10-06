using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.RouteDetermination;

/// <summary>
/// Transfer geometry only. Published clocks live in <see cref="PublishedClockPlanner"/>.
/// </summary>
public static class PickupScheduleCalculator
{
    /// <summary>
    /// Transfer AM uses pickup; PM uses dropoff when present, otherwise pickup.
    /// </summary>
    public static bool TryResolveTransferStop(
        StudentSchoolTransfer transfer,
        RouteTimeSlotKind slot,
        out decimal latitude,
        out decimal longitude,
        out string address)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        var useDropoff = slot == RouteTimeSlotKind.PM &&
                         transfer.DropoffLatitude is not null &&
                         transfer.DropoffLongitude is not null;
        var lat = useDropoff ? transfer.DropoffLatitude : transfer.PickupLatitude;
        var lon = useDropoff ? transfer.DropoffLongitude : transfer.PickupLongitude;
        if (lat is null || lon is null)
        {
            latitude = default;
            longitude = default;
            address = string.Empty;
            return false;
        }

        latitude = lat.Value;
        longitude = lon.Value;
        address = useDropoff
            ? (transfer.DropoffAddress ?? transfer.PickupAddress ?? string.Empty)
            : (transfer.PickupAddress ?? string.Empty);
        return true;
    }
}
