using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Services.Interfaces;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Shapefile-based eligibility using NetTopologySuite.
    /// Loads district and town polygons once and performs point-in-polygon checks.
    /// Assumes input coordinates are WGS84 (EPSG:4326); shapefiles should match or include PRJ.
    /// </summary>
    public sealed class ShapefileEligibilityService : IEligibilityService, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<ShapefileEligibilityService>();
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
            Logger.Information(
                "ShapefileEligibilityService constructed DistrictExists={DistrictExists} TownExists={TownExists}",
                File.Exists(_districtPath), File.Exists(_townPath));
        }

    public Task<bool> IsEligibleAsync(double latitude, double longitude)
        {
            EnsureLoaded();
            if (_districtUnion is null)
            {
                Logger.Warning("Eligibility check failed — district shapefile not loaded ({Lat}, {Lon})", latitude, longitude);
                return Task.FromResult(false);
            }

            var point = _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));
            var inDistrict = _districtUnion.Contains(point) || _districtUnion.Covers(point);
            var inTown = _townUnion is not null && (_townUnion.Contains(point) || _townUnion.Covers(point));
            var eligible = inDistrict && !inTown;
            Logger.Debug("Eligibility ({Lat}, {Lon}) InDistrict={InDistrict} InTown={InTown} Eligible={Eligible}",
                latitude, longitude, inDistrict, inTown, eligible);
            return Task.FromResult(eligible);
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
                Logger.Information(
                    "Eligibility shapefiles loaded DistrictLoaded={DistrictLoaded} TownLoaded={TownLoaded}",
                    _districtUnion is not null, _townUnion is not null);
            }
        }

        private static Geometry? LoadUnionPolygon(string shpPath)
        {
            if (!File.Exists(shpPath))
            {
                Logger.Warning("Shapefile not found at {Path}", shpPath);
                return null;
            }
            var reader = new ShapefileReader(shpPath);
            var geometries = reader.ReadAll();
            Geometry? union = null;
            var featureCount = 0;
            foreach (var geom in geometries)
            {
                featureCount++;
                union = union is null ? geom : union.Union(geom);
            }
            Logger.Information("Loaded shapefile {Path} Features={Count} HasUnion={HasUnion}", shpPath, featureCount, union is not null);
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
