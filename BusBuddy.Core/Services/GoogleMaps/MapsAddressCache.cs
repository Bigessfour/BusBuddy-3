using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusBuddy.Core.Models;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>In-memory + optional file cache for successful Maps geocode results (FR-004).</summary>
public interface IMapsAddressCache
{
    bool TryGet(string cacheKey, out MapsGeocodeResult? result);
    void Set(string cacheKey, MapsGeocodeResult result);
}

public sealed class MapsAddressCache : IMapsAddressCache
{
    /// <summary>Maps Platform allows lat/lng caching for at most 30 days.</summary>
    public static readonly TimeSpan CoordinateTtl = TimeSpan.FromDays(30);

    private static readonly ILogger Logger = Log.ForContext<MapsAddressCache>();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly ConcurrentDictionary<string, MapsGeocodeResult> _memory = new(StringComparer.Ordinal);
    private readonly string? _filePath;
    private readonly object _fileLock = new();
    private readonly TimeProvider _time;

    public MapsAddressCache()
        : this(null, TimeProvider.System)
    {
    }

    public MapsAddressCache(string? filePath)
        : this(filePath, TimeProvider.System)
    {
    }

    public MapsAddressCache(string? filePath, TimeProvider time)
    {
        _filePath = filePath;
        _time = time ?? TimeProvider.System;
        if (!string.IsNullOrWhiteSpace(_filePath))
        {
            LoadFromDisk();
        }
    }

    public static string BuildCacheKey(string? street, string? city, string? state, string? zip)
    {
        static string Norm(string? v) => (v ?? string.Empty).Trim().ToUpperInvariant();
        var raw = string.Join("|", Norm(street), Norm(city), Norm(state), Norm(zip));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    public bool TryGet(string cacheKey, out MapsGeocodeResult? result)
    {
        if (!_memory.TryGetValue(cacheKey, out var hit))
        {
            result = null;
            return false;
        }

        if (IsCoordinateExpired(hit))
        {
            ExpireCoordinates(cacheKey);
            result = null;
            return false;
        }

        if (!hit.Ok || !hit.Latitude.HasValue || !hit.Longitude.HasValue
            || !LocationCoordinate.IsPlotPrecision(hit.Precision))
        {
            ExpireCoordinates(cacheKey);
            result = null;
            return false;
        }

        result = hit;
        Logger.Debug("Maps address cache hit Key={CacheKeyPrefix}…", cacheKey[..Math.Min(8, cacheKey.Length)]);
        return true;
    }

    public void Set(string cacheKey, MapsGeocodeResult result)
    {
        if (!result.Ok || !result.Latitude.HasValue || !result.Longitude.HasValue
            || !LocationCoordinate.IsPlotPrecision(result.Precision))
        {
            return;
        }

        result.CachedAtUtc = _time.GetUtcNow();
        _memory[cacheKey] = Clone(result);
        PersistToDisk();
        Logger.Debug("Maps address cache stored Key={CacheKeyPrefix}…", cacheKey[..Math.Min(8, cacheKey.Length)]);
    }

    private static MapsGeocodeResult Clone(MapsGeocodeResult source) =>
        new()
        {
            Ok = source.Ok,
            FormattedAddress = source.FormattedAddress,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            PlaceId = source.PlaceId,
            Precision = source.Precision,
            ErrorMessage = source.ErrorMessage,
            MappingUnconfigured = source.MappingUnconfigured,
            CachedAtUtc = source.CachedAtUtc
        };

    private bool IsCoordinateExpired(MapsGeocodeResult entry)
    {
        // Legacy disk entries without CachedAtUtc are treated as expired (must re-fetch lat/lng).
        if (!entry.CachedAtUtc.HasValue)
        {
            return true;
        }

        return _time.GetUtcNow() - entry.CachedAtUtc.Value > CoordinateTtl;
    }

    private void ExpireCoordinates(string cacheKey)
    {
        // Policy: delete lat/lng after 30 days. PlaceId lives on Student, not the shared disk cache.
        _memory.TryRemove(cacheKey, out _);
        PersistToDisk();
        Logger.Debug(
            "Maps address cache expired and removed Key={CacheKeyPrefix}…",
            cacheKey[..Math.Min(8, cacheKey.Length)]);
    }

    private void LoadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var entries = JsonSerializer.Deserialize<Dictionary<string, MapsGeocodeResult>>(json);
            if (entries is null)
            {
                return;
            }

            foreach (var (key, value) in entries)
            {
                _memory[key] = value;
            }

            foreach (var key in _memory.Keys.ToList())
            {
                if (_memory.TryGetValue(key, out var entry)
                    && (IsCoordinateExpired(entry) || !LocationCoordinate.IsPlotPrecision(entry.Precision)))
                {
                    ExpireCoordinates(key);
                }
            }

            Logger.Information("Loaded {Count} entries from maps address cache", _memory.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed loading maps address cache from {Path}", _filePath);
        }
    }

    private void PersistToDisk()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var snapshot = _memory.ToDictionary(kv => kv.Key, kv => kv.Value);
                var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed persisting maps address cache to {Path}", _filePath);
            }
        }
    }
}
