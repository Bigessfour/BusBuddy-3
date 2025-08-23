using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Fleet monitoring service for real-time bus tracking and status management
    /// Integrates with Azure SQL Database for data persistence and GPS tracking
    /// Uses Serilog for structured logging and follows BusBuddy service patterns
    /// </summary>
    [DebuggerDisplay("FleetMonitoringService - Cache: {_cacheService != null}")]
    public class FleetMonitoringService : IFleetMonitoringService
    {
        private static readonly ILogger Logger = Log.ForContext<FleetMonitoringService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IBusCachingService _cacheService;
        private readonly IGeoDataService _geoDataService;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public FleetMonitoringService(
            IBusBuddyDbContextFactory contextFactory,
            IBusCachingService cacheService,
            IGeoDataService geoDataService)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _geoDataService = geoDataService ?? throw new ArgumentNullException(nameof(geoDataService));
        }

        /// <summary>
        /// Get comprehensive fleet status including active buses and their locations
        /// </summary>
        public async Task<FleetStatus> GetFleetStatusAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                using (LogContext.PushProperty("Operation", "GetFleetStatus"))
                using (LogContext.PushProperty("OperationName", "FleetMonitoring"))
                {
                    var stopwatch = Stopwatch.StartNew();
                    Logger.Information("Retrieving comprehensive fleet status");

                    using var context = _contextFactory.CreateDbContext();

                    var totalBuses = await context.Buses.CountAsync();
                    var activeBuses = await context.Buses.CountAsync(b => b.Status == "Active");
                    var maintenanceBuses = await context.Buses.CountAsync(b => b.Status == "Maintenance");
                    var outOfServiceBuses = await context.Buses.CountAsync(b => b.Status == "Out of Service");
                    var gpsEnabledBuses = await context.Buses.CountAsync(b => b.GPSTracking);

                    // Calculate overdue maintenance
                    var today = DateTime.Today;
                    var overdueBuses = await context.Buses
                        .Where(b => b.NextMaintenanceDue.HasValue && b.NextMaintenanceDue < today)
                        .CountAsync();

                    // Get critical alerts
                    var criticalAlerts = new List<string>();
                    if (overdueBuses > 0)
                        criticalAlerts.Add($"{overdueBuses} buses have overdue maintenance");

                    var offlineBuses = await context.Buses
                        .Where(b => b.GPSTracking && !b.CurrentLatitude.HasValue)
                        .CountAsync();
                    if (offlineBuses > 0)
                        criticalAlerts.Add($"{offlineBuses} GPS-enabled buses are offline");

                    var fleetStatus = new FleetStatus
                    {
                        TotalBuses = totalBuses,
                        ActiveBuses = activeBuses,
                        BusesInMaintenance = maintenanceBuses,
                        OutOfServiceBuses = outOfServiceBuses,
                        GpsEnabledBuses = gpsEnabledBuses,
                        OverdueMaintenanceBuses = overdueBuses,
                        CriticalAlerts = criticalAlerts,
                        LastUpdated = DateTime.Now
                    };

                    stopwatch.Stop();
                    Logger.Information("Fleet status retrieved successfully in {ElapsedMs}ms - {TotalBuses} total buses, {ActiveBuses} active",
                        stopwatch.ElapsedMilliseconds, totalBuses, activeBuses);

                    return fleetStatus;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retrieving fleet status");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Monitor specific bus location and status
        /// </summary>
        public async Task<BusMonitoringData?> MonitorBusLocationAsync(int busId)
        {
            using (LogContext.PushProperty("BusId", busId))
            using (LogContext.PushProperty("Operation", "MonitorBusLocation"))
            {
                Logger.Information("Monitoring bus location for bus {BusId}", busId);

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    var bus = await context.Buses
                        .Where(b => b.BusId == busId)
                        .FirstOrDefaultAsync();

                    if (bus == null)
                    {
                        Logger.Warning("Bus {BusId} not found for monitoring", busId);
                        return null;
                    }

                    // Get current route assignment (simplified - would need proper route assignment logic)
                    var currentRoute = await context.Routes
                        .Where(r => r.IsActive)
                        .FirstOrDefaultAsync(); // Placeholder - implement proper route assignment

                    // Get assigned driver (simplified - would need proper driver assignment logic)
                    var assignedDriver = await context.Drivers
                        .Where(d => d.IsActive)
                        .FirstOrDefaultAsync(); // Placeholder - implement proper driver assignment

                    // Check for maintenance alerts
                    var hasMaintenanceAlerts = bus.NextMaintenanceDue.HasValue && bus.NextMaintenanceDue < DateTime.Today;
                    var activeAlerts = new List<string>();

                    if (hasMaintenanceAlerts)
                        activeAlerts.Add($"Maintenance overdue since {bus.NextMaintenanceDue:MM/dd/yyyy}");

                    if (bus.GPSTracking && !bus.CurrentLatitude.HasValue)
                        activeAlerts.Add("GPS tracking offline");

                    if (!string.IsNullOrEmpty(bus.InspectionStatus) && bus.InspectionStatus == "Overdue")
                        activeAlerts.Add("Inspection overdue");

                    var monitoringData = new BusMonitoringData
                    {
                        BusId = bus.BusId,
                        BusNumber = bus.BusNumber,
                        Status = bus.Status,
                        CurrentLatitude = bus.CurrentLatitude,
                        CurrentLongitude = bus.CurrentLongitude,
                        IsGpsActive = bus.GPSTracking && bus.CurrentLatitude.HasValue,
                        LastLocationUpdate = bus.UpdatedDate ?? bus.CreatedDate, // Use UpdatedDate/CreatedDate for last update
                        CurrentRoute = currentRoute?.RouteName,
                        AssignedDriver = assignedDriver?.Name,
                        HasMaintenanceAlerts = hasMaintenanceAlerts,
                        ActiveAlerts = activeAlerts
                    };

                    Logger.Information("Bus monitoring data retrieved for {BusNumber} - Status: {Status}, GPS: {IsGpsActive}",
                        bus.BusNumber, bus.Status, monitoringData.IsGpsActive);

                    return monitoringData;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error monitoring bus location for bus {BusId}", busId);
                    throw;
                }
            }
        }

        /// <summary>
        /// Get buses with overdue maintenance alerts
        /// </summary>
        public async Task<List<Bus>> GetOverdueMaintenanceAlertsAsync()
        {
            using (LogContext.PushProperty("Operation", "GetOverdueMaintenanceAlerts"))
            {
                Logger.Information("Retrieving buses with overdue maintenance");

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    var today = DateTime.Today;
                    var overdueBuses = await context.Buses
                        .Where(b => b.NextMaintenanceDue.HasValue && b.NextMaintenanceDue < today)
                        .OrderBy(b => b.NextMaintenanceDue)
                        .ToListAsync();

                    Logger.Information("Found {Count} buses with overdue maintenance", overdueBuses.Count);
                    return overdueBuses;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error retrieving overdue maintenance alerts");
                    throw;
                }
            }
        }

        /// <summary>
        /// Get all active GPS-enabled buses with their current locations
        /// </summary>
        public async Task<List<Bus>> GetActiveGpsTrackedBusesAsync()
        {
            using (LogContext.PushProperty("Operation", "GetActiveGpsTrackedBuses"))
            {
                Logger.Information("Retrieving active GPS-tracked buses");

                try
                {
                    // Use caching service for better performance
                    var allBuses = await _cacheService.GetAllBusesAsync(async () =>
                    {
                        using var context = _contextFactory.CreateDbContext();
                        return await context.Buses.ToListAsync();
                    });

                    var gpsTrackedBuses = allBuses
                        .Where(b => b.GPSTracking && b.Status == "Active")
                        .ToList();

                    Logger.Information("Retrieved {Count} active GPS-tracked buses", gpsTrackedBuses.Count);
                    return gpsTrackedBuses;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error retrieving active GPS-tracked buses");
                    throw;
                }
            }
        }

        /// <summary>
        /// Update bus location coordinates from GPS device
        /// </summary>
        public async Task<bool> UpdateBusLocationAsync(int busId, decimal latitude, decimal longitude)
        {
            using (LogContext.PushProperty("BusId", busId))
            using (LogContext.PushProperty("Latitude", latitude))
            using (LogContext.PushProperty("Longitude", longitude))
            using (LogContext.PushProperty("Operation", "UpdateBusLocation"))
            {
                    Logger.Information("Updating bus location for bus {BusId} to ({Latitude}, {Longitude})",
                        busId, latitude, longitude);

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    var bus = await context.Buses
                        .Where(b => b.BusId == busId)
                        .FirstOrDefaultAsync();

                    if (bus == null)
                    {
                        Logger.Warning("Bus {BusId} not found for location update", busId);
                        return false;
                    }

                    if (!bus.GPSTracking)
                    {
                        Logger.Warning("GPS tracking not enabled for bus {BusNumber} (ID: {BusId})",
                            bus.BusNumber, busId);
                        return false;
                    }

                    bus.CurrentLatitude = latitude;
                    bus.CurrentLongitude = longitude;
                    bus.UpdatedDate = DateTime.Now;

                    await context.SaveChangesAsync();

                    // Invalidate cache to ensure fresh data
                    _cacheService.InvalidateBusCache(busId);

                    Logger.Information("Bus location updated successfully for {BusNumber}", bus.BusNumber);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error updating bus location for bus {BusId}", busId);
                    return false;
                }
            }
        }

        /// <summary>
        /// Get buses by operational status
        /// </summary>
        public async Task<List<Bus>> GetBusesByOperationalStatusAsync(string status)
        {
            using (LogContext.PushProperty("Status", status))
            using (LogContext.PushProperty("Operation", "GetBusesByOperationalStatus"))
            {
                Logger.Information("Retrieving buses with status: {Status}", status);

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    var buses = await context.Buses
                        .Where(b => b.Status == status)
                        .OrderBy(b => b.BusNumber)
                        .ToListAsync();

                    Logger.Information("Retrieved {Count} buses with status {Status}", buses.Count, status);
                    return buses;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error retrieving buses by status {Status}", status);
                    throw;
                }
            }
        }

        /// <summary>
        /// Get critical alerts for fleet management dashboard
        /// </summary>
        public async Task<List<FleetAlert>> GetCriticalAlertsAsync()
        {
            using (LogContext.PushProperty("Operation", "GetCriticalAlerts"))
            {
                Logger.Information("Retrieving critical fleet alerts");

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    var alerts = new List<FleetAlert>();
                    var alertId = 1;

                    // Overdue maintenance alerts
                    var today = DateTime.Today;
                    var overdueBuses = await context.Buses
                        .Where(b => b.NextMaintenanceDue.HasValue && b.NextMaintenanceDue < today)
                        .ToListAsync();

                    foreach (var bus in overdueBuses)
                    {
                        var daysPastDue = (today - bus.NextMaintenanceDue!.Value).Days;
                        alerts.Add(new FleetAlert
                        {
                            AlertId = alertId++,
                            BusId = bus.BusId,
                            BusNumber = bus.BusNumber,
                            AlertType = "Maintenance",
                            Message = $"Maintenance overdue by {daysPastDue} days",
                            Severity = daysPastDue > 30 ? "Critical" : "High",
                            CreatedAt = DateTime.Now
                        });
                    }

                    // GPS offline alerts
                    var offlineGpsBuses = await context.Buses
                        .Where(b => b.GPSTracking && !b.CurrentLatitude.HasValue && b.Status == "Active")
                        .ToListAsync();

                    foreach (var bus in offlineGpsBuses)
                    {
                        alerts.Add(new FleetAlert
                        {
                            AlertId = alertId++,
                            BusId = bus.BusId,
                            BusNumber = bus.BusNumber,
                            AlertType = "GPS",
                            Message = "GPS tracking offline",
                            Severity = "Medium",
                            CreatedAt = DateTime.Now
                        });
                    }

                    // Inspection overdue alerts
                    var inspectionOverdueBuses = await context.Buses
                        .Where(b => b.DateLastInspection.HasValue &&
                                   b.DateLastInspection < today.AddDays(-365)) // Assuming annual inspections
                        .ToListAsync();

                    foreach (var bus in inspectionOverdueBuses)
                    {
                        alerts.Add(new FleetAlert
                        {
                            AlertId = alertId++,
                            BusId = bus.BusId,
                            BusNumber = bus.BusNumber,
                            AlertType = "Inspection",
                            Message = "Annual inspection overdue",
                            Severity = "High",
                            CreatedAt = DateTime.Now
                        });
                    }

                    Logger.Information("Retrieved {Count} critical alerts", alerts.Count);
                    return alerts.OrderByDescending(a => a.Severity).ThenBy(a => a.CreatedAt).ToList();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error retrieving critical alerts");
                    throw;
                }
            }
        }

        /// <summary>
        /// Calculate fleet utilization metrics for reporting
        /// </summary>
        public async Task<FleetUtilizationMetrics> CalculateFleetUtilizationAsync()
        {
            using (LogContext.PushProperty("Operation", "CalculateFleetUtilization"))
            {
                Logger.Information("Calculating fleet utilization metrics");

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    var totalBuses = await context.Buses.CountAsync();
                    var activeBuses = await context.Buses.CountAsync(b => b.Status == "Active");
                    var availableBuses = await context.Buses.CountAsync(b => b.Status == "Active" || b.Status == "Available");
                    var maintenanceBuses = await context.Buses.CountAsync(b => b.Status == "Maintenance");

                    // Calculate utilization percentage
                    var utilizationPercentage = totalBuses > 0 ? (decimal)activeBuses / totalBuses * 100 : 0;

                    // Calculate average maintenance days (simplified - would need maintenance history)
                    var averageMaintenanceDays = 7; // Placeholder - implement proper calculation

                    // Calculate maintenance cost per bus (simplified - would need cost data)
                    var maintenanceCostPerBus = 1500.00m; // Placeholder - implement proper calculation

                    var metrics = new FleetUtilizationMetrics
                    {
                        UtilizationPercentage = Math.Round(utilizationPercentage, 2),
                        BusesInService = activeBuses,
                        BusesAvailable = availableBuses,
                        AverageMaintenanceDays = averageMaintenanceDays,
                        MaintenanceCostPerBus = maintenanceCostPerBus,
                        CalculatedAt = DateTime.Now
                    };

                    Logger.Information("Fleet utilization calculated - {UtilizationPercentage}% utilization, {BusesInService}/{TotalBuses} buses in service",
                        utilizationPercentage, activeBuses, totalBuses);

                    return metrics;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error calculating fleet utilization metrics");
                    throw;
                }
            }
        }
    }
}
