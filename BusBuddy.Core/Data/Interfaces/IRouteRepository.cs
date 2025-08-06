using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Route-specific repository interface
/// Extends generic repository with route-specific operations
/// </summary>
public interface IRouteRepository : IRepository<Route>
{
    // Route-specific queries
    Task<IEnumerable<Route>> GetRoutesByDateAsync(DateTime targetDate);
    Task<IEnumerable<Route>> GetRoutesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Route>> GetRoutesByNameAsync(string routeName);
    Task<IEnumerable<Route>> GetActiveRoutesAsync();
    Task<Route?> GetRouteByNameAndDateAsync(string routeName, DateTime targetDate);

    // Additional async methods for compatibility
    Task UpdateAsync(Route route);
    Task<IEnumerable<Route>> GetAllRoutesAsync();
    Task<bool> DeleteRouteAsync(int routeId);
    Task<Route?> GetRouteByIdAsync(int routeId);

    // Vehicle and driver assignments
    Task<IEnumerable<Route>> GetRoutesByVehicleAsync(int vehicleId, DateTime? targetDate = null);
    Task<IEnumerable<Route>> GetRoutesByDriverAsync(int driverId, DateTime? targetDate = null);
    Task<IEnumerable<Route>> GetRoutesWithoutVehicleAssignmentAsync(DateTime targetDate);
    Task<IEnumerable<Route>> GetRoutesWithoutDriverAssignmentAsync(DateTime targetDate);

    // Mileage and statistics
    Task<decimal> GetTotalMileageByDateAsync(DateTime targetDate);
    Task<decimal> GetTotalMileageByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetAverageRidershipByRouteAsync(string routeName, DateTime? startDate = null, DateTime? endDate = null);
    Task<Dictionary<string, decimal>> GetMileageByRouteNameAsync(DateTime startDate, DateTime endDate);
    Task<Dictionary<string, int>> GetRidershipByRouteNameAsync(DateTime startDate, DateTime endDate);

    // Schedule validation
    Task<bool> ValidateRouteScheduleAsync(DateTime targetDate);
    Task<IEnumerable<string>> GetRouteValidationErrorsAsync(DateTime targetDate);
    Task<IEnumerable<Route>> GetRoutesWithMileageIssuesAsync(DateTime? startDate = null, DateTime? endDate = null);

    // Reporting
    Task<IEnumerable<Route>> GetMostActiveRoutesAsync(DateTime startDate, DateTime endDate, int count = 10);
    Task<IEnumerable<Route>> GetLeastActiveRoutesAsync(DateTime startDate, DateTime endDate, int count = 10);
    Task<Dictionary<DateTime, int>> GetDailyRouteCountAsync(DateTime startDate, DateTime endDate);

    // Synchronous methods for Syncfusion data binding
    IEnumerable<Route> GetRoutesByDate(DateTime targetDate);
    IEnumerable<Route> GetRoutesByName(string routeName);
    IEnumerable<Route> GetActiveRoutes();
    IEnumerable<Route> GetRoutesByVehicle(int vehicleId, DateTime? targetDate = null);
    IEnumerable<Route> GetRoutesByDriver(int driverId, DateTime? targetDate = null);
    decimal GetTotalMileageByDate(DateTime targetDate);
}
