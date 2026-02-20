using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;



namespace BusBuddy.Core.Data.Repositories
{
    public enum VehicleStatus
    {
        Active,
        Inactive,
        Maintenance,
        Retired
    }

    /// <summary>
    /// Bus-specific repository implementation
    /// Extends generic repository with bus/vehicle-specific operations
    /// </summary>
    public class BusRepository : Repository<Bus>, IBusRepository
    {
        // Prefer enhanced context when available; current signature keeps backward compatibility
        public BusRepository(BusBuddy.Core.Data.BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
        {
        }

        [Obsolete("Use GetBusesByStatusAsync for clearer domain semantics.")]
        public async Task<IEnumerable<Bus>> GetVehiclesByStatusAsync(string status) => await GetBusesByStatusAsync(status);

        public async Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status)
        {
            return await QueryNoTracking()
                .Where(v => v.Status == status)
                .OrderBy(v => v.BusNumber)
                .ToListAsync();
        }

        #region Async Bus-Specific Operations
        [Obsolete("Use GetActiveBusesAsync")]
        public async Task<IEnumerable<Bus>> GetActiveVehiclesAsync() => await GetActiveBusesAsync();

        public async Task<IEnumerable<Bus>> GetActiveBusesAsync()
        {
            return await QueryNoTracking()
                .Where(v => v.Status == "Active")
                .OrderBy(v => v.BusNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetAvailableBusesAsync(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null)
        {
            var activeBuses = await GetActiveBusesAsync();

            if (!startTime.HasValue || !endTime.HasValue)
            {
                return activeBuses;
            }

            // Get buses that don't have conflicting activities
            var conflictingBusIds = await Context.Activities
                .Where(a => a.Date.Date == availabilityDate.Date &&
                           ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                            (a.EventTime > startTime && a.EventTime <= endTime) ||
                            (a.LeaveTime <= startTime && a.EventTime >= endTime)))
                .Select(a => a.AssignedVehicleId)
                .ToListAsync();

            return activeBuses.Where(v => !conflictingBusIds.Contains(v.BusId));
        }

        [Obsolete("Use GetBusesByStatusAsync(VehicleStatus)")]
        public async Task<IEnumerable<Bus>> GetVehiclesByStatusAsync(VehicleStatus status) => await GetBusesByStatusAsync(status);

        public async Task<IEnumerable<Bus>> GetBusesByStatusAsync(VehicleStatus status)
        {
            return await QueryNoTracking()
                .Where(v => v.Status == status.ToString())
                .OrderBy(v => v.BusNumber)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        [Obsolete("Use GetBusesByFleetTypeAsync")]
        public async Task<IEnumerable<Bus>> GetVehiclesByFleetTypeAsync(string fleetType) => await GetBusesByFleetTypeAsync(fleetType);

        public async Task<IEnumerable<Bus>> GetBusesByFleetTypeAsync(string fleetType)
        {
            return await QueryNoTracking()
                .Where(v => v.FleetType == fleetType)
                .OrderBy(v => v.BusNumber)
                .ToListAsync();
        }

        [Obsolete("Use GetBusByBusNumberAsync")]
        public async Task<Bus?> GetVehicleByBusNumberAsync(string busNumber) => await GetBusByBusNumberAsync(busNumber);

        public async Task<Bus?> GetBusByBusNumberAsync(string busNumber)
        {
            return await QueryNoTracking()
                .FirstOrDefaultAsync(v => v.BusNumber == busNumber);
        }

        [Obsolete("Use GetBusByVINAsync")]
        public async Task<Bus?> GetVehicleByVINAsync(string vin) => await GetBusByVINAsync(vin);

        public async Task<Bus?> GetBusByVINAsync(string vin)
        {
            return await QueryNoTracking()
                .FirstOrDefaultAsync(v => v.VINNumber == vin);
        }

        [Obsolete("Use GetBusByLicenseNumberAsync")]
        public async Task<Bus?> GetVehicleByLicenseNumberAsync(string licenseNumber) => await GetBusByLicenseNumberAsync(licenseNumber);

        public async Task<Bus?> GetBusByLicenseNumberAsync(string licenseNumber)
        {
            return await QueryNoTracking()
                .FirstOrDefaultAsync(v => v.LicenseNumber == licenseNumber);
        }
        #endregion

        #region Maintenance and Inspection
        public async Task<IEnumerable<Bus>> GetBusesDueForInspectionAsync(int withinDays = 30)
        {
            var cutoffDate = DateTime.Today.AddDays(-365 + withinDays); // Due within specified days of 1-year mark
            return await QueryNoTracking()
                .Where(v => !v.DateLastInspection.HasValue || v.DateLastInspection <= cutoffDate)
                .OrderBy(v => v.DateLastInspection ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithExpiredInspectionAsync()
        {
            var oneYearAgo = DateTime.Today.AddYears(-1);
            return await QueryNoTracking()
                .Where(v => !v.DateLastInspection.HasValue || v.DateLastInspection <= oneYearAgo)
                .OrderBy(v => v.DateLastInspection ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesDueForMaintenanceAsync()
        {
            return await QueryNoTracking()
                .Where(v => v.NextMaintenanceDue.HasValue && v.NextMaintenanceDue <= DateTime.Today.AddDays(30))
                .OrderBy(v => v.NextMaintenanceDue)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithExpiredInsuranceAsync()
        {
            return await QueryNoTracking()
                .Where(v => v.InsuranceExpiryDate.HasValue && v.InsuranceExpiryDate < DateTime.Today)
                .OrderBy(v => v.InsuranceExpiryDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithExpiringInsuranceAsync(int withinDays = 30)
        {
            var expiryDate = DateTime.Today.AddDays(withinDays);
            return await QueryNoTracking()
                .Where(v => v.InsuranceExpiryDate.HasValue &&
                           v.InsuranceExpiryDate >= DateTime.Today &&
                           v.InsuranceExpiryDate <= expiryDate)
                .OrderBy(v => v.InsuranceExpiryDate)
                .ToListAsync();
        }
        #endregion

        #region Capacity and Features
        public async Task<IEnumerable<Bus>> GetBusesBySeatingCapacityAsync(int minCapacity, int? maxCapacity = null)
        {
            var query = QueryNoTracking().Where(v => v.SeatingCapacity >= minCapacity);

            if (maxCapacity.HasValue)
            {
                query = query.Where(v => v.SeatingCapacity <= maxCapacity.Value);
            }

            return await query.OrderBy(v => v.SeatingCapacity).ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithSpecialEquipmentAsync(string equipment)
        {
            return await QueryNoTracking()
                .Where(v => v.SpecialEquipment != null && v.SpecialEquipment.Contains(equipment))
                .OrderBy(v => v.BusNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<Bus>> GetBusesWithGPSAsync()
        {
            return await QueryNoTracking()
                .Where(v => v.GPSTracking)
                .OrderBy(v => v.BusNumber)
                .ToListAsync();
        }
        #endregion

        #region Statistics and Reporting
        public async Task<int> GetTotalBusCountAsync()
        {
            return await CountAsync();
        }

        public async Task<int> GetActiveBusCountAsync()
        {
            return await CountAsync(v => v.Status == "Active");
        }

        public async Task<int> GetAverageBusAgeAsync()
        {
            var currentYear = DateTime.Now.Year;
            var averageYear = await QueryNoTracking()
                .AverageAsync(v => v.Year);

            return currentYear - (int)averageYear;
        }

        public async Task<decimal> GetTotalFleetValueAsync()
        {
            return await QueryNoTracking()
                .Where(v => v.PurchasePrice.HasValue)
                .SumAsync(v => v.PurchasePrice ?? 0);
        }

        public async Task<Dictionary<string, int>> GetBusCountByStatusAsync()
        {
            return await QueryNoTracking()
                .GroupBy(v => v.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<Dictionary<string, int>> GetBusCountByMakeAsync()
        {
            return await QueryNoTracking()
                .GroupBy(v => v.Make)
                .Select(g => new { Make = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Make, x => x.Count);
        }

        public async Task<Dictionary<int, int>> GetBusCountByYearAsync()
        {
            return await QueryNoTracking()
                .GroupBy(v => v.Year)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Year, x => x.Count);
        }
        #endregion

        #region Synchronous Methods for Syncfusion
        public IEnumerable<Bus> GetActiveBuses()
        {
            return Query()
                .Where(v => v.Status == "Active")
                .OrderBy(v => v.BusNumber)
                .ToList();
        }

        public IEnumerable<Bus> GetAvailableBuses(DateTime availabilityDate, TimeSpan? startTime = null, TimeSpan? endTime = null)
        {
            var activeBuses = GetActiveBuses();

            if (!startTime.HasValue || !endTime.HasValue)
            {
                return activeBuses;
            }

            // Get buses that don't have conflicting activities
            var conflictingBusIds = Context.Activities
                .Where(a => a.Date.Date == availabilityDate.Date &&
                           ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                            (a.EventTime > startTime && a.EventTime <= endTime) ||
                            (a.LeaveTime <= startTime && a.EventTime >= endTime)))
                .Select(a => a.AssignedVehicleId)
                .ToList();

            return activeBuses.Where(v => !conflictingBusIds.Contains(v.BusId));
        }

        public IEnumerable<Bus> GetBusesByStatus(string status)
        {
            return Query()
                .Where(v => v.Status == status)
                .OrderBy(v => v.BusNumber)
                .ToList();
        }

        public Bus? GetBusByBusNumber(string busNumber)
        {
            return Query()
                .FirstOrDefault(v => v.BusNumber == busNumber);
        }

        public IEnumerable<Bus> GetBusesDueForInspection(int withinDays = 30)
        {
            var cutoffDate = DateTime.Today.AddDays(-365 + withinDays);
            return Query()
                .Where(v => !v.DateLastInspection.HasValue || v.DateLastInspection <= cutoffDate)
                .OrderBy(v => v.DateLastInspection ?? DateTime.MinValue)
                .ToList();
        }
        #endregion
    }
}
