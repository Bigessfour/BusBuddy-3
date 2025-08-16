using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Data.Repositories;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BusBuddy.Core.Data.UnitOfWork;

/// <summary>
/// Unit of Work implementation for managing transactions and coordinating repositories
/// Ensures data consistency across multiple repository operations
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly BusBuddy.Core.Data.BusBuddyDbContext _context;
    private readonly IUserContextService _userContextService;
    private IDbContextTransaction? _transaction;
    private string? _currentAuditUser;
    private bool _disposed;

    // Repository instances
    private IActivityRepository? _activities;
    private IBusRepository? _buses;
    private IDriverRepository? _drivers;
    private IRouteRepository? _routes;
    private IStudentRepository? _students;
    private IFuelRepository? _fuelRecords;
    private IMaintenanceRepository? _maintenanceRecords;
    private IScheduleRepository? _schedules;
    private ISchoolCalendarRepository? _schoolCalendar;
    private IActivityScheduleRepository? _activitySchedules;
    // REMOVED: ITicketRepository - deprecated module

    // Generic repository cache
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(BusBuddyDbContext context, IUserContextService userContextService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
    }

    #region Repository Properties

    public IActivityRepository Activities
    {
        get { return _activities ?? (_activities = new ActivityRepository(_context, _userContextService)); }
    }

    public IBusRepository Buses
    {
        get { return _buses ?? (_buses = new BusRepository(_context, _userContextService)); }
    }

    public IDriverRepository Drivers
    {
        get { return _drivers ?? (_drivers = new DriverRepository(_context, _userContextService)); }
    }

    public IRouteRepository Routes
    {
        get { return _routes ?? (_routes = new RouteRepository(_context, _userContextService)); }
    }

    public IStudentRepository Students
    {
        get { return _students ?? (_students = new StudentRepository(_context, _userContextService)); }
    }

    public IFuelRepository FuelRecords
    {
        get { return _fuelRecords ?? (_fuelRecords = new FuelRepository(_context, _userContextService)); }
    }

    public IMaintenanceRepository MaintenanceRecords
    {
        get { return _maintenanceRecords ?? (_maintenanceRecords = new MaintenanceRepository(_context, _userContextService)); }
    }

    public IScheduleRepository Schedules
    {
        get { return _schedules ?? (_schedules = new ScheduleRepository(_context, _userContextService)); }
    }

    public ISchoolCalendarRepository SchoolCalendar
    {
        get { return _schoolCalendar ?? (_schoolCalendar = new SchoolCalendarRepository(_context, _userContextService)); }
    }

    public IActivityScheduleRepository ActivitySchedules
    {
        get { return _activitySchedules ?? (_activitySchedules = new ActivityScheduleRepository(_context, _userContextService)); }
    }

    #endregion

    #region Generic Repository Access

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new Repository<T>(_context, _userContextService);
            _repositories[type] = repository;
        }
        return (IRepository<T>)repository;
    }

    #endregion

    #region Transaction Management

    public async Task<int> SaveChangesAsync()
    {
        return await SaveChangesAsync(CancellationToken.None);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            UpdateAuditFields();
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the exception here if logging is configured
            throw new InvalidOperationException("Failed to save changes to the database.", ex);
        }
    }

    public int SaveChanges()
    {
        try
        {
            UpdateAuditFields();
            return _context.SaveChanges();
        }
        catch (Exception ex)
        {
            // Log the exception here if logging is configured
            throw new InvalidOperationException("Failed to save changes to the database.", ex);
        }
    }

    #endregion

    #region Transaction Support

    public async Task BeginTransactionAsync()
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await SaveChangesAsync();
            await _transaction.CommitAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _transaction.RollbackAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void BeginTransaction()
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _transaction = _context.Database.BeginTransaction();
    }

    public void CommitTransaction()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            SaveChanges();
            _transaction.Commit();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void RollbackTransaction()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            _transaction.Rollback();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    #endregion

    #region Bulk Operations

    public async Task<int> BulkInsertAsync<T>(IEnumerable<T> entities) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return 0;
        }


        await _context.Set<T>().AddRangeAsync(entityList);
        return await SaveChangesAsync();
    }

    public async Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return 0;
        }


        _context.Set<T>().UpdateRange(entityList);
        return await SaveChangesAsync();
    }

    public async Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            return 0;
        }


        _context.Set<T>().RemoveRange(entityList);
        return await SaveChangesAsync();
    }

    #endregion

    #region Database Operations

    public async Task<bool> DatabaseExistsAsync()
    {
        return await _context.Database.CanConnectAsync();
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task MigrateDatabaseAsync()
    {
        await _context.Database.MigrateAsync();
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Audit and Tracking

    public void SetAuditUser(string username)
    {
        _currentAuditUser = username;
    }

    public string? GetCurrentAuditUser()
    {
        return _currentAuditUser;
    }

    public async Task<IEnumerable<T>> GetAuditTrailAsync<T>(int entityId) where T : class
    {
        // This would typically involve a separate audit table
        // For now, return empty collection
        await Task.CompletedTask;
        return Enumerable.Empty<T>();
    }

    #endregion

    #region Cache Management

    public async Task RefreshCacheAsync()
    {
        // Clear the change tracker to refresh entities from database
        _context.ChangeTracker.Clear();
        await Task.CompletedTask;
    }

    public void ClearCache()
    {
        _context.ChangeTracker.Clear();
    }

    #endregion

    #region Helper Methods

    private void UpdateAuditFields()
    {
        var entries = _context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var currentTime = DateTime.UtcNow;
            var currentUser = _currentAuditUser ?? "System";

            // Handle BaseEntity pattern
            if (entry.Entity is Models.Base.BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    baseEntity.CreatedDate = currentTime;
                    baseEntity.CreatedBy = currentUser;
                }
                else if (entry.State == EntityState.Modified)
                {
                    baseEntity.UpdatedDate = currentTime;
                    baseEntity.UpdatedBy = currentUser;
                }
            }
            else
            {
                // Handle entities with audit fields but not inheriting from BaseEntity (like Student)
                var entityType = entry.Entity.GetType();

                if (entry.State == EntityState.Added)
                {
                    // Set CreatedBy and CreatedDate if properties exist
                    var createdByProperty = entityType.GetProperty("CreatedBy");
                    var createdDateProperty = entityType.GetProperty("CreatedDate");

                    if (createdByProperty != null && createdByProperty.CanWrite)
                    {
                        createdByProperty.SetValue(entry.Entity, currentUser);
                    }

                    if (createdDateProperty != null && createdDateProperty.CanWrite)
                    {
                        createdDateProperty.SetValue(entry.Entity, currentTime);
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    // Set UpdatedBy and UpdatedDate if properties exist
                    var updatedByProperty = entityType.GetProperty("UpdatedBy");
                    var updatedDateProperty = entityType.GetProperty("UpdatedDate");

                    if (updatedByProperty != null && updatedByProperty.CanWrite)
                    {
                        updatedByProperty.SetValue(entry.Entity, currentUser);
                    }

                    if (updatedDateProperty != null && updatedDateProperty.CanWrite)
                    {
                        updatedDateProperty.SetValue(entry.Entity, currentTime);
                    }

                }
            }
        }
    }

    #endregion

    #region Dispose Pattern

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _context?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

// Placeholder repository implementations - these would be fully implemented similar to ActivityRepository and BusRepository
public class DriverRepository : Repository<Driver>, IDriverRepository
{
    public DriverRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    // Implement all interface methods similar to other repositories
    public async Task<IEnumerable<Driver>> GetActiveDriversAsync() => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetAvailableDriversAsync(DateTime date, TimeSpan? startTime = null, TimeSpan? endTime = null) => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversByLicenseTypeAsync(string licenseType) => await FindAsync(d => d.DriversLicenceType == licenseType);
    public async Task<IEnumerable<Driver>> GetDriversWithCompletedTrainingAsync() => await FindAsync(d => d.TrainingComplete);
    public async Task<IEnumerable<Driver>> GetDriversWithPendingTrainingAsync() => await FindAsync(d => !d.TrainingComplete);
    public async Task<Driver?> GetDriverByPhoneAsync(string phone) => await FirstOrDefaultAsync(d => d.DriverPhone == phone);
    public async Task<Driver?> GetDriverByEmailAsync(string email) => await FirstOrDefaultAsync(d => d.DriverEmail == email);
    public async Task<IEnumerable<Driver>> GetDriversWithExpiringLicensesAsync(int withinDays = 30) => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversWithExpiredLicensesAsync() => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversRequiringMedicalExamAsync(int withinDays = 30) => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversRequiringTrainingRenewalAsync(int withinDays = 30) => await GetAllAsync();
    public async Task<bool> IsDriverAvailableAsync(int driverId, DateTime date, TimeSpan startTime, TimeSpan endTime) => await Task.FromResult(true);
    public async Task<IEnumerable<Driver>> GetDriversScheduledForDateAsync(DateTime date) => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversWithNoScheduleAsync(DateTime date) => await GetAllAsync();
    public async Task<int> GetTotalDriverCountAsync() => await CountAsync();
    public async Task<int> GetActiveDriverCountAsync() => await CountAsync();
    public async Task<Dictionary<string, int>> GetDriverCountByLicenseTypeAsync() => await Task.FromResult(new Dictionary<string, int>());
    public async Task<Dictionary<bool, int>> GetDriverCountByTrainingStatusAsync() => await Task.FromResult(new Dictionary<bool, int>());
    public async Task<IEnumerable<Driver>> GetDriversByPerformanceRatingAsync(decimal minRating) => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversWithEmergencyContactsAsync() => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversWithoutEmergencyContactsAsync() => await GetAllAsync();
    public async Task<IEnumerable<Driver>> GetDriversByLocationAsync(string city, string? state = null) => await GetAllAsync();

    // Synchronous methods
    public IEnumerable<Driver> GetActiveDrivers() => GetAll();
    public IEnumerable<Driver> GetAvailableDrivers(DateTime date, TimeSpan? startTime = null, TimeSpan? endTime = null) => GetAll();
    public IEnumerable<Driver> GetDriversByLicenseType(string licenseType) => Find(d => d.DriversLicenceType == licenseType);
    public IEnumerable<Driver> GetDriversWithCompletedTraining() => Find(d => d.TrainingComplete);
    public IEnumerable<Driver> GetDriversWithExpiringLicenses(int withinDays = 30) => GetAll();
    public bool IsDriverAvailable(int driverId, DateTime date, TimeSpan startTime, TimeSpan endTime) => true;
}

public class StudentRepository : Repository<Student>, IStudentRepository
{
    public StudentRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    // Implement all interface methods (updated for model alignment)
    public async Task<IEnumerable<Student>> GetActiveStudentsAsync() => await FindAsync(s => s.Active);
    public async Task<IEnumerable<Student>> GetStudentsByGradeAsync(string grade) => await FindAsync(s => s.Grade == grade);
    public async Task<IEnumerable<Student>> GetStudentsByRouteAsync(int? routeId)
    {
        if (routeId == null)
        {
            return new List<Student>();
        }

        string routeStr = routeId.Value.ToString();
        return await FindAsync(s => s.AMRoute == routeStr || s.PMRoute == routeStr);
    }
    public async Task<IEnumerable<Student>> GetStudentsWithoutRouteAsync() => await FindAsync(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));
    public async Task<Student?> GetStudentByNameAsync(string studentName) => await FirstOrDefaultAsync(s => s.StudentName.Contains(studentName));
    public async Task<IEnumerable<Student>> SearchStudentsByNameAsync(string searchTerm) => await FindAsync(s => s.StudentName.Contains(searchTerm));
    public async Task<IEnumerable<Student>> GetStudentsWithSpecialNeedsAsync() => await FindAsync(s => !string.IsNullOrEmpty(s.SpecialNeeds));
    public async Task<IEnumerable<Student>> GetStudentsWithMedicalConditionsAsync() => await FindAsync(s => !string.IsNullOrEmpty(s.MedicalNotes));
    public async Task<IEnumerable<Student>> GetStudentsRequiringSpecialTransportationAsync() => await FindAsync(s => !string.IsNullOrEmpty(s.SpecialNeeds) || !string.IsNullOrEmpty(s.SpecialAccommodations));
    public async Task<IEnumerable<Student>> GetStudentsWithEmergencyContactsAsync() => await FindAsync(s => !string.IsNullOrEmpty(s.EmergencyPhone));
    public async Task<IEnumerable<Student>> GetStudentsWithoutEmergencyContactsAsync() => await FindAsync(s => string.IsNullOrEmpty(s.EmergencyPhone));
    public async Task<IEnumerable<Student>> GetStudentsByTransportationTypeAsync(string transportationType) => await FindAsync(s => s.TransportationNotes != null && s.TransportationNotes.Contains(transportationType));
    public async Task<IEnumerable<Student>> GetStudentsEligibleForRouteAsync(int routeId) => await FindAsync(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString());
    public async Task<int> GetStudentCountByRouteAsync(int routeId) => await CountAsync(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString());
    public async Task<Dictionary<string, int>> GetStudentCountByRouteAsync() => await Task.FromResult(new Dictionary<string, int>());
    public async Task<int> GetTotalStudentCountAsync() => await CountAsync();
    public async Task<int> GetActiveStudentCountAsync() => await CountAsync(s => s.Active);
    public async Task<Dictionary<string, int>> GetStudentCountByGradeAsync() => await Task.FromResult(new Dictionary<string, int>());
    public async Task<Dictionary<string, int>> GetStudentCountByTransportationTypeAsync() => await Task.FromResult(new Dictionary<string, int>());
    // TODO: Age property not available on Student model - return all students for now
    public async Task<IEnumerable<Student>> GetStudentsByAgeRangeAsync(int minAge, int maxAge) => await GetAllAsync();
    public async Task<IEnumerable<Student>> GetStudentsByParentEmailAsync(string email) => await FindAsync(s => s.ParentGuardian != null && s.ParentGuardian.Contains(email));
    public async Task<IEnumerable<Student>> GetStudentsByParentPhoneAsync(string phone) => await FindAsync(s => s.HomePhone == phone || s.EmergencyPhone == phone || s.AlternativePhone == phone);
    public async Task<IEnumerable<Student>> GetStudentsWithIncompleteContactInfoAsync() => await FindAsync(s => string.IsNullOrEmpty(s.ParentGuardian) || string.IsNullOrEmpty(s.HomePhone));
    public async Task<IEnumerable<Student>> GetStudentsBySchoolAsync(string schoolName) => await FindAsync(s => s.School == schoolName);
    public async Task<IEnumerable<Student>> GetStudentsWithActivityPermissionsAsync() => await FindAsync(s => s.PhotoPermission || s.FieldTripPermission);
    public async Task<IEnumerable<Student>> GetStudentsWithoutActivityPermissionsAsync() => await FindAsync(s => !s.PhotoPermission && !s.FieldTripPermission);

    // Synchronous methods
    public IEnumerable<Student> GetActiveStudents() => Find(s => s.Active);
    public IEnumerable<Student> GetStudentsByGrade(string grade) => Find(s => s.Grade == grade);
    public IEnumerable<Student> GetStudentsByRoute(int? routeId)
    {
        if (routeId == null)
        {
            return new List<Student>();
        }

        string routeStr = routeId.Value.ToString();
        return Find(s => s.AMRoute == routeStr || s.PMRoute == routeStr);
    }
    public IEnumerable<Student> GetStudentsWithoutRoute() => Find(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));
    public IEnumerable<Student> GetStudentsWithSpecialNeeds() => Find(s => !string.IsNullOrEmpty(s.SpecialNeeds));
    public IEnumerable<Student> SearchStudentsByName(string searchTerm) => Find(s => s.StudentName.Contains(searchTerm));
    public int GetStudentCountByRoute(int routeId) => Count(s => s.AMRoute == routeId.ToString() || s.PMRoute == routeId.ToString());
}

public class FuelRepository : Repository<Fuel>, IFuelRepository
{
    public FuelRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    public async Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId) => await FindAsync(f => f.VehicleFueledId == vehicleId);
    public async Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate) => await FindAsync(f => f.FuelDate >= startDate && f.FuelDate <= endDate);
    public async Task<decimal> GetTotalFuelCostAsync(DateTime startDate, DateTime endDate) => await Task.FromResult(0m);
    public async Task<decimal> GetAverageFuelEfficiencyAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null) => await Task.FromResult(0m);
    public async Task<IEnumerable<Fuel>> GetRecentFuelRecordsAsync(int count = 10) => await GetAllAsync();

    public IEnumerable<Fuel> GetFuelRecordsByVehicle(int vehicleId) => Find(f => f.VehicleFueledId == vehicleId);
    public IEnumerable<Fuel> GetFuelRecordsByDateRange(DateTime startDate, DateTime endDate) => Find(f => f.FuelDate >= startDate && f.FuelDate <= endDate);
    public decimal GetTotalFuelCost(DateTime startDate, DateTime endDate) => 0;
}

public class MaintenanceRepository : Repository<Maintenance>, IMaintenanceRepository
{
    public MaintenanceRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByVehicleAsync(int vehicleId) => await FindAsync(m => m.VehicleId == vehicleId);
    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByDateRangeAsync(DateTime startDate, DateTime endDate) => await FindAsync(m => m.Date >= startDate && m.Date <= endDate);
    public async Task<IEnumerable<Maintenance>> GetMaintenanceRecordsByTypeAsync(string maintenanceType) => await FindAsync(m => m.MaintenanceCompleted.Contains(maintenanceType));
    public async Task<decimal> GetTotalMaintenanceCostAsync(DateTime startDate, DateTime endDate) => await Task.FromResult(0m);
    public async Task<IEnumerable<Maintenance>> GetUpcomingMaintenanceAsync(int withinDays = 30) => await GetAllAsync();
    public async Task<IEnumerable<Maintenance>> GetOverdueMaintenanceAsync() => await GetAllAsync();

    public IEnumerable<Maintenance> GetMaintenanceRecordsByVehicle(int vehicleId) => Find(m => m.VehicleId == vehicleId);
    public IEnumerable<Maintenance> GetMaintenanceRecordsByDateRange(DateTime startDate, DateTime endDate) => Find(m => m.Date >= startDate && m.Date <= endDate);
    public decimal GetTotalMaintenanceCost(DateTime startDate, DateTime endDate) => 0;
}

public class ScheduleRepository : Repository<Schedule>, IScheduleRepository
{
    public ScheduleRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    public async Task<IEnumerable<Schedule>> GetSchedulesByDateAsync(DateTime date) => await FindAsync(s => s.ScheduleDate.Date == date.Date);
    public async Task<IEnumerable<Schedule>> GetSchedulesByRouteAsync(int routeId) => await FindAsync(s => s.RouteId == routeId);
    public async Task<IEnumerable<Schedule>> GetSchedulesByBusAsync(int busId) => await FindAsync(s => s.BusId == busId);
    public async Task<IEnumerable<Schedule>> GetSchedulesByDriverAsync(int driverId) => await FindAsync(s => s.DriverId == driverId);
    public async Task<bool> HasConflictAsync(int busId, int driverId, DateTime startTime, DateTime endTime) => await Task.FromResult(false);

    public IEnumerable<Schedule> GetSchedulesByDate(DateTime date) => Find(s => s.ScheduleDate.Date == date.Date);
    public IEnumerable<Schedule> GetSchedulesByRoute(int routeId) => Find(s => s.RouteId == routeId);
    public bool HasConflict(int busId, int driverId, DateTime startTime, DateTime endTime) => false;
}

public class SchoolCalendarRepository : Repository<SchoolCalendar>, ISchoolCalendarRepository
{
    public SchoolCalendarRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    public async Task<IEnumerable<SchoolCalendar>> GetEventsByDateRangeAsync(DateTime startDate, DateTime endDate) => await FindAsync(c => c.Date >= startDate && c.Date <= endDate);
    public async Task<IEnumerable<SchoolCalendar>> GetEventsByTypeAsync(string eventType) => await FindAsync(c => c.EventType == eventType);
    public async Task<IEnumerable<SchoolCalendar>> GetSchoolDaysAsync(DateTime startDate, DateTime endDate) => await FindAsync(c => c.EventType == "School Day" && c.Date >= startDate && c.Date <= endDate);
    public async Task<IEnumerable<SchoolCalendar>> GetHolidaysAsync(DateTime startDate, DateTime endDate) => await FindAsync(c => c.EventType == "Holiday" && c.Date >= startDate && c.Date <= endDate);
    public async Task<bool> IsSchoolDayAsync(DateTime date) => await AnyAsync(c => c.Date.Date == date.Date && c.EventType == "School Day");
    public async Task<bool> AreRoutesRequiredAsync(DateTime date) => await AnyAsync(c => c.Date.Date == date.Date && c.RoutesRequired);

    public IEnumerable<SchoolCalendar> GetEventsByDateRange(DateTime startDate, DateTime endDate) => Find(c => c.Date >= startDate && c.Date <= endDate);
    public IEnumerable<SchoolCalendar> GetSchoolDays(DateTime startDate, DateTime endDate) => Find(c => c.EventType == "School Day" && c.Date >= startDate && c.Date <= endDate);
    public bool IsSchoolDay(DateTime date) => Any(c => c.Date.Date == date.Date && c.EventType == "School Day");
    public bool AreRoutesRequired(DateTime date) => Any(c => c.Date.Date == date.Date && c.RoutesRequired);
}

public class ActivityScheduleRepository : Repository<ActivitySchedule>, IActivityScheduleRepository
{
    public ActivityScheduleRepository(BusBuddyDbContext context, IUserContextService userContextService) : base(context, userContextService) { }

    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByDateAsync(DateTime date) => await FindAsync(a => a.ScheduledDate.Date == date.Date);
    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate) => await FindAsync(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate);
    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByTripTypeAsync(string tripType) => await FindAsync(a => a.TripType == tripType);
    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByVehicleAsync(int vehicleId) => await FindAsync(a => a.ScheduledVehicleId == vehicleId);
    public async Task<IEnumerable<ActivitySchedule>> GetSchedulesByDriverAsync(int driverId) => await FindAsync(a => a.ScheduledDriverId == driverId);
    public async Task<bool> HasConflictAsync(int vehicleId, int driverId, DateTime date, TimeSpan startTime, TimeSpan endTime) => await Task.FromResult(false);

    public IEnumerable<ActivitySchedule> GetSchedulesByDate(DateTime date) => Find(a => a.ScheduledDate.Date == date.Date);
    public IEnumerable<ActivitySchedule> GetSchedulesByTripType(string tripType) => Find(a => a.TripType == tripType);
    public bool HasConflict(int vehicleId, int driverId, DateTime date, TimeSpan startTime, TimeSpan endTime) => false;
}
