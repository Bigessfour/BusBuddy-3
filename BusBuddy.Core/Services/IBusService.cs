using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services
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
        Task<IEnumerable<Bus>> GetBusesByKindAsync(BusVehicleKind kind);
        Task<IEnumerable<Bus>> SearchBusesAsync(string searchTerm);

        /// <summary>
        /// Year-default route from Route vehicle FKs. Does not write the pairing.
        /// </summary>
        Task<BusHomeRoute?> GetHomeRouteAsync(int busId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Seated and wheelchair use on a route slot for this bus, plus an overflow warning.
        /// Does not change HomeRouteId, Route.AMVehicleId, or Route.PMVehicleId.
        /// </summary>
        Task<Result<BusSessionLoad>> GetSessionLoadAsync(
            int busId,
            int routeId,
            RouteTimeSlot slot,
            CancellationToken cancellationToken = default);
    }
}
