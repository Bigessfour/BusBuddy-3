using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for geographic data operations and Google Earth integration
    /// </summary>
    public interface IGeoDataService
    {
        /// <summary>
        /// Gets routes with associated geographic data for mapping visualization
        /// </summary>
        /// <returns>Collection of routes with geo data</returns>
        Task<List<Route>> GetRoutesWithGeoDataAsync();

        /// <summary>
        /// Gets GeoJSON data from Google Earth Engine assets
        /// </summary>
        /// <param name="assetId">Asset identifier in Google Earth Engine</param>
        /// <returns>GeoJSON string representation</returns>
        Task<string> GetGeoJsonAsync(string assetId);

        /// <summary>
        /// Gets geographic data for a specific route
        /// </summary>
        /// <param name="routeId">Route identifier</param>
        /// <returns>Route with geographic data populated</returns>
        Task<Route?> GetRouteGeoDataAsync(int routeId);
    }
}
