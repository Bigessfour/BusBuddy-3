using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Trips;

/// <summary>
/// Planned headcount versus the assigned bus, same block-unless-override shape as route assign.
/// Seating uses PlannedHeadcount. Wheelchair uses a rider count when the caller has one.
/// The office board has no wheelchair PAX column, so confirm passes zero and that half does not fire.
/// Does not read a route roster and does not write Route.AMVehicleId or PMVehicleId.
/// </summary>
public static class TripBoardCapacity
{
    public readonly record struct Check(bool Blocked, string? Message);

    public static Check Evaluate(Bus? bus, int? plannedHeadcount, int plannedWheelchair, bool overrideSeating)
    {
        if (bus is null)
        {
            return new Check(false, null);
        }

        var blocks = new List<string>();
        string? warning = null;
        if (bus.SeatingCapacity <= 0)
        {
            warning = $"Bus {bus.BusNumber} has no seating capacity on file.";
        }
        else if (plannedHeadcount is int headcount && headcount > bus.SeatingCapacity)
        {
            blocks.Add($"Seating capacity {bus.SeatingCapacity} would be exceeded ({headcount} planned)");
        }

        if (plannedWheelchair > bus.WheelchairStations)
        {
            blocks.Add(
                $"Wheelchair capacity {bus.WheelchairStations} stations would be exceeded ({plannedWheelchair} wheelchair riders)");
        }

        if (blocks.Count == 0)
        {
            return new Check(false, warning);
        }

        var detail = string.Join("; ", blocks);
        if (overrideSeating)
        {
            return new Check(false, warning is null ? detail + " (override recorded)" : warning + " " + detail + " (override recorded)");
        }

        return new Check(true, detail);
    }
}
