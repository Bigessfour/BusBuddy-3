using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Services.Interfaces;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Shapefile-based eligibility using NetTopologySuite.
    /// Loads district and town polygons once and performs point-in-polygon checks.
    /// Assumes input coordinates are WGS84 (EPSG:4326); shapefiles should match or include PRJ.
    /// </summary>
    public sealed class ShapefileEligibilityService : IEligibilityService, IDisposable
    {
        private readonly string _districtPath;
        private readonly string _townPath;
        private Geometry? _districtUnion;
        private Geometry? _townUnion;
        private readonly object _lock = new();
        private bool _loaded;
        private bool _disposed;
        private readonly GeometryFactory _geometryFactory = NetTopologySuite.Geometries.GeometryFactory.Default;

        public ShapefileEligibilityService(string districtShpPath, string townShpPath)
        {
            _districtPath = districtShpPath ?? throw new ArgumentNullException(nameof(districtShpPath));
            _townPath = townShpPath ?? throw new ArgumentNullException(nameof(townShpPath));
        }

    public Task<bool> IsEligibleAsync(double latitude, double longitude)
        {
            EnsureLoaded();
            if (_districtUnion is null)
            {
                return Task.FromResult(false);
            }

            var point = _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
            var inDistrict = _districtUnion.Contains(point) || _districtUnion.Covers(point);
            var inTown = _townUnion is not null && (_townUnion.Contains(point) || _townUnion.Covers(point));
            return Task.FromResult(inDistrict && !inTown);
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            lock (_lock)
            {
                if (_loaded)
                {
                    return;
                }
                _districtUnion = LoadUnionPolygon(_districtPath);
                _townUnion = File.Exists(_townPath) ? LoadUnionPolygon(_townPath) : null;
                _loaded = true;
            }
        }

        private static Geometry? LoadUnionPolygon(string shpPath)
        {
            if (!File.Exists(shpPath))
            {
                return null;
            }
            var reader = new ShapefileReader(shpPath);
            var geometries = reader.ReadAll();
            Geometry? union = null;
            foreach (var geom in geometries)
            {
                union = union is null ? geom : union.Union(geom);
            }
            return union;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _districtUnion = null;
            _townUnion = null;
            _disposed = true;
        }
    }
}
