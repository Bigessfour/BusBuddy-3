using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// District bus barn / depot anchor — configured in <see cref="RoutingDistrictSettings"/>,
/// not stored as a school destination in the catalog.
/// </summary>
public static class DistrictDepot
{
    public static bool IsConfigured(RoutingDistrictSettings? settings) =>
        settings is not null &&
        settings.DepotLatitude is double lat &&
        settings.DepotLongitude is double lon &&
        LocationCoordinate.IsValidated(lat, lon);

    public static bool TryGetCoordinates(
        RoutingDistrictSettings? settings,
        out double latitude,
        out double longitude)
    {
        if (!IsConfigured(settings))
        {
            latitude = default;
            longitude = default;
            return false;
        }

        latitude = settings!.DepotLatitude!.Value;
        longitude = settings.DepotLongitude!.Value;
        return true;
    }

    public static string GetDisplayName(RoutingDistrictSettings? settings) =>
        string.IsNullOrWhiteSpace(settings?.DepotName)
            ? "District Bus Barn"
            : settings!.DepotName!.Trim();

    public static string GetDisplayAddress(RoutingDistrictSettings? settings)
    {
        if (settings is null)
        {
            return string.Empty;
        }

        var parts = new[]
        {
            settings.DepotAddress?.Trim(),
            settings.DepotCity?.Trim(),
            string.IsNullOrWhiteSpace(settings.DepotState) ? null : settings.DepotState.Trim(),
            settings.DepotZipCode?.Trim()
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(", ", parts);
    }
}
