using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Utility class for validating database data integrity
    /// </summary>
    public class DatabaseValidator
    {
        private readonly BusBuddy.Core.Data.IBusBuddyDbContextFactory _contextFactory;
        private static readonly ILogger Logger = Log.ForContext<DatabaseValidator>();

        public DatabaseValidator(
            IBusBuddyDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <summary>
        /// Validates the database data integrity
        /// </summary>
        /// <param name="breakOnIssue">If true, will call Debugger.Break() when issues are found (only in debug mode)</param>
        /// <returns>A collection of validation issues found</returns>
        public async Task<List<string>> ValidateDatabaseDataAsync(bool breakOnIssue = false)
        {
            var issues = new List<string>();

            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Validate Vehicles table for NULL values in critical columns
                var busesWithNullValues = await context.Buses
                    .Where(v => v.BusNumber == null ||
                            v.Make == null ||
                            v.Model == null ||
                            v.LicenseNumber == null ||
                            v.VINNumber == null)
                    .ToListAsync();

                if (busesWithNullValues.Any())
                {
                    var busIdsWithNulls = string.Join(", ", busesWithNullValues.Select(b => $"{b.VehicleId} ({b.BusNumber ?? "null"})"));
                    issues.Add($"Found {busesWithNullValues.Count} buses with NULL values in critical fields: {busIdsWithNulls}");
                    Logger.Warning("Found {Count} buses with NULL values in critical fields: {BusIds}",
                        busesWithNullValues.Count, busIdsWithNulls);

                    // Break into the debugger if requested and in debug mode
                    if (breakOnIssue && Debugger.IsAttached)
                    {
                        Logger.Debug("NULL values found in buses");
                        // Debugger.Break(); // Commented out to prevent unwanted breaks
                    }
                }

                // Check for invalid foreign keys in Routes
                var routesWithInvalidVehicles = await context.Routes
                    .Where(r =>
                        (r.AMVehicleId.HasValue && !context.Buses.Any(v => v.VehicleId == r.AMVehicleId)) ||
                        (r.PMVehicleId.HasValue && !context.Buses.Any(v => v.VehicleId == r.PMVehicleId)))
                    .Select(r => r.RouteId)
                    .ToListAsync();

                if (routesWithInvalidVehicles.Any())
                {
                    issues.Add($"Found {routesWithInvalidVehicles.Count} routes with invalid vehicle references");
                    Logger.Warning("Found {Count} routes with invalid vehicle references", routesWithInvalidVehicles.Count);

                    // Break into the debugger if requested and in debug mode
                    if (breakOnIssue && Debugger.IsAttached)
                    {
                        Logger.Debug("Routes with invalid vehicle references found");
                        // Debugger.Break(); // Commented out to prevent unwanted breaks
                    }
                }

                // Check for invalid foreign keys in Drivers
                var routesWithInvalidDrivers = await context.Routes
                    .Where(r =>
                        (r.AMDriverId.HasValue && !context.Drivers.Any(d => d.DriverId == r.AMDriverId)) ||
                        (r.PMDriverId.HasValue && !context.Drivers.Any(d => d.DriverId == r.PMDriverId)))
                    .Select(r => r.RouteId)
                    .ToListAsync();

                if (routesWithInvalidDrivers.Any())
                {
                    issues.Add($"Found {routesWithInvalidDrivers.Count} routes with invalid driver references");
                    Logger.Warning("Found {Count} routes with invalid driver references", routesWithInvalidDrivers.Count);

                    // Break into the debugger if requested and in debug mode
                    if (breakOnIssue && Debugger.IsAttached)
                    {
                        Logger.Debug("Routes with invalid driver references found");
                        // Debugger.Break(); // Commented out to prevent unwanted breaks
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error validating database data");
                issues.Add($"Database validation error: {ex.Message}");

                // Break into the debugger on exception if requested and in debug mode
                if (breakOnIssue && Debugger.IsAttached)
                {
                    Logger.Debug("Exception in database validation: {ExceptionType}", ex.GetType().Name);
                    // Debugger.Break(); // Commented out to prevent unwanted breaks
                }
            }

            return issues;
        }

        /// <summary>
        /// Runs automatic fixes for common database integrity issues
        /// </summary>
        /// <param name="breakOnFix">If true, will call Debugger.Break() when fixes are applied (only in debug mode)</param>
        /// <returns>The number of issues fixed</returns>
        public async Task<int> RunAutomaticFixesAsync(bool breakOnFix = false)
        {
            int fixCount = 0;

            try
            {
                using var context = _contextFactory.CreateWriteDbContext();

                // Fix NULL values in Vehicles table
                var busesWithNullValues = await context.Buses
                    .Where(v => v.BusNumber == null ||
                            v.Make == null ||
                            v.Model == null ||
                            v.LicenseNumber == null ||
                            v.VINNumber == null ||
                            v.Status == null)
                    .ToListAsync();

                foreach (var bus in busesWithNullValues)
                {
                    bool changed = false;

                    if (bus.BusNumber == null)
                    {
                        bus.BusNumber = $"Bus-{bus.VehicleId}";
                        changed = true;
                    }

                    if (bus.Make == null)
                    {
                        bus.Make = "Unknown";
                        changed = true;
                    }

                    if (bus.Model == null)
                    {
                        bus.Model = "Unknown";
                        changed = true;
                    }

                    if (bus.LicenseNumber == null)
                    {
                        bus.LicenseNumber = $"LIC-{bus.VehicleId}";
                        changed = true;
                    }

                    if (bus.VINNumber == null)
                    {
                        bus.VINNumber = $"VIN-{bus.VehicleId}";
                        changed = true;
                    }

                    if (bus.Status == null)
                    {
                        bus.Status = "Active";
                        changed = true;
                    }

                    if (changed)
                    {
                        fixCount++;

                        // Break into the debugger for the first fix if requested and in debug mode
                        if (breakOnFix && Debugger.IsAttached && fixCount == 1)
                        {
                            Logger.Debug("Database fix applied for Bus ID {BusId}", bus.VehicleId);
                            // Debugger.Break(); // Commented out to prevent unwanted breaks
                        }
                    }
                }

                // Apply the changes to the database
                if (fixCount > 0)
                {
                    await context.SaveChangesAsync();
                    Logger.Information("Fixed {Count} buses with NULL values", fixCount);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error running automatic fixes for database integrity");

                // Break into the debugger on exception if in debug mode
                if (Debugger.IsAttached)
                {
                    Logger.Debug("Exception in database fixes: {ExceptionType}", ex.GetType().Name);
                    // Debugger.Break(); // Commented out to prevent unwanted breaks
                }
            }

            return fixCount;
        }
    }
}
