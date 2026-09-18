using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Bus-specific repository. Persist type is <see cref="Bus"/> (specs/buses.md).
/// Do not add a parallel Vehicle entity.
/// </summary>
public interface IBusRepository : IRepository<Bus>
{
    Task<IEnumerable<Bus>> GetActiveBusesAsync();
    Task<IEnumerable<Bus>> GetAvailableBusesAsync(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status);
    Task<IEnumerable<Bus>> GetBusesByFleetTypeAsync(string fleetType);
    Task<Bus?> GetByBusNumberAsync(string busNumber);
    Task<Bus?> GetByVinAsync(string vin);
    Task<Bus?> GetByLicenseNumberAsync(string licenseNumber);

    Task<IEnumerable<Bus>> GetBusesDueForInspectionAsync(int withinDays = 30);
    Task<IEnumerable<Bus>> GetBusesWithExpiredInspectionAsync();
    Task<IEnumerable<Bus>> GetBusesDueForMaintenanceAsync();
    Task<IEnumerable<Bus>> GetBusesWithExpiredInsuranceAsync();
    Task<IEnumerable<Bus>> GetBusesWithExpiringInsuranceAsync(int withinDays = 30);

    Task<IEnumerable<Bus>> GetBusesBySeatingCapacityAsync(int minCapacity, int? maxCapacity = null);
    Task<IEnumerable<Bus>> GetBusesWithSpecialEquipmentAsync(string equipment);
    Task<IEnumerable<Bus>> GetBusesWithGpsAsync();

    Task<int> GetTotalCountAsync();
    Task<int> GetActiveCountAsync();
    Task<int> GetAverageAgeAsync();
    Task<decimal> GetTotalFleetValueAsync();
    Task<Dictionary<string, int>> GetCountByStatusAsync();
    Task<Dictionary<string, int>> GetCountByMakeAsync();
    Task<Dictionary<int, int>> GetCountByYearAsync();

    IEnumerable<Bus> GetActiveBuses();
    IEnumerable<Bus> GetAvailableBuses(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    IEnumerable<Bus> GetBusesByStatus(string status);
    Bus? GetByBusNumber(string busNumber);
    IEnumerable<Bus> GetBusesDueForInspection(int withinDays = 30);
}
