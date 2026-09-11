using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Fuel service implementation using Entity Framework
/// </summary>
public class FuelService : IFuelService
{
    private static readonly ILogger Logger = Log.ForContext<FuelService>();
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
        FuelRecordValidator.ValidateForPersist(fuel);

        using var context = _contextFactory.CreateWriteDbContext();
        context.FuelRecords.Add(fuel);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            Logger.Error(ex, "EF failed creating fuel record VehicleId={VehicleId}", fuel.VehicleFueledId);
            throw new InvalidOperationException(FormatDbError(ex), ex);
        }

        Logger.Information(
            "Created fuel record {FuelId} VehicleId={VehicleId} Gallons={Gallons}",
            fuel.FuelId, fuel.VehicleFueledId, fuel.Gallons);

        await context.Entry(fuel).Reference(f => f.Vehicle).LoadAsync();
        return fuel;
    }

    public async Task<Fuel> UpdateFuelRecordAsync(Fuel fuel)
    {
        FuelRecordValidator.ValidateForPersist(fuel);

        using var context = _contextFactory.CreateWriteDbContext();
        context.FuelRecords.Update(fuel);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            Logger.Error(ex, "EF failed updating fuel record {FuelId}", fuel.FuelId);
            throw new InvalidOperationException(FormatDbError(ex), ex);
        }

        await context.Entry(fuel).Reference(f => f.Vehicle).LoadAsync();
        return fuel;
    }

    private static string FormatDbError(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? ex.Message;
        if (inner.Contains("FK_Fuel_Vehicle", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("foreign key", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not save fuel record — the selected bus is missing or invalid.";
        }

        return $"Could not save fuel record: {inner}";
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
        // Inclusive calendar-day range: [start.Date, end.Date]
        var start = startDate.Date;
        var endExclusive = endDate.Date.AddDays(1);

        using var context = _contextFactory.CreateDbContext();
        return await context.FuelRecords
            .Include(f => f.Vehicle)
            .Where(f => f.FuelDate >= start && f.FuelDate < endExclusive)
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

    public async Task<IReadOnlyList<string>> GetDistinctFuelLocationsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.FuelRecords
            .Where(f => f.FuelLocation != null && f.FuelLocation != string.Empty)
            .Select(f => f.FuelLocation)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();
    }
}
