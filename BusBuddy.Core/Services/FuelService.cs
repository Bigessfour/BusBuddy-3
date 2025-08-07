using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Services;

/// <summary>
/// Fuel service implementation using Entity Framework
/// </summary>
public class FuelService : IFuelService
{
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public FuelService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<Fuel>> GetAllFuelRecordsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.FuelRecords
            .Include(f => f.Vehicle)
            .OrderByDescending(f => f.FuelDate)
            .ToListAsync();
    }

    public async Task<Fuel?> GetFuelRecordByIdAsync(int id)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.FuelRecords
            .Include(f => f.Vehicle)
            .FirstOrDefaultAsync(f => f.FuelId == id);
    }

    public async Task<Fuel> CreateFuelRecordAsync(Fuel fuel)
    {
        if (fuel == null)
        {

            throw new ArgumentException("Fuel record cannot be null.", nameof(fuel));
        }


        if (fuel.Gallons.HasValue && fuel.Gallons.Value < 0)
        {

            throw new ArgumentException("Gallons cannot be negative.", nameof(fuel));
        }


        using var context = _contextFactory.CreateWriteDbContext();
        context.FuelRecords.Add(fuel);
        await context.SaveChangesAsync();
        return fuel;
    }

    public async Task<Fuel> UpdateFuelRecordAsync(Fuel fuel)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        context.FuelRecords.Update(fuel);
        await context.SaveChangesAsync();
        return fuel;
    }

    public async Task<bool> DeleteFuelRecordAsync(int id)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        var fuel = await context.FuelRecords.FindAsync(id);
        if (fuel == null)
        {

            return false;
        }


        context.FuelRecords.Remove(fuel);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.FuelRecords
            .Include(f => f.Vehicle)
            .Where(f => f.VehicleFueledId == vehicleId)
            .OrderByDescending(f => f.FuelDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.FuelRecords
            .Include(f => f.Vehicle)
            .Where(f => f.FuelDate >= startDate && f.FuelDate <= endDate)
            .OrderByDescending(f => f.FuelDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalFuelCostAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.FuelRecords
            .Where(f => f.VehicleFueledId == vehicleId && f.TotalCost.HasValue);

        if (startDate.HasValue)
        {
            query = query.Where(f => f.FuelDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(f => f.FuelDate <= endDate.Value);
        }


        return await query.SumAsync(f => f.TotalCost ?? 0);
    }

    public async Task<decimal> GetTotalGallonsAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.FuelRecords
            .Where(f => f.VehicleFueledId == vehicleId && f.Gallons.HasValue);

        if (startDate.HasValue)
        {
            query = query.Where(f => f.FuelDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(f => f.FuelDate <= endDate.Value);
        }


        return await query.SumAsync(f => f.Gallons ?? 0);
    }

    public async Task<decimal> GetAverageMPGAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var context = _contextFactory.CreateDbContext();
        // This is a simplified calculation - in a real system you'd track odometer readings
        var vehicle = await context.Buses.FindAsync(vehicleId);
        if (vehicle?.MilesPerGallon.HasValue == true)
        {
            return vehicle.MilesPerGallon.Value;
        }

        // Default estimate if no MPG data available
        return 7.5m; // Average bus MPG
    }
}
