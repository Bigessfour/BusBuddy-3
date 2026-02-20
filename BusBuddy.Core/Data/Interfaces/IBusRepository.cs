using BusBuddy.Core.Domain;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Bus-specific repository interface
/// Extends generic repository with bus-specific operations
/// </summary>
public interface IBusRepository : IRepository<Bus>
{
    // Bus-specific queries (standardized naming)
    Task<IEnumerable<Bus>> GetActiveBusesAsync();
    Task<IEnumerable<Bus>> GetAvailableBusesAsync(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status);
    Task<IEnumerable<Bus>> GetBusesByFleetTypeAsync(string fleetType);
    Task<Bus?> GetBusByBusNumberAsync(string busNumber);
    Task<Bus?> GetBusByVINAsync(string vin);
    Task<Bus?> GetBusByLicenseNumberAsync(string licenseNumber);

    // Maintenance and inspection queries
    Task<IEnumerable<Bus>> GetBusesDueForInspectionAsync(int withinDays = 30);
    Task<IEnumerable<Bus>> GetBusesWithExpiredInspectionAsync();
    Task<IEnumerable<Bus>> GetBusesDueForMaintenanceAsync();
    Task<IEnumerable<Bus>> GetBusesWithExpiredInsuranceAsync();
    Task<IEnumerable<Bus>> GetBusesWithExpiringInsuranceAsync(int withinDays = 30);

    // Capacity and routing
    Task<IEnumerable<Bus>> GetBusesBySeatingCapacityAsync(int minCapacity, int? maxCapacity = null);
    Task<IEnumerable<Bus>> GetBusesWithSpecialEquipmentAsync(string equipment);
    Task<IEnumerable<Bus>> GetBusesWithGPSAsync();

    // Statistics and reporting
    Task<int> GetTotalBusCountAsync();
    Task<int> GetActiveBusCountAsync();
    Task<int> GetAverageBusAgeAsync();
    Task<decimal> GetTotalFleetValueAsync();
    Task<Dictionary<string, int>> GetBusCountByStatusAsync();
    Task<Dictionary<string, int>> GetBusCountByMakeAsync();
    Task<Dictionary<int, int>> GetBusCountByYearAsync();

    // Synchronous methods for Syncfusion data binding
    IEnumerable<Bus> GetActiveBuses();
    IEnumerable<Bus> GetAvailableBuses(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    IEnumerable<Bus> GetBusesByStatus(string status);
    Bus? GetBusByBusNumber(string busNumber);
    IEnumerable<Bus> GetBusesDueForInspection(int withinDays = 30);
}
