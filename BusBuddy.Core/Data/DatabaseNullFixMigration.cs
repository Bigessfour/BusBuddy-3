using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BusBuddy.Core.Data
{
    /// <summary>
    /// Migration to fix NULL values in required string columns that cause SqlNullValueException
    /// This addresses the issue where Entity Framework cannot read NULL values into non-nullable string properties
    /// </summary>
    public class DatabaseNullFixMigration
    {
        private readonly BusBuddyDbContext _context;

        public DatabaseNullFixMigration(BusBuddyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Fix all NULL values in required string columns to prevent SqlNullValueException
        /// </summary>
        public async Task FixNullValuesAsync()
        {
            try
            {
                await FixDriverNullValues();
                await FixBusNullValues();
                await FixRouteNullValues();
                await FixActivityNullValues();
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to fix NULL values in database", ex);
            }
        }

        /// <summary>
        /// Fix NULL values in the Drivers table
        /// </summary>
        private async Task FixDriverNullValues()
        {
            // Execute raw SQL to update NULL values directly
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

                UPDATE Drivers
                SET FirstName = COALESCE(NULLIF(LTRIM(RTRIM(FirstName)), ''), NULL)
                WHERE FirstName = '';

                UPDATE Drivers
                SET LastName = COALESCE(NULLIF(LTRIM(RTRIM(LastName)), ''), NULL)
                WHERE LastName = '';

                UPDATE Drivers
                SET DriverPhone = COALESCE(NULLIF(LTRIM(RTRIM(DriverPhone)), ''), NULL)
                WHERE DriverPhone = '';

                UPDATE Drivers
                SET DriverEmail = COALESCE(NULLIF(LTRIM(RTRIM(DriverEmail)), ''), NULL)
                WHERE DriverEmail = '';

                UPDATE Drivers
                SET Address = COALESCE(NULLIF(LTRIM(RTRIM(Address)), ''), NULL)
                WHERE Address = '';

                UPDATE Drivers
                SET City = COALESCE(NULLIF(LTRIM(RTRIM(City)), ''), NULL)
                WHERE City = '';

                UPDATE Drivers
                SET State = COALESCE(NULLIF(LTRIM(RTRIM(State)), ''), NULL)
                WHERE State = '';

                UPDATE Drivers
                SET Zip = COALESCE(NULLIF(LTRIM(RTRIM(Zip)), ''), NULL)
                WHERE Zip = '';
            ");
        }

        /// <summary>
        /// Fix NULL values in the Vehicles table
        /// </summary>
        private async Task FixBusNullValues()
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE Vehicles
                SET BusNumber = COALESCE(NULLIF(LTRIM(RTRIM(BusNumber)), ''), 'BUS-' + CAST(VehicleId AS VARCHAR(10)))
                WHERE BusNumber IS NULL OR LTRIM(RTRIM(BusNumber)) = '';

                UPDATE Vehicles
                SET Make = COALESCE(NULLIF(LTRIM(RTRIM(Make)), ''), 'Unknown')
                WHERE Make IS NULL OR LTRIM(RTRIM(Make)) = '';

                UPDATE Vehicles
                SET Model = COALESCE(NULLIF(LTRIM(RTRIM(Model)), ''), 'Unknown')
                WHERE Model IS NULL OR LTRIM(RTRIM(Model)) = '';

                UPDATE Vehicles
                SET Status = COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), 'Active')
                WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';

                UPDATE Vehicles
                SET VINNumber = COALESCE(NULLIF(LTRIM(RTRIM(VINNumber)), ''), 'VIN' + CAST(VehicleId AS VARCHAR(10)) + '0000000000')
                WHERE VINNumber IS NULL OR LTRIM(RTRIM(VINNumber)) = '';

                UPDATE Vehicles
                SET LicenseNumber = COALESCE(NULLIF(LTRIM(RTRIM(LicenseNumber)), ''), 'LIC-' + CAST(VehicleId AS VARCHAR(10)))
                WHERE LicenseNumber IS NULL OR LTRIM(RTRIM(LicenseNumber)) = '';
            ");
        }

        /// <summary>
        /// Fix NULL values in the Routes table
        /// </summary>
        private async Task FixRouteNullValues()
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE Routes
                SET RouteName = COALESCE(NULLIF(LTRIM(RTRIM(RouteName)), ''), 'Route-' + CAST(RouteId AS VARCHAR(10)))
                WHERE RouteName IS NULL OR LTRIM(RTRIM(RouteName)) = '';

                UPDATE Routes
                SET Description = COALESCE(NULLIF(LTRIM(RTRIM(Description)), ''), NULL)
                WHERE Description = '';

                UPDATE Routes
                SET BusNumber = COALESCE(NULLIF(LTRIM(RTRIM(BusNumber)), ''), NULL)
                WHERE BusNumber = '';

                UPDATE Routes
                SET DriverName = COALESCE(NULLIF(LTRIM(RTRIM(DriverName)), ''), NULL)
                WHERE DriverName = '';
            ");
        }

        /// <summary>
        /// Fix NULL values in the Activities table
        /// </summary>
        private async Task FixActivityNullValues()
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE Activities
                SET ActivityType = COALESCE(NULLIF(LTRIM(RTRIM(ActivityType)), ''), 'General')
                WHERE ActivityType IS NULL OR LTRIM(RTRIM(ActivityType)) = '';

                UPDATE Activities
                SET Destination = COALESCE(NULLIF(LTRIM(RTRIM(Destination)), ''), 'Unspecified')
                WHERE Destination IS NULL OR LTRIM(RTRIM(Destination)) = '';

                UPDATE Activities
                SET RequestedBy = COALESCE(NULLIF(LTRIM(RTRIM(RequestedBy)), ''), 'System')
                WHERE RequestedBy IS NULL OR LTRIM(RTRIM(RequestedBy)) = '';

                UPDATE Activities
                SET Status = COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), 'Scheduled')
                WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';

                UPDATE Activities
                SET Description = COALESCE(NULLIF(LTRIM(RTRIM(Description)), ''), 'Activity')
                WHERE Description IS NULL OR LTRIM(RTRIM(Description)) = '';
            ");
        }

        /// <summary>
        /// Check if any required string columns still contain NULL values
        /// </summary>
        public async Task<Dictionary<string, int>> GetNullValueCountsAsync()
        {
            var nullCounts = new Dictionary<string, int>();

            // Check Drivers table
            var driversWithNulls = await _context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM Drivers
                WHERE DriverName IS NULL OR DriversLicenceType IS NULL OR Status IS NULL
            ");

            // Check Vehicles table
            var vehiclesWithNulls = await _context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM Vehicles
                WHERE BusNumber IS NULL OR Make IS NULL OR Model IS NULL OR Status IS NULL OR VINNumber IS NULL OR LicenseNumber IS NULL
            ");

            // Check Routes table
            var routesWithNulls = await _context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM Routes
                WHERE RouteName IS NULL
            ");

            // Check Activities table
            var activitiesWithNulls = await _context.Database.ExecuteSqlRawAsync(@"
                SELECT COUNT(*) FROM Activities
                WHERE ActivityType IS NULL OR Destination IS NULL OR RequestedBy IS NULL OR Status IS NULL
            ");

            return new Dictionary<string, int>
            {
                ["Drivers"] = driversWithNulls,
                ["Vehicles"] = vehiclesWithNulls,
                ["Routes"] = routesWithNulls,
                ["Activities"] = activitiesWithNulls
            };
        }
    }
}
