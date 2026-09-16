using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services;

/// <summary>
/// Interface for Fuel management service
/// </summary>
public interface IFuelService
{
    Task<IEnumerable<Fuel>> GetAllFuelRecordsAsync();
    Task<Fuel?> GetFuelRecordByIdAsync(int id);
    Task<Result<Fuel>> CreateFuelRecordAsync(Fuel fuel);
    Task<Result<Fuel>> UpdateFuelRecordAsync(Fuel fuel);
    Task<Result<bool>> DeleteFuelRecordAsync(int id);
    Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId);
    Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalFuelCostAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
    Task<decimal> GetTotalGallonsAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
    Task<decimal> GetAverageMPGAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>Distinct non-empty fuel locations already stored on records (for dropdown catalog merge).</summary>
    Task<IReadOnlyList<string>> GetDistinctFuelLocationsAsync();
}
