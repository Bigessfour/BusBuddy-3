using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
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

    public async Task<Result<Fuel>> CreateFuelRecordAsync(Fuel fuel)
    {
        try
        {
            FuelRecordValidator.ValidateForPersist(fuel);
        }
        catch (ArgumentException ex)
        {
            return Result.FailureResult<Fuel>(ex.Message);
        }

        using var context = _contextFactory.CreateWriteDbContext();
        var busCheck = await EnsureBusExistsAsync(context, fuel.VehicleFueledId, "save this fuel record");
        if (busCheck.IsFailure)
        {
            return Result.FailureResult<Fuel>(busCheck.Error);
        }

        context.FuelRecords.Add(fuel);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return ClerkWriteMessages.FailureFromException<Fuel>(
                ex,
                "save this fuel record",
                Logger,
                "EF failed creating fuel record VehicleId={VehicleId}",
                fuel.VehicleFueledId);
        }

        Logger.Information(
            "Created fuel record {FuelId} VehicleId={VehicleId} Gallons={Gallons}",
            fuel.FuelId, fuel.VehicleFueledId, fuel.Gallons);

        await context.Entry(fuel).Reference(f => f.Vehicle).LoadAsync();
        return Result.Success(fuel);
    }

    public async Task<Result<Fuel>> UpdateFuelRecordAsync(Fuel fuel)
    {
        try
        {
            FuelRecordValidator.ValidateForPersist(fuel);
        }
        catch (ArgumentException ex)
        {
            return Result.FailureResult<Fuel>(ex.Message);
        }

        using var context = _contextFactory.CreateWriteDbContext();
        var busCheck = await EnsureBusExistsAsync(context, fuel.VehicleFueledId, "save this fuel record");
        if (busCheck.IsFailure)
        {
            return Result.FailureResult<Fuel>(busCheck.Error);
        }

        context.FuelRecords.Update(fuel);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return ClerkWriteMessages.FailureFromException<Fuel>(
                ex,
                "save this fuel record",
                Logger,
                "EF failed updating fuel record {FuelId}",
                fuel.FuelId);
        }

        await context.Entry(fuel).Reference(f => f.Vehicle).LoadAsync();
        return Result.Success(fuel);
    }

    public async Task<Result<bool>> DeleteFuelRecordAsync(int id)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        var fuel = await context.FuelRecords.FindAsync(id);
        if (fuel == null)
        {
            return Result.FailureResult<bool>(ClerkWriteMessages.NotFound($"Fuel record {id}"));
        }

        context.FuelRecords.Remove(fuel);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return ClerkWriteMessages.FailureFromException<bool>(
                ex,
                "delete this fuel record",
                Logger,
                "EF failed deleting fuel record {FuelId}",
                id);
        }

        return Result.Success(true);
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
        var vehicle = await context.Buses.FindAsync(vehicleId);
        if (vehicle?.MilesPerGallon.HasValue == true)
        {
            return vehicle.MilesPerGallon.Value;
        }

        return 7.5m;
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

    private static async Task<Result> EnsureBusExistsAsync(BusBuddyDbContext context, int busId, string operation)
    {
        var bus = await context.Buses.AsNoTracking().FirstOrDefaultAsync(b => b.BusId == busId);
        if (bus is null)
        {
            return Result.Failure($"Could not {operation} — the selected bus was not found.");
        }

        return Result.Success();
    }
}
