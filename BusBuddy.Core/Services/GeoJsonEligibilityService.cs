using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Google Earth/GeoJSON-backed eligibility service.
    /// Determines eligibility when the coordinate is inside the district boundary and outside the town boundary.
    /// </summary>
    public sealed class GeoJsonEligibilityService : IEligibilityService
    {
        private readonly IGeoDataService _geoDataService;
        private readonly string _districtAssetId;
        private readonly string _townAssetId;

        // Cached parsed polygons
        private List<Polygon>? _district;
        private List<Polygon>? _town;
        private readonly object _lock = new();

        public GeoJsonEligibilityService(IGeoDataService geoDataService, string districtAssetId, string townAssetId)
        {
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));
            _districtAssetId = string.IsNullOrWhiteSpace(districtAssetId) ? throw new ArgumentException("Missing district asset id", nameof(districtAssetId)) : districtAssetId;
            _townAssetId = string.IsNullOrWhiteSpace(townAssetId) ? throw new ArgumentException("Missing town asset id", nameof(townAssetId)) : townAssetId;
        }

        public async Task<bool> IsEligibleAsync(double latitude, double longitude)
        {
            await EnsurePolygonsLoadedAsync();

            // Note: GeoJSON uses [lon, lat]
            var insideDistrict = PointInAnyPolygon(longitude, latitude, _district!);
            if (!insideDistrict) return false;

            var insideTown = PointInAnyPolygon(longitude, latitude, _town!);
            return insideDistrict && !insideTown;
        }

        private async Task EnsurePolygonsLoadedAsync()
        {
            if (_district != null && _town != null) return;

            // Double-checked locking
            if (_district == null || _town == null)
            {
                var districtTask = _geoDataService.GetGeoJsonAsync(_districtAssetId);
                var townTask = _geoDataService.GetGeoJsonAsync(_townAssetId);
                await Task.WhenAll(districtTask, townTask);

                var district = ParseGeoJsonToPolygons(districtTask.Result);
                var town = ParseGeoJsonToPolygons(townTask.Result);

                lock (_lock)
                {
                    if (_district == null) _district = district;
                    if (_town == null) _town = town;
                }
            }
        }

        #region GeoJSON parsing

        private static List<Polygon> ParseGeoJsonToPolygons(string geoJson)
        {
            var list = new List<Polygon>();
            using var doc = JsonDocument.Parse(geoJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            if (string.Equals(type, "FeatureCollection", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("features", out var features))
                {
                    foreach (var feat in features.EnumerateArray())
                    {
                        if (!feat.TryGetProperty("geometry", out var geom) || geom.ValueKind == JsonValueKind.Null)
                            continue;
                        ExtractGeometryPolygons(geom, list);
                    }
                }
            }
            else if (string.Equals(type, "Feature", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("geometry", out var geom))
                {
                    ExtractGeometryPolygons(geom, list);
                }
            }
            else
            {
                // Geometry object directly
                ExtractGeometryPolygons(root, list);
            }

            return list;
        }

        private static void ExtractGeometryPolygons(JsonElement geom, List<Polygon> output)
        {
            if (!geom.TryGetProperty("type", out var tProp)) return;
            var gType = tProp.GetString();
            if (string.IsNullOrEmpty(gType)) return;

            if (string.Equals(gType, "Polygon", StringComparison.OrdinalIgnoreCase))
            {
                if (geom.TryGetProperty("coordinates", out var coords))
                {
                    var poly = ParsePolygon(coords);
                    if (poly != null) output.Add(poly);
                }
            }
            else if (string.Equals(gType, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
            {
                if (geom.TryGetProperty("coordinates", out var mcoords))
                {
                    foreach (var polygonCoords in mcoords.EnumerateArray())
                    {
                        var poly = ParsePolygon(polygonCoords);
                        if (poly != null) output.Add(poly);
                    }
                }
            }
        }

        // coords structure: [ [ [lon,lat], ... ] , [hole1...], [hole2...] ]
        private static Polygon? ParsePolygon(JsonElement coords)
        {
            if (coords.ValueKind != JsonValueKind.Array) return null;
            var rings = new List<List<Point>>();
            foreach (var ring in coords.EnumerateArray())
            {
                var pts = new List<Point>();
                foreach (var pt in ring.EnumerateArray())
                {
                    if (pt.ValueKind != JsonValueKind.Array) continue;
                    double lon = pt[0].GetDouble();
                    double lat = pt[1].GetDouble();
                    pts.Add(new Point(lon, lat));
                }
                if (pts.Count >= 3)
                {
                    rings.Add(pts);
                }
            }
            if (rings.Count == 0) return null;
            return new Polygon(rings);
        }

        #endregion

        #region Point in polygon

        private static bool PointInAnyPolygon(double xLon, double yLat, List<Polygon> polygons)
        {
            foreach (var p in polygons)
            {
                if (PointInPolygonWithHoles(xLon, yLat, p))
                    return true;
            }
            return false;
        }

        private static bool PointInPolygonWithHoles(double xLon, double yLat, Polygon polygon)
        {
            // Inside outer and not in any hole
            if (!RayCastingContains(polygon.Rings[0], xLon, yLat))
                return false;

            for (int i = 1; i < polygon.Rings.Count; i++)
            {
                if (RayCastingContains(polygon.Rings[i], xLon, yLat))
                    return false; // inside a hole
            }
            return true;
        }

        // Standard ray-casting algorithm
        private static bool RayCastingContains(List<Point> ring, double x, double y)
        {
            bool inside = false;
            int count = ring.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var xi = ring[i].X; var yi = ring[i].Y;
                var xj = ring[j].X; var yj = ring[j].Y;

                bool intersect = ((yi > y) != (yj > y)) &&
                                 (x < (xj - xi) * (y - yi) / ((yj - yi) == 0 ? 1e-12 : (yj - yi)) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private sealed class Polygon
        {
            public List<List<Point>> Rings { get; }
            public Polygon(List<List<Point>> rings) => Rings = rings;
        }

        private readonly struct Point
        {
            public double X { get; }
            public double Y { get; }
            public Point(double x, double y)
            {
                X = x; Y = y;
            }
        }

        #endregion
    }
}
