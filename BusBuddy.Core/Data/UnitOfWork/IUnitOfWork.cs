using BusBuddy.Core.Data.Interfaces;

namespace BusBuddy.Core.Data.UnitOfWork;

/// <summary>
/// Unit of Work interface for managing transactions and coordinating repositories
/// Ensures data consistency across multiple repository operations
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository properties
    IActivityRepository Activities { get; }
    IBusRepository Buses { get; }
    IDriverRepository Drivers { get; }
    IRouteRepository Routes { get; }
    IStudentRepository Students { get; }
    IFuelRepository FuelRecords { get; }
    IMaintenanceRepository MaintenanceRecords { get; }
    IScheduleRepository Schedules { get; }
    ISchoolCalendarRepository SchoolCalendar { get; }
    IActivityScheduleRepository ActivitySchedules { get; }

    // Generic repository access
    IRepository<T> Repository<T>() where T : class;

    // Transaction management
    Task<int> SaveChangesAsync();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    int SaveChanges();

    // Transaction support
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    void BeginTransaction();
    void CommitTransaction();
    void RollbackTransaction();

    // Bulk operations
    Task<int> BulkInsertAsync<T>(IEnumerable<T> entities) where T : class;
    Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities) where T : class;
    Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities) where T : class;

    // Database operations
    Task<bool> DatabaseExistsAsync();
    Task EnsureDatabaseCreatedAsync();
    Task MigrateDatabaseAsync();
    Task<bool> CanConnectAsync();

    // Audit and tracking
    void SetAuditUser(string username);
    string? GetCurrentAuditUser();
    Task<IEnumerable<T>> GetAuditTrailAsync<T>(int entityId) where T : class;

    // Cache management
    Task RefreshCacheAsync();
    void ClearCache();
}
