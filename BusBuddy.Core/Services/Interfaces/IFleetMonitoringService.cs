using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Service interface for real-time fleet monitoring and status tracking
    /// Provides GPS tracking, maintenance alerts, and bus status monitoring
    /// </summary>
    public interface IFleetMonitoringService
    {
        /// <summary>
        /// Get comprehensive fleet status including active buses and their locations
        /// </summary>
        /// <returns>Fleet status summary with real-time data</returns>
        Task<FleetStatus> GetFleetStatusAsync();

        /// <summary>
        /// Monitor specific bus location and status
        /// </summary>
        /// <param name="busId">Bus identifier</param>
        /// <returns>Real-time bus monitoring data</returns>
        Task<BusMonitoringData?> MonitorBusLocationAsync(int busId);

        /// <summary>
        /// Get buses with overdue maintenance alerts
        /// </summary>
        /// <returns>List of buses requiring maintenance attention</returns>
        Task<List<Bus>> GetOverdueMaintenanceAlertsAsync();

        /// <summary>
        /// Get all active GPS-enabled buses with their current locations
        /// </summary>
        /// <returns>List of buses with GPS tracking data</returns>
        Task<List<Bus>> GetActiveGpsTrackedBusesAsync();

        /// <summary>
        /// Update bus location coordinates from GPS device
        /// </summary>
        /// <param name="busId">Bus identifier</param>
        /// <param name="latitude">Current latitude</param>
        /// <param name="longitude">Current longitude</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateBusLocationAsync(int busId, decimal latitude, decimal longitude);

        /// <summary>
        /// Get buses by operational status (Active, Maintenance, Out of Service, etc.)
        /// </summary>
        /// <param name="status">Status filter</param>
        /// <returns>Filtered list of buses</returns>
        Task<List<Bus>> GetBusesByOperationalStatusAsync(string status);

        /// <summary>
        /// Get critical alerts for fleet management dashboard
        /// </summary>
        /// <returns>List of critical alerts requiring immediate attention</returns>
        Task<List<FleetAlert>> GetCriticalAlertsAsync();

        /// <summary>
        /// Calculate fleet utilization metrics for reporting
        /// </summary>
        /// <returns>Fleet utilization statistics</returns>
        Task<FleetUtilizationMetrics> CalculateFleetUtilizationAsync();
    }

    /// <summary>
    /// Represents overall fleet status and metrics
    /// </summary>
    public class FleetStatus
    {
        public int TotalBuses { get; set; }
        public int ActiveBuses { get; set; }
        public int BusesInMaintenance { get; set; }
        public int OutOfServiceBuses { get; set; }
        public int GpsEnabledBuses { get; set; }
        public int OverdueMaintenanceBuses { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public List<string> CriticalAlerts { get; set; } = new();
    }

    /// <summary>
    /// Real-time monitoring data for a specific bus
    /// </summary>
    public class BusMonitoringData
    {
        public int BusId { get; set; }
        public string BusNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public decimal? CurrentLatitude { get; set; }
        public decimal? CurrentLongitude { get; set; }
        public bool IsGpsActive { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
        public string? CurrentRoute { get; set; }
        public string? AssignedDriver { get; set; }
        public bool HasMaintenanceAlerts { get; set; }
        public List<string> ActiveAlerts { get; set; } = new();
    }

    /// <summary>
    /// Fleet alert for critical issues requiring attention
    /// </summary>
    public class FleetAlert
    {
        public int AlertId { get; set; }
        public int BusId { get; set; }
        public string BusNumber { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsAcknowledged { get; set; }
    }

    /// <summary>
    /// Fleet utilization metrics for performance analysis
    /// </summary>
    public class FleetUtilizationMetrics
    {
        public decimal UtilizationPercentage { get; set; }
        public int BusesInService { get; set; }
        public int BusesAvailable { get; set; }
        public int AverageMaintenanceDays { get; set; }
        public decimal MaintenanceCostPerBus { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
    }
}
