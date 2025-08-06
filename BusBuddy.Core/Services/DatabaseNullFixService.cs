using Serilog;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Data;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service to fix NULL values in database that cause SqlNullValueException
    /// </summary>
    public class DatabaseNullFixService
    {
        private readonly BusBuddyDbContext _context;
        private static readonly ILogger Logger = Log.ForContext<DatabaseNullFixService>();

        public DatabaseNullFixService(BusBuddyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Execute the NULL value fix migration immediately
        /// </summary>
        public async Task FixNullValuesNowAsync()
        {
            try
            {
                Logger.Information("Starting database NULL value fix...");

                var migration = new DatabaseNullFixMigration(_context);

                // Check current NULL counts
                var beforeCounts = await GetNullCountsAsync();
                Logger.Information("NULL values before fix: {Counts}", string.Join(", ", beforeCounts.Select(kv => $"{kv.Key}: {kv.Value}")));

                // Execute the fix
                await migration.FixNullValuesAsync();

                // Check after counts
                var afterCounts = await GetNullCountsAsync();
                Logger.Information("NULL values after fix: {Counts}", string.Join(", ", afterCounts.Select(kv => $"{kv.Key}: {kv.Value}")));

                Logger.Information("Database NULL value fix completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to fix NULL values in database");
                throw;
            }
        }

        /// <summary>
        /// Get count of NULL values in critical tables
        /// </summary>
        private async Task<Dictionary<string, int>> GetNullCountsAsync()
        {
            var counts = new Dictionary<string, int>();

            try
            {
                // Check Drivers NULL values
                var driversNulls = await _context.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*) as Value FROM Drivers
                    WHERE DriverName IS NULL OR DriversLicenceType IS NULL OR Status IS NULL
                ").FirstOrDefaultAsync();
                counts["Drivers"] = driversNulls;

                // Check Vehicles NULL values
                var vehiclesNulls = await _context.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*) as Value FROM Vehicles
                    WHERE BusNumber IS NULL OR Make IS NULL OR Model IS NULL OR Status IS NULL OR VINNumber IS NULL OR LicenseNumber IS NULL
                ").FirstOrDefaultAsync();
                counts["Vehicles"] = vehiclesNulls;

                // Check Routes NULL values
                var routesNulls = await _context.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*) as Value FROM Routes
                    WHERE RouteName IS NULL
                ").FirstOrDefaultAsync();
                counts["Routes"] = routesNulls;

                // Check Activities NULL values
                var activitiesNulls = await _context.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*) as Value FROM Activities
                    WHERE ActivityType IS NULL OR Destination IS NULL OR RequestedBy IS NULL OR Status IS NULL
                ").FirstOrDefaultAsync();
                counts["Activities"] = activitiesNulls;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking NULL value counts");
                // Return safe defaults
                counts = new Dictionary<string, int>
                {
                    ["Drivers"] = -1,
                    ["Vehicles"] = -1,
                    ["Routes"] = -1,
                    ["Activities"] = -1
                };
            }

            return counts;
        }

        /// <summary>
        /// Execute immediate SQL fix for Drivers table NULL values
        /// </summary>
        public async Task FixDriverNullsImmediateAsync()
        {
            try
            {
                Logger.Information("Executing immediate fix for Driver NULL values...");

                await _context.Database.ExecuteSqlRawAsync(@"
                    UPDATE Drivers
                    SET DriverName = COALESCE(NULLIF(LTRIM(RTRIM(DriverName)), ''), 'Driver-' + CAST(DriverId AS VARCHAR(10)))
                    WHERE DriverName IS NULL OR LTRIM(RTRIM(DriverName)) = '';

                    UPDATE Drivers
                    SET DriversLicenceType = COALESCE(NULLIF(LTRIM(RTRIM(DriversLicenceType)), ''), 'Standard')
                    WHERE DriversLicenceType IS NULL OR LTRIM(RTRIM(DriversLicenceType)) = '';

                    UPDATE Drivers
                    SET Status = COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), 'Active')
                    WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';
                ");

                Logger.Information("Driver NULL values fixed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to fix Driver NULL values");
                throw;
            }
        }
    }
}
