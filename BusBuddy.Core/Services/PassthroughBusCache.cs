using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>No memory cache. Used when a service needs IBusService without the fleet cache.</summary>
internal sealed class PassthroughBusCache : IBusCachingService
{
    public static readonly PassthroughBusCache Instance = new();

    public Task<List<Bus>> GetAllBusesAsync(Func<Task<List<Bus>>> factory) => factory();

    public Task<Bus?> GetBusByIdAsync(int busId, Func<int, Task<Bus?>> factory) => factory(busId);

    public void InvalidateBusCache(int busId)
    {
    }

    public void InvalidateAllBusCache()
    {
    }
}
