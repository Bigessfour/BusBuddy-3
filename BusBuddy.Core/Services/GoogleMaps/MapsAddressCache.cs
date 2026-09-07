using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace BusBuddy.Core.Services.GoogleMaps;

/// <summary>In-memory + optional file cache for successful Maps geocode results (FR-004, 30-day TTL).</summary>
public interface IMapsAddressCache
{
    bool TryGet(string cacheKey, out MapsGeocodeResult? result);
    void Set(string cacheKey, MapsGeocodeResult result);
}

public sealed class MapsAddressCache : IMapsAddressCache
{
    private static readonly ILogger Logger = Log.ForContext<MapsAddressCache>();
    public static readonly TimeSpan TimeToLive = TimeSpan.FromDays(30);

    private readonly ConcurrentDictionary<string, MapsGeocodeResult> _memory = new(StringComparer.Ordinal);
    private readonly string? _filePath;
    private readonly object _fileLock = new();
    private readonly Func<DateTimeOffset> _clock;

    public MapsAddressCache()
        : this(null)
    {
    }

    public MapsAddressCache(string? filePath)
        : this(filePath, null)
    {
    }

    public MapsAddressCache(string? filePath, Func<DateTimeOffset>? clock)
    {
        _filePath = filePath;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
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
        if (_memory.TryGetValue(cacheKey, out var hit))
        {
            if (IsExpired(hit))
            {
                _memory.TryRemove(cacheKey, out _);
                result = null;
                return false;
            }

            result = hit;
            Logger.Debug("Maps address cache hit Key={CacheKeyPrefix}…", cacheKey[..Math.Min(8, cacheKey.Length)]);
            return true;
        }

        result = null;
        return false;
    }

    public void Set(string cacheKey, MapsGeocodeResult result)
    {
        if (!result.Ok)
        {
            return;
        }

        if (result.CachedAtUtc == default)
        {
            result.CachedAtUtc = _clock();
        }

        _memory[cacheKey] = result;
        PersistToDisk();
        Logger.Debug("Maps address cache stored Key={CacheKeyPrefix}…", cacheKey[..Math.Min(8, cacheKey.Length)]);
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
                if (value.Ok && !IsExpired(value))
                {
                    _memory[key] = value;
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
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed persisting maps address cache to {Path}", _filePath);
            }
        }
    }

    private bool IsExpired(MapsGeocodeResult result)
    {
        var stamped = result.CachedAtUtc == default ? DateTimeOffset.MinValue : result.CachedAtUtc;
        return _clock() - stamped > TimeToLive;
    }
}
