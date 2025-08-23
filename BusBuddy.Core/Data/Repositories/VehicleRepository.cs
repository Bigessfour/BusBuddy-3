using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Data.Repositories
{
    /// <summary>
    /// Thread-safe repository for Vehicle (Bus) entities with optimized query patterns
    /// </summary>
    public class VehicleRepository : Repository<Bus>, IVehicleRepository
    {
        private readonly IBusBuddyDbContextFactory ContextFactory;
        private static readonly ILogger Logger = Log.ForContext<VehicleRepository>();

        public VehicleRepository(
            BusBuddyDbContext context,
            IUserContextService userContextService,
            IBusBuddyDbContextFactory contextFactory)
            : base(context, userContextService)
        {
            ContextFactory = contextFactory;
        }

        /// <summary>
        /// Get all vehicles with their routes using split queries to prevent cartesian explosion
        /// Each thread gets its own context for thread safety
        /// </summary>
        public async Task<IEnumerable<Bus>> GetAllWithRoutesAsync()
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                // Use AsSplitQuery to avoid the cartesian explosion problem
                return await context.Set<Bus>()
                    .Include(v => v.AMRoutes)
                    .Include(v => v.PMRoutes)
                    .AsSplitQuery() // This is the key to avoiding the performance issue
                    .AsNoTracking() // Improve performance for read-only operations
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading vehicles with routes");
                throw;
            }
        }

        /// <summary>
        /// Get a vehicle with all related data using split queries
        /// </summary>
        public async Task<Bus?> GetVehicleWithDetailsAsync(int vehicleId)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                // Use AsSplitQuery to avoid the cartesian explosion problem
                return await context.Set<Bus>()
                    .Where(v => v.BusId == vehicleId)
                    .Include(v => v.FuelRecords)
                    .Include(v => v.MaintenanceRecords)
                    .Include(v => v.Activities)
                    .Include(v => v.AMRoutes)
                    .Include(v => v.PMRoutes)
                    .Include(v => v.ScheduledActivities)
                    .AsSplitQuery() // This is the key to avoiding the performance issue
                    .AsNoTracking() // Improve performance for read-only operations
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading vehicle with details for ID {VehicleId}", vehicleId);
                throw;
            }
        }

        /// <summary>
        /// Gets vehicles matching specific criteria
        /// </summary>
        public async Task<IEnumerable<Bus>> GetActiveVehiclesAsync()
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                return await context.Set<Bus>()
                    .Where(v => v.Status == "Active")
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading active vehicles");
                throw;
            }
        }

        /// <summary>
        /// Gets vehicles with maintenance records that are due for service
        /// </summary>
        public async Task<IEnumerable<Bus>> GetVehiclesDueForMaintenanceAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                using var context = ContextFactory.CreateDbContext();

                return await context.Set<Bus>()
                    .Where(v => v.Status == "Active" && v.NextMaintenanceDue != null && v.NextMaintenanceDue <= now)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading vehicles due for maintenance");
                throw;
            }
        }
    }

    /// <summary>
    /// Interface for the Vehicle repository with specialized methods
    /// </summary>
    public interface IVehicleRepository : IRepository<Bus>
    {
        Task<IEnumerable<Bus>> GetAllWithRoutesAsync();
        Task<Bus?> GetVehicleWithDetailsAsync(int vehicleId);
        Task<IEnumerable<Bus>> GetActiveVehiclesAsync();
        Task<IEnumerable<Bus>> GetVehiclesDueForMaintenanceAsync();
    }
}
