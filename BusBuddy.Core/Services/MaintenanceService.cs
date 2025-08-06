using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Services;

/// <summary>
/// Maintenance service implementation using Entity Framework
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public MaintenanceService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<Maintenance>> GetAllMaintenanceRecordsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<Maintenance?> GetMaintenanceRecordByIdAsync(int id)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.MaintenanceId == id);
    }

    public async Task<Maintenance> CreateMaintenanceRecordAsync(Maintenance maintenance)
    {
        maintenance.CreatedDate = DateTime.UtcNow;
        using var context = _contextFactory.CreateWriteDbContext();
        context.MaintenanceRecords.Add(maintenance);
        await context.SaveChangesAsync();
        return maintenance;
    }

    public async Task<Maintenance> UpdateMaintenanceRecordAsync(Maintenance maintenance)
    {
        maintenance.UpdatedDate = DateTime.UtcNow;
        using var context = _contextFactory.CreateWriteDbContext();
        context.MaintenanceRecords.Update(maintenance);
        await context.SaveChangesAsync();
        return maintenance;
    }

    public async Task<bool> DeleteMaintenanceRecordAsync(int id)
    {
        using var context = _contextFactory.CreateWriteDbContext();
        var maintenance = await context.MaintenanceRecords.FindAsync(id);
        if (maintenance == null)
        {

            return false;
        }


        context.MaintenanceRecords.Remove(maintenance);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByVehicleAsync(int vehicleId)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .Where(m => m.Date >= startDate && m.Date <= endDate)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByPriorityAsync(string priority)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.MaintenanceRecords
            .Include(m => m.Vehicle)
            .Where(m => m.Priority == priority)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<decimal> GetMaintenanceCostTotalAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
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


        return await query.SumAsync(m => m.RepairCost);
    }
}
