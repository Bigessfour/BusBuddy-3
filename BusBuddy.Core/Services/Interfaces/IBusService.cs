using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing buses/vehicles
    /// </summary>
    public interface IBusService
    {
        Task<IEnumerable<Bus>> GetAllBusesAsync();
        Task<Bus?> GetBusByIdAsync(int busId);
        Task<Bus> AddBusAsync(Bus bus);
        Task<bool> UpdateBusAsync(Bus bus);
        Task<bool> DeleteBusAsync(int busId);
        Task<IEnumerable<Bus>> GetActiveBusesAsync();
        Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status);
        Task<IEnumerable<Bus>> GetBusesByTypeAsync(string type);
        Task<IEnumerable<Bus>> SearchBusesAsync(string searchTerm);

        // Route-related methods
        Task<List<Route>> GetAllRouteEntitiesAsync();
    }
}
