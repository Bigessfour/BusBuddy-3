using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Bus-specific repository interface
/// Extends generic repository with bus/vehicle-specific operations
/// </summary>
public interface IBusRepository : IRepository<Bus>
{
    // Vehicle-specific queries
    Task<IEnumerable<Bus>> GetActiveVehiclesAsync();
    Task<IEnumerable<Bus>> GetAvailableVehiclesAsync(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    Task<IEnumerable<Bus>> GetVehiclesByStatusAsync(string status);
    Task<IEnumerable<Bus>> GetVehiclesByFleetTypeAsync(string fleetType);
    Task<Bus?> GetVehicleByBusNumberAsync(string busNumber);
    Task<Bus?> GetVehicleByVINAsync(string vin);
    Task<Bus?> GetVehicleByLicenseNumberAsync(string licenseNumber);

    // Maintenance and inspection queries
    Task<IEnumerable<Bus>> GetVehiclesDueForInspectionAsync(int withinDays = 30);
    Task<IEnumerable<Bus>> GetVehiclesWithExpiredInspectionAsync();
    Task<IEnumerable<Bus>> GetVehiclesDueForMaintenanceAsync();
    Task<IEnumerable<Bus>> GetVehiclesWithExpiredInsuranceAsync();
    Task<IEnumerable<Bus>> GetVehiclesWithExpiringInsuranceAsync(int withinDays = 30);

    // Capacity and routing
    Task<IEnumerable<Bus>> GetVehiclesBySeatingCapacityAsync(int minCapacity, int? maxCapacity = null);
    Task<IEnumerable<Bus>> GetVehiclesWithSpecialEquipmentAsync(string equipment);
    Task<IEnumerable<Bus>> GetVehiclesWithGPSAsync();

    // Statistics and reporting
    Task<int> GetTotalVehicleCountAsync();
    Task<int> GetActiveVehicleCountAsync();
    Task<int> GetAverageVehicleAgeAsync();
    Task<decimal> GetTotalFleetValueAsync();
    Task<Dictionary<string, int>> GetVehicleCountByStatusAsync();
    Task<Dictionary<string, int>> GetVehicleCountByMakeAsync();
    Task<Dictionary<int, int>> GetVehicleCountByYearAsync();

    // Synchronous methods for Syncfusion data binding
    IEnumerable<Bus> GetActiveVehicles();
    IEnumerable<Bus> GetAvailableVehicles(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    IEnumerable<Bus> GetVehiclesByStatus(string status);
    Bus? GetVehicleByBusNumber(string busNumber);
    IEnumerable<Bus> GetVehiclesDueForInspection(int withinDays = 30);
}
