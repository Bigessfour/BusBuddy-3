using BusBuddy.Core.Data;
using BusBuddy.Core.Extensions;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using System.Diagnostics;
using System.Globalization;
namespace BusBuddy.Core.Services
{
    [DebuggerDisplay("BusService - Cache: {_cacheService != null}")]
    public class BusService : IBusService
    {
        private static readonly ILogger Logger = Log.ForContext<BusService>();
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IBusCachingService _cacheService;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        // Removed unused lists that were previously used for sample data fallback

        public BusService(
            IBusBuddyDbContextFactory contextFactory,
            IBusCachingService cacheService)
        {
            _contextFactory = contextFactory;
            _cacheService = cacheService;

            // No sample data initialization - all data comes from the database
            // with proper error handling
        }

        // Entity Framework methods for actual database operations using caching
        public async Task<List<Bus>> GetAllBusEntitiesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                using (LogContext.PushProperty("QueryType", "GetAllBusEntities"))
                using (LogContext.PushProperty("OperationName", "DatabaseQuery"))
                {
                    var stopwatch = Stopwatch.StartNew();
                    Logger.Information("Retrieving all bus entities (with caching)");

                    try
                    {
                        var result = await _cacheService.GetAllBusesAsync(async () =>
                        {
                            Logger.Information("Cache miss - retrieving all bus entities from database");
                            // Create a fresh context to avoid concurrency issues
                            var context = _contextFactory.CreateDbContext();
                            try
                            {
                                // Use projection to handle NULL values safely
                                // Debug.Assert to help find issues during debugging
                                Debug.Assert(context != null, "DbContext is null");
                                Debug.Assert(context.Buses != null, "Buses DbSet is null");

                                return await context.Buses
                                    .AsNoTracking() // Use AsNoTracking for better performance in read operations
                                    .Select(v => new Bus
                                    {
                                        BusId = v.BusId,
                                        BusNumber = v.BusNumber ?? string.Empty,
                                        Year = v.Year,
                                        Make = v.Make ?? string.Empty,
                                        Model = v.Model ?? string.Empty,
                                        SeatingCapacity = v.SeatingCapacity,
                                        VINNumber = v.VINNumber ?? string.Empty,
                                        LicenseNumber = v.LicenseNumber ?? string.Empty,
                                        DateLastInspection = v.DateLastInspection,
                                        CurrentOdometer = v.CurrentOdometer,
                                        Status = v.Status ?? "Active",
                                        Department = v.Department,
                                        FleetType = v.FleetType,
                                        FuelCapacity = v.FuelCapacity,
                                        FuelType = v.FuelType,
                                        MilesPerGallon = v.MilesPerGallon,
                                        NextMaintenanceDue = v.NextMaintenanceDue,
                                        NextMaintenanceMileage = v.NextMaintenanceMileage,
                                        LastServiceDate = v.LastServiceDate,
                                        SpecialEquipment = v.SpecialEquipment,
                                        GPSTracking = v.GPSTracking,
                                        GPSDeviceId = v.GPSDeviceId,
                                        Notes = v.Notes
                                        // Fleet grid does not edit routes — omit AMRoutes/PMRoutes
                                        // so Save never walks a Route graph (UTM EF identity conflict).
                                    })
                                    .ToListAsync();
                            }
                            finally
                            {
                                // Properly dispose the context when done
                                await context.DisposeAsync();
                            }
                        });

                        stopwatch.Stop();
                        Logger.Information("Retrieved {BusCount} bus entities in {Duration}ms",
                            result.Count, stopwatch.ElapsedMilliseconds);

                        return result;
                    }
                    catch (System.Data.SqlTypes.SqlNullValueException ex)
                    {
                        stopwatch.Stop();
                        Logger.Warning(ex, "SQL NULL value error when retrieving buses. Returning empty list to avoid application failure.");
                        return new List<Bus>();
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving all bus entities after {Duration}ms",
                            stopwatch.ElapsedMilliseconds);

                        // If we're debugging, break into the debugger for SqlNullValueException
                        if (Debugger.IsAttached && ex.ToString().Contains("SqlNullValueException"))
                        {
                            Logger.Debug("Breaking into debugger due to SqlNullValueException");
                            Debugger.Break();
                        }

                        throw; // Propagate exception to caller - no fallback to sample data
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<(List<Bus> Buses, int TotalCount)> GetBusesPaginatedAsync(int pageNumber, int pageSize, string? sortColumn = null, bool isAscending = true)
        {
            using (LogContext.PushProperty("QueryType", "GetBusesPaginated"))
            using (LogContext.PushProperty("OperationName", "DatabaseQuery"))
            using (LogContext.PushProperty("PageNumber", pageNumber))
            using (LogContext.PushProperty("PageSize", pageSize))
            using (LogContext.PushProperty("SortColumn", sortColumn ?? "BusNumber"))
            using (LogContext.PushProperty("SortDirection", isAscending ? "Ascending" : "Descending"))
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.Information("Retrieving paginated bus entities (page {PageNumber}, size {PageSize})", pageNumber, pageSize);

                try
                {
                    using var context = _contextFactory.CreateDbContext();

                    // Get total count for pagination
                    var totalCount = await context.Buses.CountAsync();

                    // Start with base query
                    var query = context.Buses.AsNoTracking();

                    // Apply sorting
                    if (!string.IsNullOrEmpty(sortColumn))
                    {
                        // Apply ordering based on the column name
                        query = sortColumn.ToLower(CultureInfo.InvariantCulture) switch
                        {
                            "busnumber" => isAscending
                                ? query.OrderBy(v => v.BusNumber)
                                : query.OrderByDescending(v => v.BusNumber),
                            "year" => isAscending
                                ? query.OrderBy(v => v.Year)
                                : query.OrderByDescending(v => v.Year),
                            "make" => isAscending
                                ? query.OrderBy(v => v.Make)
                                : query.OrderByDescending(v => v.Make),
                            "model" => isAscending
                                ? query.OrderBy(v => v.Model)
                                : query.OrderByDescending(v => v.Model),
                            "seatingcapacity" => isAscending
                                ? query.OrderBy(v => v.SeatingCapacity)
                                : query.OrderByDescending(v => v.SeatingCapacity),
                            "status" => isAscending
                                ? query.OrderBy(v => v.Status)
                                : query.OrderByDescending(v => v.Status),
                            "datelastinspection" => isAscending
                                ? query.OrderBy(v => v.DateLastInspection)
                                : query.OrderByDescending(v => v.DateLastInspection),
                            "currentodometer" => isAscending
                                ? query.OrderBy(v => v.CurrentOdometer)
                                : query.OrderByDescending(v => v.CurrentOdometer),
                            _ => isAscending
                                ? query.OrderBy(v => v.BusNumber)
                                : query.OrderByDescending(v => v.BusNumber), // Default sort by BusNumber
                        };
                    }
                    else
                    {
                        // Default sorting by BusNumber if no sort column specified
                        query = isAscending
                            ? query.OrderBy(v => v.BusNumber)
                            : query.OrderByDescending(v => v.BusNumber);
                    }

                    // Apply pagination using Skip/Take
                    var buses = await query
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    stopwatch.Stop();
                    Logger.Information("Retrieved {BusCount} of {TotalCount} bus entities in {Duration}ms (page {PageNumber})",
                        buses.Count, totalCount, stopwatch.ElapsedMilliseconds, pageNumber);

                    return (buses, totalCount);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving paginated bus entities after {Duration}ms",
                        stopwatch.ElapsedMilliseconds);
                    throw; // Propagate the exception to the caller
                }
            }
        }

        public async Task<Bus?> GetBusEntityByIdAsync(int busId)
        {
            using (LogContext.PushProperty("QueryType", "GetBusEntityById"))
            using (LogContext.PushProperty("BusId", busId))
            using (LogContext.PushProperty("OperationName", "DatabaseQuery"))
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.Information("Retrieving bus entity with ID: {BusId} (with caching)", busId);

                try
                {
                    var result = await _cacheService.GetBusByIdAsync(busId, async (id) =>
                    {
                        Logger.Information("Cache miss - retrieving bus entity with ID: {BusId} from database", id);
                        // Create a fresh context to avoid concurrency issues
                        var context = _contextFactory.CreateDbContext();
                        try
                        {
                            return await context.Buses
                                .AsNoTracking()
                                // Fleet edit needs scalars only — Hop 4 owns route vehicle FKs.
                                // Including AMRoutes/PMRoutes caused EF identity conflicts on Update.
                                .FirstOrDefaultAsync(v => v.BusId == id);
                        }
                        finally
                        {
                            // Properly dispose the context when done
                            await context.DisposeAsync();
                        }
                    });

                    stopwatch.Stop();
                    if (result != null)
                    {
                        Logger.Information("Retrieved bus entity {BusId} in {Duration}ms",
                            busId, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        Logger.Warning("Bus entity {BusId} not found after {Duration}ms",
                            busId, stopwatch.ElapsedMilliseconds);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving bus entity {BusId} after {Duration}ms",
                        busId, stopwatch.ElapsedMilliseconds);
                    throw; // Propagate exception to caller - no fallback to sample data
                }
            }
        }

        public async Task<Bus> AddBusEntityAsync(Bus bus)
        {
            using (LogContext.PushProperty("OperationType", "AddBusEntity"))
            using (LogContext.PushProperty("BusNumber", bus.BusNumber))
            {
                Logger.Information("Adding new bus entity: {BusNumber}", bus.BusNumber);

                using var context = _contextFactory.CreateWriteDbContext();
                context.Buses.Add(bus);

                using (LogContext.PushProperty("OperationName", "AddBus"))
                using (LogContext.PushProperty("DatabaseOperation", true))
                using (LogContext.PushProperty("BusNumber", bus.BusNumber))
                {
                    var stopwatch = Stopwatch.StartNew();
                    Logger.Debug("Starting database operation: AddBus");

                    try
                    {
                        var result = await context.SaveChangesAsync();
                        stopwatch.Stop();

                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        using (LogContext.PushProperty("ChangedEntities", result))
                        {
                            Logger.Information("Database operation AddBus completed in {Duration}ms. Changed {ChangedEntities} entities.",
                                stopwatch.ElapsedMilliseconds, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        {
                            DatabaseUserMessage.LogFailure(Logger, ex, "Database operation AddBus failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                        }
                        throw;
                    }
                }

                _cacheService.InvalidateAllBusCache();

                Logger.Information("Successfully added bus: {BusNumber} with ID {BusId}",
                    bus.BusNumber, bus.BusId);

                return bus;
            }
        }

        public async Task<bool> UpdateBusEntityAsync(Bus bus)
        {
            using (LogContext.PushProperty("OperationType", "UpdateBusEntity"))
            using (LogContext.PushProperty("BusId", bus.BusId))
            using (LogContext.PushProperty("BusNumber", bus.BusNumber))
            {
                Logger.Information("Updating bus entity with ID: {BusId}, Number: {BusNumber}",
                    bus.BusId, bus.BusNumber);

                using var context = _contextFactory.CreateWriteDbContext();
                // Never Attach/Update the full graph — loaded Bus often carries Route/Schedule
                // navigations that collide with already-tracked Route rows (UTM log 2026-09-11).
                var existing = await context.Buses.FindAsync(bus.BusId);
                if (existing is null)
                {
                    Logger.Warning("Bus with ID: {BusId} not found for update", bus.BusId);
                    return false;
                }

                context.Entry(existing).CurrentValues.SetValues(bus);
                // Preserve key / identity; SetValues may overwrite BusId with same value (OK).

                using (LogContext.PushProperty("OperationName", "UpdateBus"))
                using (LogContext.PushProperty("DatabaseOperation", true))
                using (LogContext.PushProperty("BusId", bus.BusId))
                {
                    var stopwatch = Stopwatch.StartNew();
                    Logger.Debug("Starting database operation: UpdateBus");

                    try
                    {
                        var result = await context.SaveChangesAsync();
                        stopwatch.Stop();

                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        using (LogContext.PushProperty("ChangedEntities", result))
                        {
                            Logger.Information("Database operation UpdateBus completed in {Duration}ms. Changed {ChangedEntities} entities.",
                                stopwatch.ElapsedMilliseconds, result);
                        }

                        _cacheService.InvalidateAllBusCache();

                        if (result > 0)
                        {
                            Logger.Information("Successfully updated bus with ID: {BusId}", bus.BusId);
                            return true;
                        }
                        else
                        {
                            Logger.Warning("No changes detected when updating bus with ID: {BusId}", bus.BusId);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                        {
                            DatabaseUserMessage.LogFailure(Logger, ex, "Database operation UpdateBus failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                        }
                        throw;
                    }
                }
            }
        }

        public async Task<bool> DeleteBusEntityAsync(int busId)
        {
            using (LogContext.PushProperty("OperationType", "DeleteBusEntity"))
            using (LogContext.PushProperty("BusId", busId))
            {
                Logger.Information("Deleting or soft-retiring bus entity with ID: {BusId}", busId);

                using var context = _contextFactory.CreateWriteDbContext();
                var bus = await context.Buses.FindAsync(busId);
                if (bus is null)
                {
                    Logger.Warning("Bus with ID: {BusId} not found for deletion", busId);
                    return false;
                }

                using (LogContext.PushProperty("BusNumber", bus.BusNumber))
                {
                    // Restrict FKs: Routes AM/PM, Fuel, Maintenance, trip loans — never cascade-delete routes.
                    var assignedRoutes = await context.Routes
                        .Where(r => r.AMVehicleId == busId || r.PMVehicleId == busId)
                        .Select(r => new { r.RouteId, r.RouteName, r.Date, r.AMVehicleId, r.PMVehicleId, r.BusNumber })
                        .ToListAsync();
                    var hasFuel = await context.FuelRecords.AnyAsync(f => f.VehicleFueledId == busId);
                    var hasMaintenance = await context.MaintenanceRecords.AnyAsync(m => m.VehicleId == busId);
                    var hasTrips = await context.TripEvents.AnyAsync(t => t.VehicleId == busId);

                    var hasBlockingFks = assignedRoutes.Count > 0 || hasFuel || hasMaintenance || hasTrips;
                    if (!hasBlockingFks)
                    {
                        context.Buses.Remove(bus);
                        using (LogContext.PushProperty("OperationName", "HardDeleteBus"))
                        using (LogContext.PushProperty("DatabaseOperation", true))
                        {
                            var stopwatch = Stopwatch.StartNew();
                            try
                            {
                                await context.SaveChangesAsync();
                                stopwatch.Stop();
                                Logger.Information(
                                    "Hard-deleted bus {BusId} in {Duration}ms (no Restrict FKs)",
                                    busId,
                                    stopwatch.ElapsedMilliseconds);
                                _cacheService.InvalidateBusCache(busId);
                                _cacheService.InvalidateAllBusCache();
                                return true;
                            }
                            catch (Exception ex)
                            {
                                stopwatch.Stop();
                                DatabaseUserMessage.LogFailure(
                                    Logger,
                                    ex,
                                    "Database operation HardDeleteBus failed after {Duration}ms",
                                    stopwatch.ElapsedMilliseconds);
                                throw;
                            }
                        }
                    }

                    // Soft-retire — clear vehicle FKs on today/future route days only (keep history).
                    var cutoffDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                    var futureRoutes = await context.Routes
                        .Where(r =>
                            r.Date >= cutoffDate
                            && (r.AMVehicleId == busId || r.PMVehicleId == busId))
                        .ToListAsync();

                    foreach (var route in futureRoutes)
                    {
                        if (route.AMVehicleId == busId)
                        {
                            route.AMVehicleId = null;
                            context.Entry(route).Property(r => r.AMVehicleId).IsModified = true;
                        }

                        if (route.PMVehicleId == busId)
                        {
                            route.PMVehicleId = null;
                            context.Entry(route).Property(r => r.PMVehicleId).IsModified = true;
                        }

                        if (string.Equals(route.BusNumber, bus.BusNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            route.BusNumber = string.Empty;
                            context.Entry(route).Property(r => r.BusNumber).IsModified = true;
                        }
                    }

                    var routeLabels = assignedRoutes
                        .Select(r => string.IsNullOrWhiteSpace(r.RouteName) ? $"Route {r.RouteId}" : r.RouteName!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .ToList();
                    var routeSummary = routeLabels.Count > 0
                        ? string.Join(", ", routeLabels)
                        : "(fuel/maintenance/trip history)";

                    Logger.Warning(
                        "Soft-retiring bus {BusId} — Restrict FKs present (routes={RouteCount}, fuel={HasFuel}, maint={HasMaint}, trips={HasTrips}). Labels={Labels}",
                        busId,
                        assignedRoutes.Count,
                        hasFuel,
                        hasMaintenance,
                        hasTrips,
                        routeSummary);

                    bus.Status = "Retired";
                    bus.UpdatedDate = DateTime.UtcNow;
                    context.Entry(bus).Property(b => b.Status).IsModified = true;
                    context.Entry(bus).Property(b => b.UpdatedDate).IsModified = true;

                    using (LogContext.PushProperty("OperationName", "SoftRetireBus"))
                    using (LogContext.PushProperty("DatabaseOperation", true))
                    {
                        var stopwatch = Stopwatch.StartNew();
                        try
                        {
                            var result = await context.SaveChangesAsync();
                            stopwatch.Stop();
                            Logger.Information(
                                "Soft-retired bus {BusId} in {Duration}ms (changed {Changed})",
                                busId,
                                stopwatch.ElapsedMilliseconds,
                                result);

                            _cacheService.InvalidateBusCache(busId);
                            _cacheService.InvalidateAllBusCache();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();
                            DatabaseUserMessage.LogFailure(
                                Logger,
                                ex,
                                "Database operation SoftRetireBus failed after {Duration}ms",
                                stopwatch.ElapsedMilliseconds);
                            throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validates VIN format and uniqueness in the database (migrated from legacy VehicleService)
        /// </summary>
        public async Task<bool> ValidateVinAsync(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
            {
                Logger.Warning("VIN validation failed: invalid length or empty");
                return false;
            }

            using var context = _contextFactory.CreateDbContext();
            var exists = await context.Buses.AnyAsync(v => v.VINNumber == vin);
            if (exists)
            {
                Logger.Warning("VIN validation failed: duplicate VIN {VIN}", vin);
                return false;
            }
            return true;
        }

        #region IBusService Implementation

        [DebuggerStepThrough]
        public async Task<IEnumerable<Bus>> GetAllBusesAsync()
        {
            using (LogContext.PushProperty("QueryType", "GetAllBuses"))
            {
                Logger.Information("Retrieving all buses");

                try
                {
                    var buses = await GetAllBusEntitiesAsync();
                    return buses;
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to retrieve all buses");
                    throw;
                }
            }
        }

        public Task<Bus?> GetBusByIdAsync(int busId) => GetBusEntityByIdAsync(busId);

        [DebuggerStepThrough]

        public async Task<Bus> AddBusAsync(Bus bus)
        {
            using (LogContext.PushProperty("QueryType", "AddBus"))
            {
                Logger.Information("Adding new bus: {BusNumber}", bus.BusNumber);

                try
                {
                    return await AddBusEntityAsync(bus);
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to add bus: {BusNumber}", bus.BusNumber);
                    throw;
                }
            }
        }

        public async Task<bool> UpdateBusAsync(Bus bus)
        {
            using (LogContext.PushProperty("QueryType", "UpdateBus"))
            {
                Logger.Information("Updating bus with ID: {BusId}", bus.BusId);

                try
                {
                    return await UpdateBusEntityAsync(bus);
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to update bus with ID: {BusId}", bus.BusId);
                    throw;
                }
            }
        }

        public async Task<bool> DeleteBusAsync(int busId)
        {
            using (LogContext.PushProperty("QueryType", "DeleteBus"))
            {
                Logger.Information("Deleting bus with ID: {BusId}", busId);

                try
                {
                    return await DeleteBusEntityAsync(busId);
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to delete bus with ID: {BusId}", busId);
                    throw;
                }
            }
        }

        public async Task<IEnumerable<Bus>> GetActiveBusesAsync()
        {
            using (LogContext.PushProperty("QueryType", "GetActiveBuses"))
            {
                Logger.Information("Retrieving active buses");

                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    return await context.Buses
                        .AsNoTracking()
                        .Where(b => b.Status == "Active")
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to retrieve active buses");
                    throw;
                }
            }
        }

        public async Task<IEnumerable<Bus>> GetBusesByStatusAsync(string status)
        {
            using (LogContext.PushProperty("QueryType", "GetBusesByStatus"))
            using (LogContext.PushProperty("Status", status))
            {
                Logger.Information("Retrieving buses with status: {Status}", status);

                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    return await context.Buses
                        .AsNoTracking()
                        .Where(b => b.Status == status)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to retrieve buses with status: {Status}", status);
                    throw;
                }
            }
        }

        public async Task<IEnumerable<Bus>> GetBusesByTypeAsync(string type)
        {
            using (LogContext.PushProperty("QueryType", "GetBusesByType"))
            using (LogContext.PushProperty("Type", type))
            {
                Logger.Information("Retrieving buses with type: {Type}", type);

                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    return await context.Buses
                        .AsNoTracking()
                        .Where(b => b.FleetType == type)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to retrieve buses with type: {Type}", type);
                    throw;
                }
            }
        }

        public async Task<IEnumerable<Bus>> SearchBusesAsync(string searchTerm)
        {
            using (LogContext.PushProperty("QueryType", "SearchBuses"))
            using (LogContext.PushProperty("SearchTerm", searchTerm))
            {
                Logger.Information("Searching buses with term: {SearchTerm}", searchTerm);

                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    return await context.Buses
                        .AsNoTracking()
                        .Where(b =>
                            b.BusNumber.Contains(searchTerm) ||
                            b.Make.Contains(searchTerm) ||
                            b.Model.Contains(searchTerm) ||
                            b.LicenseNumber.Contains(searchTerm) ||
                            (b.FleetType != null && b.FleetType.Contains(searchTerm)))
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Failed to search buses with term: {SearchTerm}", searchTerm);
                    throw;
                }
            }
        }

        #endregion

        #region Legacy Methods

        // These methods are kept for backward compatibility
        // They should be deprecated in favor of the new interface methods

        public async Task<List<Bus>> GetBusListAsync()
        {
            using (LogContext.PushProperty("QueryType", "GetBusList"))
            using (LogContext.PushProperty("LegacyMethod", true))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    Logger.Information("Retrieving all buses (legacy method - using projection)");

                    try
                    {
                        // Create a fresh context for this operation
                        var context = _contextFactory.CreateDbContext();
                        try
                        {
                            // Use projection to select only the fields we need
                            var result = await context.Buses
                                .AsNoTracking()
                                .Select(b => new Bus
                                {
                                    BusId = b.BusId,
                                    BusNumber = b.BusNumber,
                                    Model = b.Make + " " + b.Model,
                                    Capacity = b.SeatingCapacity,
                                    Status = b.Status,
                                    DateLastInspection = b.DateLastInspection
                                })
                                .ToListAsync();

                            Logger.Information("Retrieved {BusCount} buses using projection", result.Count);

                            stopwatch.Stop();
                            Logger.Information("GetBusList_Legacy completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
                            return result;
                        }
                        finally
                        {
                            // Properly dispose the context when done
                            await context.DisposeAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        DatabaseUserMessage.LogFailure(Logger, ex, "Failed to retrieve buses from database");
                        throw; // Notify caller instead of using sample data
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    DatabaseUserMessage.LogFailure(Logger, ex, "GetBusList_Legacy failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
        }


        #endregion

        public async Task<int> GetAssignedStudentCountAsync(BusBuddyDbContext context, int busId)
        {
            return await context.Students.CountAsync(s => s.RouteAssignmentId != null &&
                context.RouteAssignments.Any(ra => ra.RouteAssignmentId == s.RouteAssignmentId && ra.VehicleId == busId));
        }

        // (Removed duplicate legacy ValidateVinAsync implementation)

    }
}
