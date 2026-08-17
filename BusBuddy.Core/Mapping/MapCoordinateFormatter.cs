using System.Globalization;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// Formats decimal degrees for Syncfusion ImageryLayer markers
/// (official samples use N/S/E/W suffixes on string Latitude/Longitude).
/// </summary>
public static class MapCoordinateFormatter
{
    public static string FormatLatitude(double latitude)
    {
        var abs = Math.Abs(latitude).ToString("F4", CultureInfo.InvariantCulture);
        return latitude >= 0 ? abs + "N" : abs + "S";
    }

    public static string FormatLongitude(double longitude)
    {
        var abs = Math.Abs(longitude).ToString("F4", CultureInfo.InvariantCulture);
        return longitude >= 0 ? abs + "E" : abs + "W";
    }
}
