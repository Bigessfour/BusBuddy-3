using System.Text.Json;

namespace BusBuddy.Core.Mapping;

/// <summary>
/// South, west, north, and east edges of a clerk-supplied district polygon.
/// </summary>
public readonly record struct DistrictBoundaryExtent(
    double South,
    double West,
    double North,
    double East);

/// <summary>
/// Reads a GeoJSON polygon and keeps only its extent. The file may describe any district.
/// </summary>
public static class DistrictBoundaryFile
{
    public const int MaxFileCharacters = 2_000_000;

    public static bool TryReadExtent(string? json, out DistrictBoundaryExtent extent, out string error)
    {
        extent = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The boundary file is empty.";
            return false;
        }

        if (json.Length > MaxFileCharacters)
        {
            error = "The boundary file is too large.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "The boundary file is not GeoJSON.";
            return false;
        }

        using (document)
        {
            var acc = new Accumulator();
            Walk(document.RootElement, acc);
            if (acc.Invalid)
            {
                error = "The boundary file has coordinates outside the valid range.";
                return false;
            }

            if (!acc.SawPolygon || acc.Positions < 4)
            {
                error = "The boundary file has no polygon.";
                return false;
            }

            if (acc.North <= acc.South || acc.East <= acc.West)
            {
                error = "The boundary file does not cover an area.";
                return false;
            }

            extent = new DistrictBoundaryExtent(acc.South, acc.West, acc.North, acc.East);
            return true;
        }
    }

    private static void Walk(JsonElement element, Accumulator acc)
    {
        if (acc.Invalid || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("type", out var typeElement) &&
            typeElement.ValueKind == JsonValueKind.String)
        {
            var type = typeElement.GetString();
            if (type is "Polygon" && element.TryGetProperty("coordinates", out var polygon))
            {
                acc.SawPolygon = true;
                AddRings(polygon, acc);
                return;
            }

            if (type is "MultiPolygon" && element.TryGetProperty("coordinates", out var multi))
            {
                acc.SawPolygon = true;
                if (multi.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in multi.EnumerateArray())
                    {
                        AddRings(part, acc);
                    }
                }

                return;
            }

            if (type is "GeometryCollection" &&
                element.TryGetProperty("geometries", out var geometries) &&
                geometries.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in geometries.EnumerateArray())
                {
                    Walk(child, acc);
                }

                return;
            }
        }

        if (element.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array)
        {
            foreach (var feature in features.EnumerateArray())
            {
                Walk(feature, acc);
            }
        }

        if (element.TryGetProperty("geometry", out var featureGeometry) && featureGeometry.ValueKind == JsonValueKind.Object)
        {
            Walk(featureGeometry, acc);
        }
    }

    private static void AddRings(JsonElement rings, Accumulator acc)
    {
        if (acc.Invalid || rings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var ring in rings.EnumerateArray())
        {
            if (ring.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var position in ring.EnumerateArray())
            {
                if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() < 2)
                {
                    continue;
                }

                if (!TryReadNumber(position[0], out var longitude) || !TryReadNumber(position[1], out var latitude) ||
                    !DistrictMapAnchor.IsValidLatitude(latitude) ||
                    !DistrictMapAnchor.IsValidLongitude(longitude))
                {
                    acc.Invalid = true;
                    return;
                }

                acc.Positions++;
                if (latitude < acc.South)
                {
                    acc.South = latitude;
                }

                if (latitude > acc.North)
                {
                    acc.North = latitude;
                }

                if (longitude < acc.West)
                {
                    acc.West = longitude;
                }

                if (longitude > acc.East)
                {
                    acc.East = longitude;
                }
            }
        }
    }

    private static bool TryReadNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        value = default;
        return false;
    }

    private sealed class Accumulator
    {
        public double South { get; set; } = double.MaxValue;

        public double North { get; set; } = double.MinValue;

        public double West { get; set; } = double.MaxValue;

        public double East { get; set; } = double.MinValue;

        public int Positions { get; set; }

        public bool SawPolygon { get; set; }

        public bool Invalid { get; set; }
    }
}
