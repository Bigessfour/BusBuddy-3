using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Geographic data for mapping visualization. Route geometry comes from the database.
    /// Street geocoding/routing is Google Maps Platform (spec 007), paused until wired.
    /// </summary>
    public interface IGeoDataService
    {
        /// <summary>
        /// Gets routes with associated geographic data for mapping visualization
        /// </summary>
        Task<List<Route>> GetRoutesWithGeoDataAsync();

        /// <summary>
        /// Gets geographic data for a specific route
        /// </summary>
        Task<Route?> GetRouteGeoDataAsync(int routeId);
    }
}
