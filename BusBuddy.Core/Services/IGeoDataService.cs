using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Geographic data for mapping visualization. Route geometry comes from the database.
    /// Street geocoding/routing is Google Maps Platform (spec 007).
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

        /// <summary>
        /// Schools, catalog stops, and — when <paramref name="routeId"/> is set — that
        /// route's stored path, published stops, and assigned homes. No Google HTTP.
        /// </summary>
        Task<DistrictMapSnapshot> GetDistrictMapAsync(
            int? routeId,
            CancellationToken cancellationToken = default);
    }
}
