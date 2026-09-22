using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services
{
    // NOTE (RTD-01): Duplicate RouteTimeSlot enum & RouteValidationResult class removed.
    // Canonical definitions live in Models/Route.Extensions.cs (RouteTimeSlot and RouteValidationResult).
    // Service now consumes BusBuddy.Core.Models.RouteTimeSlot directly to eliminate casts.

    public interface IRouteService
    {
        // Basic CRUD Operations with Result Pattern
        Task<Result<IEnumerable<Route>>> GetAllActiveRoutesAsync();
        Task<Result<IEnumerable<Route>>> GetAllRoutesAsync();
        Task<Result<Route>> GetRouteByIdAsync(int id);
        Task<Result<Route>> CreateRouteAsync(Route route);
        Task<Result<Route>> UpdateRouteAsync(Route route);
        Task<Result<bool>> DeleteRouteAsync(int id);

        // Route Stop Management
        Task<Result<IEnumerable<RouteStop>>> GetRouteStopsAsync(int routeId);

        // Advanced Route Assignment Features
        Task<Result<List<Bus>>> GetAvailableBusesAsync();
        Task<Result<List<Driver>>> GetAvailableDriversAsync();
        Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId);
        Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId, RouteTimeSlot timeSlot);
        Task<Result<bool>> AssignStudentToRouteAsync(int studentId, int routeId, RouteTimeSlot timeSlot, bool overrideSeating);
        Task<Result<bool>> RemoveStudentFromRouteAsync(int studentId, int routeId);
        Task<Result<bool>> RemoveStudentFromRouteAsync(int studentId, int routeId, RouteTimeSlot timeSlot);
        /// <summary>
        /// Same-day not-riding. Does not delete the published stop, year assignment, or student.
        /// </summary>
        Task<Result<RouteRiderException>> RecordRiderExceptionAsync(
            int routeId,
            int studentId,
            DateTime exceptionDate,
            string? reason = null);
        /// <summary>Removes a same-day not-riding row. The year assignment and published stops stay.</summary>
        Task<Result<bool>> ClearRiderExceptionAsync(int routeId, int studentId, DateTime exceptionDate);
        /// <summary>Student ids with a not-riding row on that UTC calendar day. Does not change clocks.</summary>
        Task<Result<IReadOnlyList<int>>> GetRiderExceptionStudentIdsAsync(int routeId, DateTime exceptionDate);
        /// <summary>
        /// Session roster for one school day: assigned students on this row's slot, minus not-riding exceptions.
        /// Capacity is that slot's bus. A missing bus yields capacity 0 and a warning.
        /// </summary>
        Task<Result<RouteSessionLoad>> GetSessionLoadAsync(int routeId, DateTime serviceDate);
        /// <summary>Active students with neither AM nor PM assigned. Prefer the slot overload for fill work.</summary>
        Task<Result<List<Student>>> GetUnassignedStudentsAsync();
        /// <summary>Active students missing that slot. AM-assigned/PM-empty children are returned for PM, and vice versa.</summary>
        Task<Result<List<Student>>> GetUnassignedStudentsAsync(RouteTimeSlot timeSlot);
        Task<Result<List<Student>>> GetStudentsForRouteAsync(int routeId, RouteTimeSlot timeSlot);
        Task<Result<List<Student>>> AutoAssignStudentsAsync(int routeId, RouteTimeSlot timeSlot);
        Task<Result<List<Route>>> GetRoutesWithCapacityAsync();

        // Route Validation and Analysis
        Task<Result<RouteUtilizationStats>> GetRouteUtilizationStatsAsync();

        // Route Building Methods
        Task<Result<Route>> CreateNewRouteAsync(
            string routeName,
            DateTime routeDate,
            string? description = null,
            string? session = null,
            string? school = null);
        Task<Result<bool>> AssignVehicleToRouteAsync(int routeId, int vehicleId, BusBuddy.Core.Models.RouteTimeSlot timeSlot);
        Task<Result<bool>> AssignDriverToRouteAsync(int routeId, int driverId, BusBuddy.Core.Models.RouteTimeSlot timeSlot);
        Task<Result<RouteStop>> AddStopToRouteAsync(int routeId, RouteStop routeStop);
        /// <summary>Updates stop name, address, and validated coordinates. Stop order and published clocks are unchanged.</summary>
        Task<Result<RouteStop>> UpdateRouteStopAsync(int routeId, RouteStop routeStop);
        Task<Result<bool>> RemoveStopFromRouteAsync(int routeId, int stopId);
        Task<Result<bool>> ReorderRouteStopsAsync(int routeId, List<int> orderedStopIds);
        Task<Result<RouteValidationResult>> ValidateRouteForActivationAsync(int routeId);
        Task<Result<bool>> ActivateRouteAsync(int routeId);
        Task<Result<bool>> DeactivateRouteAsync(int routeId);
        Task<Result<Route>> CloneRouteAsync(int sourceRouteId, DateTime newDate, string? newRouteName = null);
        // Persist updated stop timing (arrival/departure)
        Task<Result<bool>> UpdateRouteStopsTimingAsync(int routeId, IEnumerable<RouteStop> stops);
        /// <summary>
        /// Rebuilds waypoints from published <see cref="RouteStop"/> rows and refreshes the Google drive path.
        /// </summary>
        Task<Result<BusBuddy.Core.Services.GoogleMaps.DrivePathRefreshResult>> RefreshDrivePathAsync(int routeId);
    }
}
