using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Driver-specific repository implementation
/// Extends generic repository with driver-specific operations
/// </summary>
public class DriverRepository : Repository<Driver>, IDriverRepository
{
    public DriverRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService)
    {
    }

    #region Async Driver-Specific Operations

    public async Task<IEnumerable<Driver>> GetActiveDriversAsync()
    {
        return await Query()
            .Where(d => d.Status == "Active")
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetAvailableDriversAsync(DateTime checkDate, TimeSpan? startTime = null, TimeSpan? endTime = null)
    {
        var activeDrivers = await GetActiveDriversAsync();

        if (!startTime.HasValue || !endTime.HasValue)
        {
            return activeDrivers;
        }

        // Get drivers that don't have conflicting activities
        var conflictingDriverIds = await Context.Activities
            .Where(a => a.Date.Date == checkDate.Date &&
                       ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                        (a.EventTime > startTime && a.EventTime <= endTime) ||
                        (a.LeaveTime <= startTime && a.EventTime >= endTime)))
            .Select(a => a.DriverId)
            .ToListAsync();

        return activeDrivers.Where(d => !conflictingDriverIds.Contains(d.DriverId));
    }

    public async Task<Driver?> GetDriverByEmployeeNumberAsync(string employeeNumber)
    {
        return await FirstOrDefaultAsync(d => d.LicenseNumber == employeeNumber);
    }

    public async Task<IEnumerable<Driver>> GetDriversByLicenseTypeAsync(string licenseType)
    {
        return await FindAsync(d => d.DriversLicenceType == licenseType && d.Status == "Active");
    }

    public async Task<IEnumerable<Driver>> SearchDriversAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetActiveDriversAsync();
        }

        return await Query()
            .Where(d => d.Status == "Active" &&
                       (d.DriverName.Contains(searchTerm) ||
                        (d.DriverEmail != null && d.DriverEmail.Contains(searchTerm))))
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversWithCompletedTrainingAsync()
    {
        return await FindAsync(d => d.TrainingComplete && d.Status == "Active");
    }

    public async Task<IEnumerable<Driver>> GetDriversWithPendingTrainingAsync()
    {
        return await FindAsync(d => !d.TrainingComplete && d.Status == "Active");
    }

    public async Task<Driver?> GetDriverByPhoneAsync(string phone)
    {
        return await FirstOrDefaultAsync(d => d.DriverPhone == phone);
    }

    public async Task<Driver?> GetDriverByEmailAsync(string email)
    {
        return await FirstOrDefaultAsync(d => d.DriverEmail == email);
    }

    public async Task<IEnumerable<Driver>> GetDriversWithExpiringLicensesAsync(int withinDays = 30)
    {
        var futureDate = DateTime.Today.AddDays(withinDays);
        return await Query()
            .Where(d => d.Status == "Active" &&
                       d.LicenseExpiryDate.HasValue &&
                       d.LicenseExpiryDate.Value >= DateTime.Today &&
                       d.LicenseExpiryDate.Value <= futureDate)
            .OrderBy(d => d.LicenseExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversWithExpiredLicensesAsync()
    {
        return await Query()
            .Where(d => d.LicenseExpiryDate.HasValue && d.LicenseExpiryDate.Value < DateTime.Today)
            .OrderBy(d => d.LicenseExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversRequiringMedicalExamAsync(int withinDays = 30)
    {
        var futureDate = DateTime.Today.AddDays(withinDays);
        return await Query()
            .Where(d => d.Status == "Active" &&
                       d.PhysicalExamExpiry.HasValue &&
                       d.PhysicalExamExpiry.Value >= DateTime.Today &&
                       d.PhysicalExamExpiry.Value <= futureDate)
            .OrderBy(d => d.PhysicalExamExpiry)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversRequiringTrainingRenewalAsync(int withinDays = 30)
    {
        var futureDate = DateTime.Today.AddDays(withinDays);
        return await Query()
            .Where(d => d.Status == "Active" &&
                       d.BackgroundCheckExpiry.HasValue &&
                       d.BackgroundCheckExpiry.Value >= DateTime.Today &&
                       d.BackgroundCheckExpiry.Value <= futureDate)
            .OrderBy(d => d.BackgroundCheckExpiry)
            .ToListAsync();
    }

    public async Task<bool> IsDriverAvailableAsync(int driverId, DateTime checkDate, TimeSpan startTime, TimeSpan endTime)
    {
        var conflictingActivities = await Context.Activities
            .AnyAsync(a => a.DriverId == driverId &&
                          a.Date.Date == checkDate.Date &&
                          ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                           (a.EventTime > startTime && a.EventTime <= endTime) ||
                           (a.LeaveTime <= startTime && a.EventTime >= endTime)));

        return !conflictingActivities;
    }

    public async Task<IEnumerable<Driver>> GetDriversScheduledForDateAsync(DateTime checkDate)
    {
        var driverIds = await Context.Activities
            .Where(a => a.Date.Date == checkDate.Date)
            .Select(a => a.DriverId)
            .Distinct()
            .ToListAsync();

        return await Query()
            .Where(d => driverIds.Contains(d.DriverId))
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversWithNoScheduleAsync(DateTime checkDate)
    {
        var scheduledDriverIds = await Context.Activities
            .Where(a => a.Date.Date == checkDate.Date)
            .Select(a => a.DriverId)
            .Distinct()
            .ToListAsync();

        return await Query()
            .Where(d => d.Status == "Active" && !scheduledDriverIds.Contains(d.DriverId))
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<int> GetTotalDriverCountAsync()
    {
        return await CountAsync();
    }

    public async Task<int> GetActiveDriverCountAsync()
    {
        return await CountAsync(d => d.Status == "Active");
    }

    public async Task<Dictionary<string, int>> GetDriverCountByLicenseTypeAsync()
    {
        return await Query()
            .GroupBy(d => d.DriversLicenceType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<bool, int>> GetDriverCountByTrainingStatusAsync()
    {
        return await Query()
            .GroupBy(d => d.TrainingComplete)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<IEnumerable<Driver>> GetDriversByPerformanceRatingAsync(decimal minRating)
    {
        // Since the Driver model doesn't have PerformanceRating, return active drivers
        return await Query()
            .Where(d => d.Status == "Active")
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversWithEmergencyContactsAsync()
    {
        return await Query()
            .Where(d => d.Status == "Active" &&
                       !string.IsNullOrEmpty(d.EmergencyContactName) &&
                       !string.IsNullOrEmpty(d.EmergencyContactPhone))
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversWithoutEmergencyContactsAsync()
    {
        return await Query()
            .Where(d => d.Status == "Active" &&
                       (string.IsNullOrEmpty(d.EmergencyContactName) ||
                        string.IsNullOrEmpty(d.EmergencyContactPhone)))
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Driver>> GetDriversByLocationAsync(string city, string? state = null)
    {
        var query = Query().Where(d => d.Status == "Active" && d.City == city);

        if (!string.IsNullOrEmpty(state))
        {
            query = query.Where(d => d.State == state);
        }

        return await query
            .OrderBy(d => d.DriverName)
            .ToListAsync();
    }

    #endregion

    #region Synchronous Driver-Specific Operations

    public IEnumerable<Driver> GetActiveDrivers()
    {
        return Query()
            .Where(d => d.Status == "Active")
            .OrderBy(d => d.DriverName)
            .ToList();
    }

    public IEnumerable<Driver> GetAvailableDrivers(DateTime checkDate, TimeSpan? startTime = null, TimeSpan? endTime = null)
    {
        var activeDrivers = GetActiveDrivers();

        if (!startTime.HasValue || !endTime.HasValue)
        {
            return activeDrivers;
        }

        // Get drivers that don't have conflicting activities
        var conflictingDriverIds = Context.Activities
            .Where(a => a.Date.Date == checkDate.Date &&
                       ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                        (a.EventTime > startTime && a.EventTime <= endTime) ||
                        (a.LeaveTime <= startTime && a.EventTime >= endTime)))
            .Select(a => a.DriverId)
            .ToList();

        return activeDrivers.Where(d => !conflictingDriverIds.Contains(d.DriverId));
    }

    public IEnumerable<Driver> GetDriversByLicenseType(string licenseType)
    {
        return Find(d => d.DriversLicenceType == licenseType && d.Status == "Active");
    }

    public IEnumerable<Driver> GetDriversWithCompletedTraining()
    {
        return Find(d => d.TrainingComplete && d.Status == "Active");
    }

    public IEnumerable<Driver> GetDriversWithExpiringLicenses(int withinDays = 30)
    {
        var futureDate = DateTime.Today.AddDays(withinDays);
        return Query()
            .Where(d => d.Status == "Active" &&
                       d.LicenseExpiryDate.HasValue &&
                       d.LicenseExpiryDate.Value >= DateTime.Today &&
                       d.LicenseExpiryDate.Value <= futureDate)
            .OrderBy(d => d.LicenseExpiryDate)
            .ToList();
    }

    public bool IsDriverAvailable(int driverId, DateTime checkDate, TimeSpan startTime, TimeSpan endTime)
    {
        var conflictingActivities = Context.Activities
            .Any(a => a.DriverId == driverId &&
                     a.Date.Date == checkDate.Date &&
                     ((a.LeaveTime >= startTime && a.LeaveTime < endTime) ||
                      (a.EventTime > startTime && a.EventTime <= endTime) ||
                      (a.LeaveTime <= startTime && a.EventTime >= endTime)));

        return !conflictingActivities;
    }

    public Driver? GetDriverByEmployeeNumber(string employeeNumber)
    {
        return FirstOrDefault(d => d.LicenseNumber == employeeNumber);
    }

    public IEnumerable<Driver> SearchDrivers(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return GetActiveDrivers();
        }

        return Query()
            .Where(d => d.Status == "Active" &&
                       (d.DriverName.Contains(searchTerm) ||
                        (d.DriverEmail != null && d.DriverEmail.Contains(searchTerm))))
            .OrderBy(d => d.DriverName)
            .ToList();
    }

    #endregion
}
