using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Domain;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Interface for bus caching operations
    /// </summary>
    public interface IBusCachingService
    {
        Task<List<Bus>> GetAllBusesAsync(Func<Task<List<Bus>>> factory);
        Task<Bus?> GetBusByIdAsync(int busId, Func<Task<Bus?>> factory);
        Task InvalidateBusCacheAsync(int? busId = null);
    }
}
