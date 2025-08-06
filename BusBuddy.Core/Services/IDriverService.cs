using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service interface for managing school bus drivers
    /// Provides CRUD operations and business logic for driver management, including route assignments
    /// </summary>
    public interface IDriverService
    {
        // Basic CRUD Operations
        Task<List<Driver>> GetAllDriversAsync();
        Task<Driver?> GetDriverByIdAsync(int driverId);
        Task<Driver> AddDriverAsync(Driver driver);
        Task<bool> UpdateDriverAsync(Driver driver);
        Task<bool> DeleteDriverAsync(int driverId);

        // Query Operations
        Task<List<Driver>> GetActiveDriversAsync();
        Task<List<Driver>> GetDriversByQualificationStatusAsync(string status);
        Task<List<Driver>> GetDriversByLicenseStatusAsync(string status);
        Task<List<Driver>> SearchDriversAsync(string searchTerm);

        // Route Assignment
        Task<List<Driver>> GetAvailableDriversForRouteAsync(DateTime routeDate, bool isAMRoute);
        Task<bool> AssignDriverToRouteAsync(int driverId, int routeId, bool isAMRoute);
        Task<bool> RemoveDriverFromRouteAsync(int routeId, bool isAMRoute);
        Task<List<Route>> GetDriverRoutesAsync(int driverId, DateTime? startDate = null, DateTime? endDate = null);
        Task<bool> IsDriverAvailableForRouteAsync(int driverId, DateTime routeDate, bool isAMRoute);

        // License and Qualification Management
        Task<bool> UpdateDriverLicenseInfoAsync(int driverId, string licenseNumber, string licenseClass, DateTime expiryDate, string? endorsements = null);
        Task<bool> UpdateDriverQualificationAsync(int driverId, bool trainingComplete, DateTime? backgroundCheckDate = null, DateTime? drugTestDate = null, DateTime? physicalExamDate = null);
        Task<bool> UpdateDriverStatusAsync(int driverId, string status);

        // Driver Validation
        Task<List<string>> ValidateDriverAsync(Driver driver);

        // Analytics and Reporting
        Task<Dictionary<string, int>> GetDriverStatisticsAsync();
        Task<List<Driver>> GetDriversNeedingRenewalAsync();
        Task<Dictionary<string, double>> GetDriverAssignmentMetricsAsync(DateTime startDate, DateTime endDate);
        Task<string> ExportDriversToCsvAsync();

#if DEBUG
        // DEBUG Instrumentation
        Task<Dictionary<string, object>> GetDriverDiagnosticsAsync(int driverId);
        Task<Dictionary<string, object>> GetDriverOperationMetricsAsync();
#endif
    }
}
