using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.WPF.Models;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service interface for Google Earth integration
    /// Provides geospatial mapping and route visualization capabilities
    /// </summary>
    public interface IGoogleEarthService
    {
        /// <summary>
        /// Initialize the Google Earth service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Get all bus locations
        /// </summary>
        Task<List<BusLocation>> GetBusLocationsAsync();

        /// <summary>
        /// Center the map on the fleet
        /// </summary>
        Task CenterOnFleetAsync();

        /// <summary>
        /// Show all buses on the map
        /// </summary>
        Task ShowAllBusesAsync();

        /// <summary>
        /// Show routes on the map
        /// </summary>
        Task ShowRoutesAsync();

        /// <summary>
        /// Show schools on the map
        /// </summary>
        Task ShowSchoolsAsync();

        /// <summary>
        /// Track a specific bus
        /// </summary>
        Task TrackBusAsync(string busNumber);

        /// <summary>
        /// Zoom in on the map
        /// </summary>
        void ZoomIn();

        /// <summary>
        /// Zoom out on the map
        /// </summary>
        void ZoomOut();

        /// <summary>
        /// Reset the map view
        /// </summary>
        Task ResetViewAsync();

        /// <summary>
        /// Change the map layer
        /// </summary>
        void ChangeMapLayer(string layerType);

        /// <summary>
        /// Start live tracking
        /// </summary>
        void StartLiveTracking();

        /// <summary>
        /// Stop live tracking
        /// </summary>
        void StopLiveTracking();

        /// <summary>
        /// Check if the service is available
        /// </summary>
        Task<bool> IsAvailableAsync();
    }
}
