using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>
/// Interface for Fuel management service
/// </summary>
public interface IFuelService
{
    Task<IEnumerable<Fuel>> GetAllFuelRecordsAsync();
    Task<Fuel?> GetFuelRecordByIdAsync(int id);
    Task<Fuel> CreateFuelRecordAsync(Fuel fuel);
    Task<Fuel> UpdateFuelRecordAsync(Fuel fuel);
    Task<bool> DeleteFuelRecordAsync(int id);
    Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId);
    Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalFuelCostAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
    Task<decimal> GetTotalGallonsAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
    Task<decimal> GetAverageMPGAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
}
