using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing buses/vehicles
    /// </summary>
    public interface IBusService
    {
        Task<IEnumerable<Bus>> GetAllBusesAsync();
        Task<Bus?> GetBusByIdAsync(int busId);
        Task<Result<Bus>> AddBusAsync(Bus bus);
        Task<Result<bool>> UpdateBusAsync(Bus bus);
        Task<Result<bool>> DeleteBusAsync(int busId);
        Task<IEnumerable<Bus>> GetActiveBusesAsync();
        Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status);
        Task<IEnumerable<Bus>> GetBusesByTypeAsync(string type);
        Task<IEnumerable<Bus>> SearchBusesAsync(string searchTerm);
    }
}
