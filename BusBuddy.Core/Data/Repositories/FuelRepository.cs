using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Fuel-specific repository implementation
/// Extends generic repository with fuel record operations
/// </summary>
public class FuelRepository : Repository<Fuel>, IFuelRepository
{
    public FuelRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async Fuel-Specific Operations

    public async Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId)
    {
        return await Query()
            .Where(f => f.VehicleFueledId == vehicleId)
            .OrderByDescending(f => f.FuelDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(f => f.FuelDate >= startDate.Date && f.FuelDate <= endDate.Date)
            .OrderByDescending(f => f.FuelDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalFuelCostAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(f => f.FuelDate >= startDate.Date && f.FuelDate <= endDate.Date && f.TotalCost.HasValue)
            .SumAsync(f => f.TotalCost!.Value);
    }

    public async Task<decimal> GetAverageFuelEfficiencyAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = Query().Where(f => f.VehicleFueledId == vehicleId && f.Gallons.HasValue && f.Gallons.Value > 0);

        if (startDate.HasValue)
        {
            query = query.Where(f => f.FuelDate >= startDate.Value.Date);
        }

        if (endDate.HasValue)
        {
            query = query.Where(f => f.FuelDate <= endDate.Value.Date);
        }

        var records = await query.ToListAsync();

        if (!records.Any())
        {
            return 0;
        }

        // Calculate efficiency from odometer readings and gallons
        var recordsWithData = records.Where(f => f.Gallons.HasValue && f.Gallons.Value > 0).ToList();
        if (!recordsWithData.Any())
        {
            return 0;
        }

        // Simple average for now - could be enhanced with actual mileage calculation
        return recordsWithData.Average(f => f.VehicleOdometerReading / f.Gallons!.Value);
    }

    public async Task<IEnumerable<Fuel>> GetRecentFuelRecordsAsync(int count = 10)
    {
        return await Query()
            .OrderByDescending(f => f.FuelDate)
            .Take(count)
            .ToListAsync();
    }

    #endregion

    #region Synchronous Fuel-Specific Operations

    public IEnumerable<Fuel> GetFuelRecordsByVehicle(int vehicleId)
    {
        return Query()
            .Where(f => f.VehicleFueledId == vehicleId)
            .OrderByDescending(f => f.FuelDate)
            .ToList();
    }

    public IEnumerable<Fuel> GetFuelRecordsByDateRange(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(f => f.FuelDate >= startDate.Date && f.FuelDate <= endDate.Date)
            .OrderByDescending(f => f.FuelDate)
            .ToList();
    }

    public decimal GetTotalFuelCost(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(f => f.FuelDate >= startDate.Date && f.FuelDate <= endDate.Date && f.TotalCost.HasValue)
            .Sum(f => f.TotalCost!.Value);
    }

    #endregion
}
