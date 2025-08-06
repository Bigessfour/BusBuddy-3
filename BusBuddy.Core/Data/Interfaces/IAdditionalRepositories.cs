using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Additional repository interfaces for comprehensive data access
/// </summary>

public interface IFuelRepository : IRepository<Fuel>
{
    Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId);
    Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalFuelCostAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetAverageFuelEfficiencyAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<Fuel>> GetRecentFuelRecordsAsync(int count = 10);

    // Synchronous methods
    IEnumerable<Fuel> GetFuelRecordsByVehicle(int vehicleId);
    IEnumerable<Fuel> GetFuelRecordsByDateRange(DateTime startDate, DateTime endDate);
    decimal GetTotalFuelCost(DateTime startDate, DateTime endDate);
}

public interface IMaintenanceRepository : IRepository<Maintenance>
{
    Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByVehicleAsync(int vehicleId);
    Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByTypeAsync(string maintenanceType);
    Task<decimal> GetTotalMaintenanceCostAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Maintenance>> GetUpcomingMaintenanceAsync(int withinDays = 30);
    Task<IEnumerable<Maintenance>> GetOverdueMaintenanceAsync();

    // Synchronous methods
    IEnumerable<Maintenance> GetMaintenanceRecordsByVehicle(int vehicleId);
    IEnumerable<Maintenance> GetMaintenanceRecordsByDateRange(DateTime startDate, DateTime endDate);
    decimal GetTotalMaintenanceCost(DateTime startDate, DateTime endDate);
}

public interface IScheduleRepository : IRepository<Schedule>
{
    Task<IEnumerable<Schedule>> GetSchedulesByDateAsync(DateTime scheduleDate);
    Task<IEnumerable<Schedule>> GetSchedulesByRouteAsync(int routeId);
    Task<IEnumerable<Schedule>> GetSchedulesByBusAsync(int busId);
    Task<IEnumerable<Schedule>> GetSchedulesByDriverAsync(int driverId);
    Task<bool> HasConflictAsync(int busId, int driverId, DateTime startTime, DateTime endTime);

    // Synchronous methods
    IEnumerable<Schedule> GetSchedulesByDate(DateTime scheduleDate);
    IEnumerable<Schedule> GetSchedulesByRoute(int routeId);
    bool HasConflict(int busId, int driverId, DateTime startTime, DateTime endTime);
}

public interface ISchoolCalendarRepository : IRepository<SchoolCalendar>
{
    Task<IEnumerable<SchoolCalendar>> GetEventsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<SchoolCalendar>> GetEventsByTypeAsync(string eventType);
    Task<IEnumerable<SchoolCalendar>> GetSchoolDaysAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<SchoolCalendar>> GetHolidaysAsync(DateTime startDate, DateTime endDate);
    Task<bool> IsSchoolDayAsync(DateTime checkDate);
    Task<bool> AreRoutesRequiredAsync(DateTime checkDate);

    // Synchronous methods
    IEnumerable<SchoolCalendar> GetEventsByDateRange(DateTime startDate, DateTime endDate);
    IEnumerable<SchoolCalendar> GetSchoolDays(DateTime startDate, DateTime endDate);
    bool IsSchoolDay(DateTime checkDate);
    bool AreRoutesRequired(DateTime checkDate);
}

public interface IActivityScheduleRepository : IRepository<ActivitySchedule>
{
    Task<IEnumerable<ActivitySchedule>> GetSchedulesByDateAsync(DateTime scheduleDate);
    Task<IEnumerable<ActivitySchedule>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ActivitySchedule>> GetSchedulesByTripTypeAsync(string tripType);
    Task<IEnumerable<ActivitySchedule>> GetSchedulesByVehicleAsync(int vehicleId);
    Task<IEnumerable<ActivitySchedule>> GetSchedulesByDriverAsync(int driverId);
    Task<bool> HasConflictAsync(int vehicleId, int driverId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime);

    // Synchronous methods
    IEnumerable<ActivitySchedule> GetSchedulesByDate(DateTime scheduleDate);
    IEnumerable<ActivitySchedule> GetSchedulesByTripType(string tripType);
    bool HasConflict(int vehicleId, int driverId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime);
}
