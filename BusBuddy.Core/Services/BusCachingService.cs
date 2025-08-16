using BusBuddy.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service to cache bus entity data and reduce redundant database queries
    /// </summary>
    public class BusCachingService : IBusCachingService, IDisposable
    {
        private readonly IMemoryCache _cache;
        private static readonly ILogger Logger = Log.ForContext<BusCachingService>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;

        // Cache keys
        private const string ALL_BUSES_KEY = "AllBuses";
        private const string BUS_ENTITY_PREFIX = "BusEntity_";

        // Cache durations
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _extendedCacheDuration = TimeSpan.FromMinutes(15);

        public BusCachingService(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Get all buses from cache or using the provided factory method
        /// </summary>
        public async Task<List<Bus>> GetAllBusesAsync(Func<Task<List<Bus>>> factory)
        {
            if (_cache.TryGetValue(ALL_BUSES_KEY, out List<Bus>? buses))
            {
                Logger.Information("Retrieved all buses from cache");
                return buses ?? new List<Bus>();
            }

            try
            {
                await _semaphore.WaitAsync();

                // Double-check after acquiring semaphore
                if (_cache.TryGetValue(ALL_BUSES_KEY, out buses) && buses != null)
                {
                    Logger.Information("Retrieved all buses from cache after semaphore");
                    return buses;
                }

                try
                {
                    // Cache miss - get from database
                    buses = await factory();

                    // Ensure no null collections exist
                    if (buses == null) buses = new List<Bus>();

                    // Validate the data before caching
                    foreach (var bus in buses)
                    {
                        // Ensure string properties are not null
                        if (bus.BusNumber == null) bus.BusNumber = string.Empty;
                        if (bus.Make == null) bus.Make = string.Empty;
                        if (bus.Model == null) bus.Model = string.Empty;
                        if (bus.VINNumber == null) bus.VINNumber = string.Empty;
                        if (bus.LicenseNumber == null) bus.LicenseNumber = string.Empty;
                        if (bus.Status == null) bus.Status = "Active";
                    }

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(_cacheDuration)
                        .SetPriority(CacheItemPriority.High);

                    _cache.Set(ALL_BUSES_KEY, buses, cacheOptions);
                    Logger.Information("Added all buses to cache");
                }
                catch (System.Data.SqlTypes.SqlNullValueException ex)
                {
                    Logger.Warning(ex, "SQL NULL value error when retrieving buses. Returning empty list to avoid application failure.");
                    buses = new List<Bus>();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error retrieving buses for caching");
                    throw;
                }

                return buses ?? new List<Bus>();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Get bus by ID from cache or using the provided factory method
        /// </summary>
        public async Task<Bus?> GetBusByIdAsync(int busId, Func<int, Task<Bus?>> factory)
        {
            var cacheKey = $"{BUS_ENTITY_PREFIX}{busId}";

            if (_cache.TryGetValue(cacheKey, out Bus? bus))
            {
                Logger.Information("Retrieved bus ID {BusId} from cache", busId);
                return bus;
            }

            try
            {
                await _semaphore.WaitAsync();

                // Double-check after acquiring semaphore
                if (_cache.TryGetValue(cacheKey, out bus) && bus != null)
                {
                    Logger.Information("Retrieved bus ID {BusId} from cache after semaphore", busId);
                    return bus;
                }

                // Cache miss - get from database
                bus = await factory(busId);

                if (bus != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(_extendedCacheDuration)
                        .SetPriority(CacheItemPriority.Normal);

                    _cache.Set(cacheKey, bus, cacheOptions);
                    Logger.Information("Added bus ID {BusId} to cache", busId);
                }

                return bus;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Invalidate the cache for a specific bus and the all buses collection
        /// </summary>
        public void InvalidateBusCache(int busId)
        {
            Logger.Information("Invalidating cache for bus ID {BusId}", busId);
            _cache.Remove($"{BUS_ENTITY_PREFIX}{busId}");
            _cache.Remove(ALL_BUSES_KEY);
        }

        /// <summary>
        /// Invalidate the entire bus cache
        /// </summary>
        public void InvalidateAllBusCache()
        {
            Logger.Information("Invalidating all bus caches");
            _cache.Remove(ALL_BUSES_KEY);
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }

    public interface IBusCachingService
    {
        Task<List<Bus>> GetAllBusesAsync(Func<Task<List<Bus>>> factory);
        Task<Bus?> GetBusByIdAsync(int busId, Func<int, Task<Bus?>> factory);
        void InvalidateBusCache(int busId);
        void InvalidateAllBusCache();
    }
}
