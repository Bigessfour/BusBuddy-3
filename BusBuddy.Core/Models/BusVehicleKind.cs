namespace BusBuddy.Core.Models;

/// <summary>
/// District bus body. specs/buses.md: Regular or SpecialNeeds. Activity vans stay Regular here.
/// </summary>
public enum BusVehicleKind
{
    Regular = 0,
    SpecialNeeds = 1
}

public static class BusVehicleKindMapping
{
    public static BusVehicleKind FromFleetType(string? fleetType)
    {
        if (!string.IsNullOrWhiteSpace(fleetType)
            && fleetType.Contains("special", StringComparison.OrdinalIgnoreCase))
        {
            return BusVehicleKind.SpecialNeeds;
        }

        return BusVehicleKind.Regular;
    }
}
