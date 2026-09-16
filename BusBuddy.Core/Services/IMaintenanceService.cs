using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services;

/// <summary>
/// Interface for Maintenance management service
/// </summary>
public interface IMaintenanceService
{
    Task<IEnumerable<Maintenance>> GetAllMaintenanceRecordsAsync();
    Task<Maintenance?> GetMaintenanceRecordByIdAsync(int id);
    Task<Result<Maintenance>> CreateMaintenanceRecordAsync(Maintenance maintenance);
    Task<Result<Maintenance>> UpdateMaintenanceRecordAsync(Maintenance maintenance);
    Task<Result<bool>> DeleteMaintenanceRecordAsync(int id);
    Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByVehicleAsync(int vehicleId);
    Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByPriorityAsync(string priority);
    Task<decimal> GetMaintenanceCostTotalAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
}
