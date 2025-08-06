using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Driver-specific repository interface
/// Extends generic repository with driver-specific operations
/// </summary>
public interface IDriverRepository : IRepository<Driver>
{
    // Driver-specific queries
    Task<IEnumerable<Driver>> GetActiveDriversAsync();
    Task<IEnumerable<Driver>> GetAvailableDriversAsync(DateTime checkDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    Task<IEnumerable<Driver>> GetDriversByLicenseTypeAsync(string licenseType);
    Task<IEnumerable<Driver>> GetDriversWithCompletedTrainingAsync();
    Task<IEnumerable<Driver>> GetDriversWithPendingTrainingAsync();
    Task<Driver?> GetDriverByPhoneAsync(string phone);
    Task<Driver?> GetDriverByEmailAsync(string email);

    // License and certification management
    Task<IEnumerable<Driver>> GetDriversWithExpiringLicensesAsync(int withinDays = 30);
    Task<IEnumerable<Driver>> GetDriversWithExpiredLicensesAsync();
    Task<IEnumerable<Driver>> GetDriversRequiringMedicalExamAsync(int withinDays = 30);
    Task<IEnumerable<Driver>> GetDriversRequiringTrainingRenewalAsync(int withinDays = 30);

    // Scheduling and availability
    Task<bool> IsDriverAvailableAsync(int driverId, DateTime checkDate, TimeSpan startTime, TimeSpan endTime);
    Task<IEnumerable<Driver>> GetDriversScheduledForDateAsync(DateTime checkDate);
    Task<IEnumerable<Driver>> GetDriversWithNoScheduleAsync(DateTime checkDate);

    // Performance and statistics
    Task<int> GetTotalDriverCountAsync();
    Task<int> GetActiveDriverCountAsync();
    Task<Dictionary<string, int>> GetDriverCountByLicenseTypeAsync();
    Task<Dictionary<bool, int>> GetDriverCountByTrainingStatusAsync();
    Task<IEnumerable<Driver>> GetDriversByPerformanceRatingAsync(decimal minRating);

    // Emergency and contact information
    Task<IEnumerable<Driver>> GetDriversWithEmergencyContactsAsync();
    Task<IEnumerable<Driver>> GetDriversWithoutEmergencyContactsAsync();
    Task<IEnumerable<Driver>> GetDriversByLocationAsync(string city, string? state = null);

    // Synchronous methods for Syncfusion data binding
    IEnumerable<Driver> GetActiveDrivers();
    IEnumerable<Driver> GetAvailableDrivers(DateTime checkDate, TimeSpan? startTime = null, TimeSpan? endTime = null);
    IEnumerable<Driver> GetDriversByLicenseType(string licenseType);
    IEnumerable<Driver> GetDriversWithCompletedTraining();
    IEnumerable<Driver> GetDriversWithExpiringLicenses(int withinDays = 30);
    bool IsDriverAvailable(int driverId, DateTime checkDate, TimeSpan startTime, TimeSpan endTime);
}
