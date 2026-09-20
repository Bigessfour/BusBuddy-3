using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories
{
    public enum BusStatus
    {
        Active,
        Inactive,
        Maintenance,
        Retired
    }

    /// <summary>
    /// Bus-specific repository. Persist type is <see cref="Bus"/>.
    /// </summary>
    public class BusRepository : Repository<Bus>, IBusRepository
    {
        public BusRepository(BusBuddyDbContext context, IUserContextService userContextService)
            : base(context, userContextService)
        {
        }

        public async Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status)
        {
            return await Query()
                .Where(b => b.Status == status)
                .OrderBy(b => b.BusNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetActiveBusesAsync()
        {
            return await Query()
                .Where(b => b.Status == "Active")
                .OrderBy(b => b.BusNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetAvailableBusesAsync(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null)
        {
            var activeBuses = await GetActiveBusesAsync();

            if (!startTime.HasValue || !endTime.HasValue)
            {
                return activeBuses;
            }

            var conflictingBusIds = await Context.Activities
                .Where(a => a.Date.Date == availabilityDate.Date &&
                           ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                            (a.EventTime > startTime && a.EventTime <= endTime) ||
                            (a.LeaveTime <= startTime && a.EventTime >= endTime)))
                .Select(a => a.AssignedVehicleId)
                .ToListAsync();

            return activeBuses.Where(b => !conflictingBusIds.Contains(b.BusId));
        }

        public async Task<IEnumerable<Bus>> GetBusesByStatusAsync(BusStatus status)
        {
            return await Query()
                .Where(b => b.Status == status.ToString())
                .OrderBy(b => b.BusNumber)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<Bus>> GetBusesByFleetTypeAsync(string fleetType)
        {
            return await Query()
                .Where(b => b.FleetType == fleetType)
                .OrderBy(b => b.BusNumber)
                .ToListAsync();
        }

        public async Task<Bus?> GetByBusNumberAsync(string busNumber)
        {
            return await Query()
                .FirstOrDefaultAsync(b => b.BusNumber == busNumber);
        }

        public async Task<Bus?> GetByVinAsync(string vin)
        {
            return await Query()
                .FirstOrDefaultAsync(b => b.VINNumber == vin);
        }

        public async Task<Bus?> GetByLicenseNumberAsync(string licenseNumber)
        {
            return await Query()
                .FirstOrDefaultAsync(b => b.LicenseNumber == licenseNumber);
        }

        public async Task<IEnumerable<Bus>> GetBusesDueForInspectionAsync(int withinDays = 30)
        {
            var cutoffDate = DateTime.Today.AddDays(-365 + withinDays);
            return await Query()
                .Where(b => !b.DateLastInspection.HasValue || b.DateLastInspection <= cutoffDate)
                .OrderBy(b => b.DateLastInspection ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithExpiredInspectionAsync()
        {
            var oneYearAgo = DateTime.Today.AddYears(-1);
            return await Query()
                .Where(b => !b.DateLastInspection.HasValue || b.DateLastInspection <= oneYearAgo)
                .OrderBy(b => b.DateLastInspection ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesDueForMaintenanceAsync()
        {
            return await Query()
                .Where(b => b.NextMaintenanceDue.HasValue && b.NextMaintenanceDue <= DateTime.Today.AddDays(30))
                .OrderBy(b => b.NextMaintenanceDue)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithExpiredInsuranceAsync()
        {
            return await Query()
                .Where(b => b.InsuranceExpiryDate.HasValue && b.InsuranceExpiryDate < DateTime.Today)
                .OrderBy(b => b.InsuranceExpiryDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithExpiringInsuranceAsync(int withinDays = 30)
        {
            var expiryDate = DateTime.Today.AddDays(withinDays);
            return await Query()
                .Where(b => b.InsuranceExpiryDate.HasValue &&
                           b.InsuranceExpiryDate >= DateTime.Today &&
                           b.InsuranceExpiryDate <= expiryDate)
                .OrderBy(b => b.InsuranceExpiryDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesBySeatingCapacityAsync(int minCapacity, int? maxCapacity = null)
        {
            var query = Query().Where(b => b.SeatingCapacity >= minCapacity);

            if (maxCapacity.HasValue)
            {
                query = query.Where(b => b.SeatingCapacity <= maxCapacity.Value);
            }

            return await query.OrderBy(b => b.SeatingCapacity).ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithSpecialEquipmentAsync(string equipment)
        {
            return await Query()
                .Where(b => b.SpecialEquipment != null && b.SpecialEquipment.Contains(equipment))
                .OrderBy(b => b.BusNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithGpsAsync()
        {
            return await Query()
                .Where(b => b.GPSTracking)
                .OrderBy(b => b.BusNumber)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await CountAsync();
        }

        public async Task<int> GetActiveCountAsync()
        {
            return await CountAsync(b => b.Status == "Active");
        }

        public async Task<int> GetAverageAgeAsync()
        {
            var currentYear = DateTime.Now.Year;
            var averageYear = await Query()
                .AverageAsync(b => b.Year);

            return currentYear - (int)averageYear;
        }

        public async Task<decimal> GetTotalFleetValueAsync()
        {
            return await Query()
                .Where(b => b.PurchasePrice.HasValue)
                .SumAsync(b => b.PurchasePrice ?? 0);
        }

        public async Task<Dictionary<string, int>> GetCountByStatusAsync()
        {
            return await Query()
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<Dictionary<string, int>> GetCountByMakeAsync()
        {
            return await Query()
                .GroupBy(b => b.Make)
                .Select(g => new { Make = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Make, x => x.Count);
        }

        public async Task<Dictionary<int, int>> GetCountByYearAsync()
        {
            return await Query()
                .GroupBy(b => b.Year)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Year, x => x.Count);
        }

        public IEnumerable<Bus> GetActiveBuses()
        {
            return Query()
                .Where(b => b.Status == "Active")
                .OrderBy(b => b.BusNumber)
                .ToList();
        }

        public IEnumerable<Bus> GetAvailableBuses(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null)
        {
            var activeBuses = GetActiveBuses();

            if (!startTime.HasValue || !endTime.HasValue)
            {
                return activeBuses;
            }

            var conflictingBusIds = Context.Activities
                .Where(a => a.Date.Date == availabilityDate.Date &&
                           ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                            (a.EventTime > startTime && a.EventTime <= endTime) ||
                            (a.LeaveTime <= startTime && a.EventTime >= endTime)))
                .Select(a => a.AssignedVehicleId)
                .ToList();

            return activeBuses.Where(b => !conflictingBusIds.Contains(b.BusId));
        }

        public IEnumerable<Bus> GetBusesByStatus(string status)
        {
            return Query()
                .Where(b => b.Status == status)
                .OrderBy(b => b.BusNumber)
                .ToList();
        }

        public Bus? GetByBusNumber(string busNumber)
        {
            return Query()
                .FirstOrDefault(b => b.BusNumber == busNumber);
        }

        public IEnumerable<Bus> GetBusesDueForInspection(int withinDays = 30)
        {
            var cutoffDate = DateTime.Today.AddDays(-365 + withinDays);
            return Query()
                .Where(b => !b.DateLastInspection.HasValue || b.DateLastInspection <= cutoffDate)
                .OrderBy(b => b.DateLastInspection ?? DateTime.MinValue)
                .ToList();
        }
    }
}
