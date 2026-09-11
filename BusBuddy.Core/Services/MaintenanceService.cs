using System.Diagnostics;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Maintenance service implementation using Entity Framework
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private static readonly ILogger Logger = Log.ForContext<MaintenanceService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public MaintenanceService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
        Logger.Debug("MaintenanceService constructed");
    }

    public async Task<IEnumerable<Maintenance>> GetAllMaintenanceRecordsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.Information("Loading all maintenance records");
        using var context = _contextFactory.CreateDbContext();
        var records = await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
        stopwatch.Stop();
        Logger.Information("Loaded {Count} maintenance records in {ElapsedMs}ms", records.Count, stopwatch.ElapsedMilliseconds);
        return records;
    }

    public async Task<Maintenance?> GetMaintenanceRecordByIdAsync(int id)
    {
        Logger.Information("Loading maintenance record {MaintenanceId}", id);
        using var context = _contextFactory.CreateDbContext();
        var record = await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.MaintenanceId == id);
        if (record is null)
        {
            Logger.Warning("Maintenance record {MaintenanceId} not found", id);
        }
        else
        {
            Logger.Information("Loaded maintenance record {MaintenanceId} VehicleId={VehicleId} Status={Status}",
                id, record.VehicleId, record.Status);
        }

        return record;
    }

    public async Task<Maintenance> CreateMaintenanceRecordAsync(Maintenance maintenance)
    {
        MaintenanceRecordValidator.ValidateForPersist(maintenance);
        DetachVehicleGraph(maintenance);
        maintenance.CreatedDate = DateTime.UtcNow;

        Logger.Information(
            "Creating maintenance record VehicleId={VehicleId} Date={Date:yyyy-MM-dd} Work={Work} Status={Status}",
            maintenance.VehicleId, maintenance.Date, maintenance.MaintenanceCompleted, maintenance.Status);

        using var context = _contextFactory.CreateWriteDbContext();
        context.MaintenanceRecords.Add(maintenance);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            Logger.Error(ex, "EF failed creating maintenance record VehicleId={VehicleId}", maintenance.VehicleId);
            throw new InvalidOperationException(FormatDbError(ex), ex);
        }

        Logger.Information("Created maintenance record {MaintenanceId}", maintenance.MaintenanceId);
        await context.Entry(maintenance).Reference(m => m.Vehicle).LoadAsync();
        return maintenance;
    }

    public async Task<Maintenance> UpdateMaintenanceRecordAsync(Maintenance maintenance)
    {
        MaintenanceRecordValidator.ValidateForPersist(maintenance);
        DetachVehicleGraph(maintenance);
        maintenance.UpdatedDate = DateTime.UtcNow;

        Logger.Information(
            "Updating maintenance record {MaintenanceId} VehicleId={VehicleId} Status={Status} Cost={Cost}",
            maintenance.MaintenanceId, maintenance.VehicleId, maintenance.Status, maintenance.RepairCost);

        using var context = _contextFactory.CreateWriteDbContext();
        context.MaintenanceRecords.Update(maintenance);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            Logger.Error(ex, "EF failed updating maintenance record {MaintenanceId}", maintenance.MaintenanceId);
            throw new InvalidOperationException(FormatDbError(ex), ex);
        }

        Logger.Information("Updated maintenance record {MaintenanceId}", maintenance.MaintenanceId);
        await context.Entry(maintenance).Reference(m => m.Vehicle).LoadAsync();
        return maintenance;
    }

    public async Task<bool> DeleteMaintenanceRecordAsync(int id)
    {
        Logger.Information("Deleting maintenance record {MaintenanceId}", id);
        using var context = _contextFactory.CreateWriteDbContext();
        var maintenance = await context.MaintenanceRecords.FindAsync(id);
        if (maintenance == null)
        {
            Logger.Warning("Maintenance record {MaintenanceId} not found for delete", id);
            return false;
        }

        context.MaintenanceRecords.Remove(maintenance);
        await context.SaveChangesAsync();
        Logger.Information("Deleted maintenance record {MaintenanceId}", id);
        return true;
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByVehicleAsync(int vehicleId)
    {
        Logger.Information("Loading maintenance records for vehicle {VehicleId}", vehicleId);
        using var context = _contextFactory.CreateDbContext();
        var records = await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
        Logger.Information("Loaded {Count} maintenance records for vehicle {VehicleId}", records.Count, vehicleId);
        return records;
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        Logger.Information("Loading maintenance records from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}", startDate, endDate);
        using var context = _contextFactory.CreateDbContext();
        var records = await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .Where(m => m.Date >= startDate && m.Date <= endDate)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
        Logger.Information("Loaded {Count} maintenance records in date range", records.Count);
        return records;
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByPriorityAsync(string priority)
    {
        Logger.Information("Loading maintenance records with priority {Priority}", priority);
        using var context = _contextFactory.CreateDbContext();
        var records = await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .Where(m => m.Priority == priority)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
        Logger.Information("Loaded {Count} maintenance records with priority {Priority}", records.Count, priority);
        return records;
    }

    public async Task<decimal> GetMaintenanceCostTotalAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        Logger.Information(
            "Summing maintenance cost for vehicle {VehicleId} Start={Start:yyyy-MM-dd} End={End:yyyy-MM-dd}",
            vehicleId, startDate, endDate);
        using var context = _contextFactory.CreateDbContext();
        var query = context.MaintenanceRecords
            .Where(m => m.VehicleId == vehicleId && m.RepairCost > 0);

        if (startDate.HasValue)
        {
            query = query.Where(m => m.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(m => m.Date <= endDate.Value);
        }

        var total = await query.SumAsync(m => m.RepairCost);
        Logger.Information("Maintenance cost total for vehicle {VehicleId} is {Total}", vehicleId, total);
        return total;
    }

    /// <summary>
    /// Avoids EF re-attaching a Bus graph loaded from a different DbContext on Update/Add.
    /// </summary>
    private static void DetachVehicleGraph(Maintenance maintenance) =>
        maintenance.Vehicle = null!;

    private static string FormatDbError(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? ex.Message;
        if (inner.Contains("FK_Maintenance_Vehicle", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("foreign key", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not save maintenance record — the selected bus is missing or invalid.";
        }

        return DatabaseUserMessage.ForOperation(ex, "save maintenance record");
    }
}
