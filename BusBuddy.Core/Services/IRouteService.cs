using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Enhanced Route Service interface with comprehensive route assignment capabilities
    /// Implements Result pattern for robust error handling and logging
    /// Supports student-to-route assignments, capacity validation, and utilization analytics
    /// </summary>
    public enum RouteTimeSlot
    {
        AM,
        PM,
        Both
    }

    public class RouteValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public interface IRouteService
    {
        // Basic CRUD Operations with Result Pattern
        Task<Result<IEnumerable<Route>>> GetAllActiveRoutesAsync();
        Task<Result<IEnumerable<Route>>> GetAllRoutesAsync();
        Task<Result<Route>> GetRouteByIdAsync(int id);
        Task<Result<Route>> CreateRouteAsync(Route route);
        Task<Result<Route>> UpdateRouteAsync(Route route);
        Task<Result<bool>> DeleteRouteAsync(int id);
        Task<Result<IEnumerable<Route>>> SearchRoutesAsync(string searchTerm);
        Task<Result<IEnumerable<Route>>> GetRoutesByBusIdAsync(int busId);
        Task<Result<bool>> IsRouteNumberUniqueAsync(string routeNumber, int? excludeId = null);

        // Route Stop Management
        Task<Result<IEnumerable<RouteStop>>> GetRouteStopsAsync(int routeId);
        Task<Result<RouteStop>> AddRouteStopAsync(RouteStop routeStop);
        Task<Result<RouteStop>> UpdateRouteStopAsync(RouteStop routeStop);
        Task<Result<bool>> DeleteRouteStopAsync(int routeStopId);
        Task<Result<decimal>> GetRouteTotalDistanceAsync(int routeId);
        Task<Result<TimeSpan>> GetRouteEstimatedTimeAsync(int routeId);

        // Advanced Route Assignment Features
        Task<Result<List<Bus>>> GetAvailableBusesAsync();
        Task<Result<List<Driver>>> GetAvailableDriversAsync();
        Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId);
        Task<Result<bool>> RemoveStudentFromRouteAsync(int studentId, int routeId);
        Task<Result<List<Student>>> GetUnassignedStudentsAsync();
        Task<Result<List<Route>>> GetRoutesWithCapacityAsync();

        // Route Validation and Analysis
        Task<Result<bool>> ValidateRouteCapacityAsync(int routeId);
        Task<Result<RouteUtilizationStats>> GetRouteUtilizationStatsAsync();
        Task<Result<bool>> CanAssignStudentToRouteAsync(int studentId, int routeId);

        // Route Building Methods
        Task<Result<Route>> CreateNewRouteAsync(string routeName, DateTime routeDate, string? description = null);
        Task<Result<bool>> AssignVehicleToRouteAsync(int routeId, int vehicleId, RouteTimeSlot timeSlot);
        Task<Result<bool>> AssignDriverToRouteAsync(int routeId, int driverId, RouteTimeSlot timeSlot);
        Task<Result<RouteStop>> AddStopToRouteAsync(int routeId, RouteStop stop);
        Task<Result<bool>> RemoveStopFromRouteAsync(int routeId, int stopId);
        Task<Result<bool>> ReorderRouteStopsAsync(int routeId, List<int> orderedStopIds);
        Task<Result<RouteValidationResult>> ValidateRouteForActivationAsync(int routeId);
        Task<Result<bool>> ActivateRouteAsync(int routeId);
        Task<Result<bool>> DeactivateRouteAsync(int routeId);
        Task<Result<Route>> CloneRouteAsync(int sourceRouteId, DateTime newDate, string? newRouteName = null);
    }
}
