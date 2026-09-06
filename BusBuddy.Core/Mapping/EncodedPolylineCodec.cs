namespace BusBuddy.Core.Mapping;

/// <summary>Google encoded polyline codec (precision 1e-5).</summary>
public static class EncodedPolylineCodec
{
    public static IReadOnlyList<(double Latitude, double Longitude)> Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return Array.Empty<(double, double)>();
        }

        var points = new List<(double, double)>();
        var index = 0;
        var lat = 0;
        var lng = 0;

        while (index < encoded.Length)
        {
            if (!TryDecodeNext(encoded, ref index, out var dLat) ||
                !TryDecodeNext(encoded, ref index, out var dLng))
            {
                break;
            }

            lat += dLat;
            lng += dLng;
            points.Add((lat / 1e5, lng / 1e5));
        }

        return points;
    }

    private static bool TryDecodeNext(string encoded, ref int index, out int value)
    {
        value = 0;
        var result = 0;
        var shift = 0;

        while (index < encoded.Length)
        {
            var b = encoded[index++] - 63;
            result |= (b & 0x1f) << shift;
            shift += 5;
            if (b < 0x20)
            {
                value = (result & 1) != 0 ? ~(result >> 1) : result >> 1;
                return true;
            }
        }

        return false;
    }
}
