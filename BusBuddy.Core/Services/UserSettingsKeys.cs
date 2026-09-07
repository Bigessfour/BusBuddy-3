namespace BusBuddy.Core.Services;

/// <summary>Keys persisted in %AppData%/BusBuddy/user-settings.json.</summary>
public static class UserSettingsKeys
{
    public const string Theme = "Theme";
    public const string EnableActivityLogging = "EnableActivityLogging";
    public const string ShowDashboardOnStartup = "ShowDashboardOnStartup";
    public const string EnableRouteGeoExport = "EnableRouteGeoExport";

    public const string DistrictDepotName = "DistrictDepotName";
    public const string DistrictDepotAddress = "DistrictDepotAddress";
    public const string DistrictDepotCity = "DistrictDepotCity";
    public const string DistrictDepotState = "DistrictDepotState";
    public const string DistrictDepotZipCode = "DistrictDepotZipCode";
    public const string DistrictDepotLatitude = "DistrictDepotLatitude";
    public const string DistrictDepotLongitude = "DistrictDepotLongitude";
    public const string DistrictBoundingBoxMinLat = "DistrictBoundingBoxMinLat";
    public const string DistrictBoundingBoxMinLon = "DistrictBoundingBoxMinLon";
    public const string DistrictBoundingBoxMaxLat = "DistrictBoundingBoxMaxLat";
    public const string DistrictBoundingBoxMaxLon = "DistrictBoundingBoxMaxLon";
}
