using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace BusBuddy.Core.Services
{
    public interface IEnhancedCachingService
    {
        Task<IReadOnlyList<Bus>> GetAllBusesAsync(Func<Task<IEnumerable<Bus>>> fetchFunc);
        Task<IReadOnlyList<Driver>> GetAllDriversAsync(Func<Task<IEnumerable<Driver>>> fetchFunc);
        Task<IReadOnlyList<Route>> GetAllRoutesAsync(Func<Task<IEnumerable<Route>>> fetchFunc);
        Task<IReadOnlyList<Student>> GetAllStudentsAsync(Func<Task<IEnumerable<Student>>> fetchFunc);
        Task<IReadOnlyDictionary<string, int>> GetDashboardMetricsAsync(Func<Task<Dictionary<string, int>>> fetchFunc);
        Task<Dictionary<string, int>> GetCachedDashboardMetricsAsync();
        void SetDashboardMetricsDirectly(Dictionary<string, int> metrics);
        void InvalidateCache(string key);
        void InvalidateAllCaches();
    }

    public class EnhancedCachingService : IEnhancedCachingService
    {
        private readonly IMemoryCache _cache;
        private static readonly ILogger Logger = Log.ForContext<EnhancedCachingService>();
        private static readonly TimeSpan DefaultCacheTime = TimeSpan.FromMinutes(15);
        private static readonly HashSet<string> _allCacheKeys = new HashSet<string>();
        private static readonly object _cacheLock = new object();

        public EnhancedCachingService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Logger.Information("EnhancedCachingService initialized");
        }

        public async Task<IReadOnlyList<Bus>> GetAllBusesAsync(Func<Task<IEnumerable<Bus>>> fetchFunc)
        {
            return await GetOrAddCacheAsync("AllBuses", fetchFunc);
        }

        public async Task<IReadOnlyList<Driver>> GetAllDriversAsync(Func<Task<IEnumerable<Driver>>> fetchFunc)
        {
            return await GetOrAddCacheAsync("AllDrivers", fetchFunc);
        }

        public async Task<IReadOnlyList<Route>> GetAllRoutesAsync(Func<Task<IEnumerable<Route>>> fetchFunc)
        {
            return await GetOrAddCacheAsync("AllRoutes", fetchFunc);
        }

        public async Task<IReadOnlyList<Student>> GetAllStudentsAsync(Func<Task<IEnumerable<Student>>> fetchFunc)
        {
            return await GetOrAddCacheAsync("AllStudents", fetchFunc);
        }

        public Task<Dictionary<string, int>> GetCachedDashboardMetricsAsync()
        {
            const string cacheKey = "DashboardMetrics";
            Debug.WriteLine($"[DEBUG] EnhancedCachingService.GetCachedDashboardMetricsAsync: Checking cache for key '{cacheKey}'");

            if (_cache.TryGetValue(cacheKey, out Dictionary<string, int>? cachedResult) && cachedResult != null)
            {
                Logger.Debug("Cache hit for {Key}, returning cached dashboard metrics", cacheKey);
                Debug.WriteLine($"[DEBUG] EnhancedCachingService: Cache HIT for '{cacheKey}' with {cachedResult.Count} metrics");
                return Task.FromResult(cachedResult);
            }

            Logger.Debug("No cached dashboard metrics found");
            Debug.WriteLine($"[DEBUG] EnhancedCachingService: Cache MISS for '{cacheKey}'");
            return Task.FromResult(new Dictionary<string, int>());
        }

        public async Task<IReadOnlyDictionary<string, int>> GetDashboardMetricsAsync(Func<Task<Dictionary<string, int>>> fetchFunc)
        {
            const string cacheKey = "DashboardMetrics";
            Debug.WriteLine($"[DEBUG] EnhancedCachingService.GetDashboardMetricsAsync: Checking cache for key '{cacheKey}'");

            if (_cache.TryGetValue(cacheKey, out Dictionary<string, int>? cachedResult) && cachedResult != null)
            {
                Logger.Debug("Cache hit for {Key}, returned dashboard metrics", cacheKey);
                Debug.WriteLine($"[DEBUG] EnhancedCachingService: Cache HIT for '{cacheKey}' with {cachedResult.Count} metrics");
                return cachedResult;
            }

            Logger.Information("Cache miss for {Key}, fetching dashboard metrics", cacheKey);
            Debug.WriteLine($"[DEBUG] EnhancedCachingService: Cache MISS for '{cacheKey}', fetching data");

            var stopwatch = Stopwatch.StartNew();
            var result = await fetchFunc();
            stopwatch.Stop();

            Logger.Information("Fetched dashboard metrics in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            Debug.WriteLine($"[DEBUG] EnhancedCachingService: Fetched dashboard metrics in {stopwatch.ElapsedMilliseconds}ms");

            // Add to cache with a shorter expiration for metrics
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, result, cacheEntryOptions);
            Debug.WriteLine($"[DEBUG] EnhancedCachingService: Added {result.Count} metrics to cache with 5min expiration");

            // Track this key
            lock (_cacheLock)
            {
                _allCacheKeys.Add(cacheKey);
            }

            return result;
        }

        public void SetDashboardMetricsDirectly(Dictionary<string, int> metrics)
        {
            Debug.WriteLine($"[DEBUG] EnhancedCachingService.SetDashboardMetricsDirectly: Called with {metrics?.Count ?? 0} metrics");

            if (metrics == null || metrics.Count == 0)
            {
                Logger.Warning("Attempted to cache empty dashboard metrics");
                Debug.WriteLine($"[DEBUG] EnhancedCachingService: Attempted to cache empty metrics, skipping");
                return;
            }

            const string cacheKey = "DashboardMetrics";
            Logger.Information("Directly setting dashboard metrics cache with {Count} values", metrics.Count);
            Debug.WriteLine($"[DEBUG] EnhancedCachingService: Setting cache for key '{cacheKey}' with {metrics.Count} values");

            // Add to cache with a shorter expiration for metrics
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, metrics, cacheEntryOptions);
            Debug.WriteLine($"[DEBUG] EnhancedCachingService: Cache set successfully for key '{cacheKey}'");

            // Track this key
            lock (_cacheLock)
            {
                _allCacheKeys.Add(cacheKey);
            }

            Logger.Debug("Dashboard metrics cached: {Metrics}",
                string.Join(", ", metrics.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        public void InvalidateCache(string key)
        {
            Logger.Information("Invalidating cache for key: {Key}", key);
            _cache.Remove(key);

            lock (_cacheLock)
            {
                _allCacheKeys.Remove(key);
            }
        }

        public void InvalidateAllCaches()
        {
            Logger.Information("Invalidating all caches");

            lock (_cacheLock)
            {
                foreach (var key in _allCacheKeys)
                {
                    _cache.Remove(key);
                }
                _allCacheKeys.Clear();
            }
        }

        private async Task<IReadOnlyList<T>> GetOrAddCacheAsync<T>(string key, Func<Task<IEnumerable<T>>> fetchFunc)
        {
            if (_cache.TryGetValue(key, out IReadOnlyList<T>? cachedResult) && cachedResult != null)
            {
                Logger.Debug("Cache hit for {Key}, returned {Count} items", key, cachedResult.Count);
                return cachedResult;
            }

            Logger.Information("Cache miss for {Key}, fetching data", key);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = (await fetchFunc()).ToList();
                stopwatch.Stop();

                Logger.Information("Fetched {Count} items for {Key} in {ElapsedMs}ms",
                    result.Count, key, stopwatch.ElapsedMilliseconds);

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(DefaultCacheTime);

                _cache.Set(key, result, cacheEntryOptions);

                // Track this key
                lock (_cacheLock)
                {
                    _allCacheKeys.Add(key);
                }

                return result;
            }
            catch (System.Data.SqlTypes.SqlNullValueException ex)
            {
                stopwatch.Stop();
                Logger.Warning(ex, "SQL NULL value error when fetching data for {Key} after {ElapsedMs}ms. Returning empty list to prevent application failure.",
                    key, stopwatch.ElapsedMilliseconds);

                // Return empty list instead of throwing to prevent application failures
                var emptyList = new List<T>();

                // Still cache the empty result with shorter expiration to prevent constant retries
                var shortCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(1));

                _cache.Set(key, emptyList, shortCacheOptions);

                // Track this key
                lock (_cacheLock)
                {
                    _allCacheKeys.Add(key);
                }

                return emptyList;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Error fetching data for {Key} after {ElapsedMs}ms",
                    key, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
