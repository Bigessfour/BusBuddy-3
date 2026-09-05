namespace BusBuddy.Core.Mapping;

/// <summary>
/// Fallback map center when no school destination with coordinates exists.
/// Lamar, CO area — matches SchoolDestinationForm defaults for district clerks.
/// </summary>
public static class MapDefaults
{
    public const double FallbackLatitude = 38.0872;
    public const double FallbackLongitude = -102.6208;
    public const int DefaultZoomLevel = 11;
    public const int SchoolZoomLevel = 13;
}
