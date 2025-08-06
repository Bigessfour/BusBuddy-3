using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Maintenance-specific repository implementation
/// Extends generic repository with maintenance record operations
/// </summary>
public class MaintenanceRepository : Repository<Maintenance>, IMaintenanceRepository
{
    public MaintenanceRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async Maintenance-Specific Operations

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByVehicleAsync(int vehicleId)
    {
        return await Query()
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(m => m.Date >= startDate.Date && m.Date <= endDate.Date)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByTypeAsync(string maintenanceType)
    {
        return await Query()
            .Where(m => m.MaintenanceCompleted == maintenanceType)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalMaintenanceCostAsync(DateTime startDate, DateTime endDate)
    {
        return await Query()
            .Where(m => m.Date >= startDate.Date && m.Date <= endDate.Date)
            .SumAsync(m => m.RepairCost);
    }

    public async Task<IEnumerable<Maintenance>> GetUpcomingMaintenanceAsync(int withinDays = 30)
    {
        var futureDate = DateTime.Today.AddDays(withinDays);
        return await Query()
            .Where(m => m.NextServiceDue.HasValue &&
                       m.NextServiceDue.Value >= DateTime.Today &&
                       m.NextServiceDue.Value <= futureDate)
            .OrderBy(m => m.NextServiceDue)
            .ToListAsync();
    }

    public async Task<IEnumerable<Maintenance>> GetOverdueMaintenanceAsync()
    {
        return await Query()
            .Where(m => m.NextServiceDue.HasValue && m.NextServiceDue.Value < DateTime.Today)
            .OrderBy(m => m.NextServiceDue)
            .ToListAsync();
    }

    #endregion

    #region Synchronous Maintenance-Specific Operations

    public IEnumerable<Maintenance> GetMaintenanceRecordsByVehicle(int vehicleId)
    {
        return Query()
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.Date)
            .ToList();
    }

    public IEnumerable<Maintenance> GetMaintenanceRecordsByDateRange(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(m => m.Date >= startDate.Date && m.Date <= endDate.Date)
            .OrderByDescending(m => m.Date)
            .ToList();
    }

    public decimal GetTotalMaintenanceCost(DateTime startDate, DateTime endDate)
    {
        return Query()
            .Where(m => m.Date >= startDate.Date && m.Date <= endDate.Date)
            .Sum(m => m.RepairCost);
    }

    #endregion
}
