using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static readonly ILogger Logger = Log.ForContext<MapsAddressCache>();
    private readonly ConcurrentDictionary<string, MapsGeocodeResult> _memory = new(StringComparer.Ordinal);
    private readonly string? _filePath;
    private readonly object _fileLock = new();

    public MapsAddressCache()
        : this(null)
    {
    }

    public MapsAddressCache(string? filePath)
    {
        _filePath = filePath;
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
                if (value.Ok)
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
}
