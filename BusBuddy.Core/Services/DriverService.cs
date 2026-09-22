using System.Text;
using System.Text.RegularExpressions;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service implementation for managing school bus drivers
    /// Provides CRUD operations and business logic for driver management, including route assignments
    /// </summary>
    public class DriverService : IDriverService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private static readonly ILogger Logger = Log.ForContext<DriverService>();
        private readonly IEnhancedCachingService _cachingService;
        private readonly IRouteService _routeService;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static bool _nullValuesFixed;

        public DriverService(
            IBusBuddyDbContextFactory contextFactory,
            IEnhancedCachingService cachingService,
            IRouteService? routeService = null)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(cachingService);

            _contextFactory = contextFactory;
            _cachingService = cachingService;
            // No null persist fallback: leftover bool-assign always goes through RouteService.
            _routeService = routeService ?? new RouteService(contextFactory);
        }

        // Context helpers: factories create short-lived contexts — always dispose.
        private static (BusBuddyDbContext Ctx, bool Dispose) GetReadContext(IBusBuddyDbContextFactory factory)
        {
            return (factory.CreateDbContext(), true);
        }

        private static (BusBuddyDbContext Ctx, bool Dispose) GetWriteContext(IBusBuddyDbContextFactory factory)
        {
            return (factory.CreateWriteDbContext(), true);
        }

        private (BusBuddyDbContext Ctx, bool Dispose) GetReadContext() => GetReadContext(_contextFactory);

        private (BusBuddyDbContext Ctx, bool Dispose) GetWriteContext() => GetWriteContext(_contextFactory);

        #region Basic CRUD Operations

        public async Task<List<Driver>> GetAllDriversAsync()
        {
            return (await _cachingService.GetAllDriversAsync(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    Logger.Information("Retrieving all drivers from database (cache miss)");
                    var (context, dispose) = GetReadContext();

                    // Fix any NULL values before attempting to read drivers
                    await FixNullDriverValuesIfNeeded(context);

                    try
                    {
                        return await context.Drivers
                            .AsNoTracking()
                            .ToListAsync();
                    }
                    finally
                    {
                        if (dispose)
                        {
                            await context.DisposeAsync();
                        }
                    }
                }
                catch (System.Data.SqlTypes.SqlNullValueException ex)
                {
                    Logger.Warning(ex, "SQL NULL value error when retrieving drivers. Attempting to fix NULL values and retry.");

                    // Try to fix NULL values and retry once
                    try
                    {
                        var (fixContext, disposeFix) = GetWriteContext();
                        try
                        {
                            await FixNullDriverValuesIfNeeded(fixContext);
                        }
                        finally
                        {
                            if (disposeFix)
                            {
                                await fixContext.DisposeAsync();
                            }
                        }

                        var (retryContext, disposeRetry) = GetReadContext();
                        try
                        {
                            return await retryContext.Drivers
                                .AsNoTracking()
                                .ToListAsync();
                        }
                        finally
                        {
                            if (disposeRetry)
                            {
                                await retryContext.DisposeAsync();
                            }
                        }
                    }
                    catch (Exception retryEx)
                    {
                        Logger.Error(retryEx, "Failed to fix NULL values and retry. Returning empty list to avoid application failure.");
                        return new List<Driver>();
                    }
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving all drivers");
                    throw;
                }
                finally
                {
                    _semaphore.Release();
                }
            })).ToList();
        }

        public async Task<Driver?> GetDriverByIdAsync(int driverId)
        {
            try
            {
                Logger.Information("Retrieving driver with ID: {DriverId}", driverId);
                var (context, dispose) = GetReadContext();
                try
                {
                    return await context.Drivers
                        .AsNoTracking()
                        .Include(d => d.AMRoutes)
                        .Include(d => d.PMRoutes)
                        .FirstOrDefaultAsync(d => d.DriverId == driverId);
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving driver with ID: {DriverId}", driverId);
                throw;
            }
        }

        public async Task<Driver> AddDriverAsync(Driver driver)
        {
            ArgumentNullException.ThrowIfNull(driver);

            try
            {
                Logger.Information("Adding new driver: {DriverName}", driver.DriverName);

                // Validate driver data
                var validationErrors = await ValidateDriverAsync(driver);
                if (validationErrors.Count > 0)
                {
                    throw new ArgumentException($"Driver validation failed: {string.Join(", ", validationErrors)}");
                }

                // Set default values
                if (driver.CreatedDate == default)
                {
                    driver.CreatedDate = DateTime.UtcNow;
                }

                if (string.IsNullOrWhiteSpace(driver.Status))
                {
                    driver.Status = "Active";
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    context.Drivers.Add(driver);
                    await context.SaveChangesAsync();
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }

                // Invalidate cache after adding driver
                _cachingService.InvalidateCache("AllDrivers");

                Logger.Information("Successfully added driver with ID: {DriverId}", driver.DriverId);
                return driver;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error adding driver: {DriverName}", driver.DriverName);
                throw;
            }
        }

        public async Task<bool> UpdateDriverAsync(Driver driver)
        {
            ArgumentNullException.ThrowIfNull(driver);

            try
            {
                Logger.Information("Updating driver with ID: {DriverId}", driver.DriverId);

                // Validate driver data
                var validationErrors = await ValidateDriverAsync(driver);
                if (validationErrors.Count > 0)
                {
                    throw new ArgumentException($"Driver validation failed: {string.Join(", ", validationErrors)}");
                }

                // Update modification timestamp
                driver.UpdatedDate = DateTime.UtcNow;

                var (context, dispose) = GetWriteContext();
                context.Entry(driver).State = EntityState.Modified;

                // Don't modify relationships here - use specific methods for that
                context.Entry(driver).Collection(d => d.AMRoutes).IsModified = false;
                context.Entry(driver).Collection(d => d.PMRoutes).IsModified = false;
                context.Entry(driver).Collection(d => d.Schedules).IsModified = false;

                try
                {
                    var result = await context.SaveChangesAsync();
                    // EF may return 0 when values are unchanged; still treat as success after Modified attach.
                    if (result > 0)
                    {
                        _cachingService.InvalidateCache("AllDrivers");
                        Logger.Information("Successfully updated driver: {DriverName}", driver.DriverName);
                    }
                    else
                    {
                        _cachingService.InvalidateCache("AllDrivers");
                        Logger.Information("Update completed with no column changes for driver: {DriverId}", driver.DriverId);
                    }

                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!await context.Drivers.AnyAsync(e => e.DriverId == driver.DriverId))
                    {
                        Logger.Warning("Driver with ID {DriverId} not found for update", driver.DriverId);
                        return false;
                    }
                    else
                    {
                        DatabaseUserMessage.LogFailure(Logger, ex, "Concurrency error updating driver: {DriverId}", driver.DriverId);
                        throw;
                    }
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating driver with ID: {DriverId}", driver.DriverId);
                throw;
            }
        }

        public async Task<bool> DeleteDriverAsync(int driverId)
        {
            // Soft-retire — never hard-delete while Routes / history may reference the driver.
            Logger.Information("Soft-retiring driver {DriverId} via status Inactive", driverId);
            return await UpdateDriverStatusAsync(driverId, "Inactive");
        }

        #endregion

        #region Query Operations

        public async Task<List<Driver>> GetActiveDriversAsync()
        {
            try
            {
                Logger.Information("Retrieving active drivers");
                var (context, dispose) = GetReadContext();

                // Fix any NULL values before attempting to read drivers
                await FixNullDriverValuesIfNeeded(context);

                try
                {
                    return await context.Drivers
                        .AsNoTracking()
                        .Where(d => d.Status == "Active")
                        .ToListAsync();
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (System.Data.SqlTypes.SqlNullValueException ex)
            {
                Logger.Warning(ex, "SQL NULL value error when retrieving active drivers. Attempting to fix and retry.");

                try
                {
                    var (fixContext, disposeFix) = GetWriteContext();
                    try
                    {
                        await FixNullDriverValuesIfNeeded(fixContext);
                    }
                    finally
                    {
                        if (disposeFix)
                        {
                            await fixContext.DisposeAsync();
                        }
                    }

                    var (retryContext, disposeRetry) = GetReadContext();
                    try
                    {
                        return await retryContext.Drivers
                            .AsNoTracking()
                            .Where(d => d.Status == "Active")
                            .ToListAsync();
                    }
                    finally
                    {
                        if (disposeRetry)
                        {
                            await retryContext.DisposeAsync();
                        }
                    }
                }
                catch (Exception retryEx)
                {
                    Logger.Error(retryEx, "Failed to fix NULL values and retry. Returning empty list.");
                    return new List<Driver>();
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving active drivers");
                throw;
            }
        }

        public async Task<List<Driver>> GetDriversByQualificationStatusAsync(string status)
        {
            ArgumentNullException.ThrowIfNull(status);

            try
            {
                Logger.Information("Retrieving drivers by qualification status: {Status}", status);
                using var context = _contextFactory.CreateDbContext();

                // Since QualificationStatus is a computed property, we need to calculate it in memory
                var drivers = await context.Drivers
                    .AsNoTracking()
                    .ToListAsync();

                return drivers.Where(d => d.QualificationStatus == status).ToList();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving drivers by qualification status: {Status}", status);
                throw;
            }
        }

        public async Task<List<Driver>> GetDriversByLicenseStatusAsync(string status)
        {
            ArgumentNullException.ThrowIfNull(status);

            try
            {
                Logger.Information("Retrieving drivers by license status: {Status}", status);
                using var context = _contextFactory.CreateDbContext();

                // Handle license status filter
                var drivers = await context.Drivers
                    .AsNoTracking()
                    .ToListAsync();

                return drivers.Where(d => d.LicenseStatus == status).ToList();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving drivers by license status: {Status}", status);
                throw;
            }
        }

        public async Task<List<Driver>> SearchDriversAsync(string searchTerm)
        {
            ArgumentNullException.ThrowIfNull(searchTerm);

            try
            {
                Logger.Information("Searching drivers with term: {SearchTerm}", searchTerm);

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return await GetAllDriversAsync();
                }

                var pattern = $"%{searchTerm}%";
                var (context, dispose) = GetReadContext();
                try
                {
                    // Ensure case-insensitive behavior reliably when using InMemory provider (used in tests)
                    var isInMemory = context.Database.ProviderName != null &&
                                     context.Database.ProviderName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

                    if (isInMemory)
                    {
                        var list = await context.Drivers.AsNoTracking().ToListAsync();
                        return list.Where(d =>
                                (!string.IsNullOrEmpty(d.DriverName) && d.DriverName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(d.FirstName) && d.FirstName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(d.LastName) && d.LastName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(d.DriverPhone) && d.DriverPhone.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(d.DriverEmail) && d.DriverEmail.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(d.LicenseNumber) && d.LicenseNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                    }

                    return await context.Drivers
                        .AsNoTracking()
                        .Where(d =>
                            (!string.IsNullOrEmpty(d.DriverName) && EF.Functions.Like(d.DriverName, pattern)) ||
                            (!string.IsNullOrEmpty(d.FirstName) && EF.Functions.Like(d.FirstName, pattern)) ||
                            (!string.IsNullOrEmpty(d.LastName) && EF.Functions.Like(d.LastName, pattern)) ||
                            (!string.IsNullOrEmpty(d.DriverPhone) && EF.Functions.Like(d.DriverPhone, pattern)) ||
                            (!string.IsNullOrEmpty(d.DriverEmail) && EF.Functions.Like(d.DriverEmail, pattern)) ||
                            (!string.IsNullOrEmpty(d.LicenseNumber) && EF.Functions.Like(d.LicenseNumber, pattern)))
                        .ToListAsync();
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error searching drivers with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        #endregion

        #region Route Assignment

        public async Task<List<Driver>> GetAvailableDriversForRouteAsync(DateTime routeDate, bool isAMRoute)
        {
            try
            {
                Logger.Information("Finding available drivers for route date: {RouteDate}, AM: {IsAMRoute}",
                    routeDate.ToShortDateString(), isAMRoute);

                using var context = _contextFactory.CreateDbContext();

                // Get active drivers
                var allActiveDrivers = await context.Drivers
                    .AsNoTracking()
                    .Where(d => d.Status == "Active" && d.TrainingComplete)
                    .ToListAsync();

                // Filter out drivers with expired licenses
                var qualifiedDrivers = allActiveDrivers
                    .Where(d => d.LicenseStatus != "Expired")
                    .ToList();

                // Get all drivers already assigned to routes on that date
                var busyDriverIds = await context.Routes
                    .Where(r => r.Date.Date == routeDate.Date)
                    .Select(r => isAMRoute ? r.AMDriverId : r.PMDriverId)
                    .Where(id => id != null)
                    .ToListAsync();

                // Return drivers not already assigned
                return qualifiedDrivers
                    .Where(d => !busyDriverIds.Contains(d.DriverId))
                    .ToList();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error finding available drivers for route date: {RouteDate}",
                    routeDate.ToShortDateString());
                throw;
            }
        }

        public async Task<bool> AssignDriverToRouteAsync(int driverId, int routeId, bool isAMRoute)
        {
            try
            {
                Logger.Information("Assigning driver {DriverId} to route {RouteId}, AM: {IsAMRoute}",
                    driverId, routeId, isAMRoute);

                DateTime routeDate;
                var (context, dispose) = GetReadContext();
                try
                {
                    var driver = await context.Drivers.FindAsync(driverId);
                    if (driver == null)
                    {
                        Logger.Warning("Driver with ID {DriverId} not found", driverId);
                        return false;
                    }

                    if (driver.Status != "Active" || !driver.TrainingComplete || driver.LicenseStatus == "Expired")
                    {
                        Logger.Warning("Driver {DriverId} is not qualified for assignment", driverId);
                        throw new InvalidOperationException("Driver is not qualified for assignment: " +
                            (driver.Status != "Active" ? "inactive status" :
                             !driver.TrainingComplete ? "training incomplete" :
                             "expired license"));
                    }

                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        Logger.Warning("Route with ID {RouteId} not found", routeId);
                        return false;
                    }

                    routeDate = route.Date;
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }

                if (!await IsDriverAvailableForRouteAsync(driverId, routeDate, isAMRoute))
                {
                    Logger.Warning("Driver {DriverId} is already assigned to another route on {Date}",
                        driverId, routeDate.ToShortDateString());
                    throw new InvalidOperationException("Driver is already assigned to another route at this time");
                }

                var slot = isAMRoute ? RouteTimeSlot.AM : RouteTimeSlot.PM;
                var result = await _routeService.AssignDriverToRouteAsync(routeId, driverId, slot);
                if (!result.IsSuccess)
                {
                    Logger.Warning(
                        "RouteService.AssignDriverToRouteAsync failed for driver {DriverId} route {RouteId}: {Error}",
                        driverId, routeId, result.Error);
                    return false;
                }

                Logger.Information("Successfully assigned driver {DriverId} to route {RouteId}", driverId, routeId);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Rethrow business rule exceptions
                throw;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning driver {DriverId} to route {RouteId}", driverId, routeId);
                throw;
            }
        }

        public async Task<bool> RemoveDriverFromRouteAsync(int routeId, bool isAMRoute)
        {
            try
            {
                Logger.Information("Removing driver from route {RouteId}, AM: {IsAMRoute}", routeId, isAMRoute);

                using var context = _contextFactory.CreateWriteDbContext();

                var route = await context.Routes.FindAsync(routeId);
                if (route == null)
                {
                    Logger.Warning("Route with ID {RouteId} not found", routeId);
                    return false;
                }

                if (isAMRoute)
                {
                    route.AMDriverId = null;
                }
                else
                {
                    route.PMDriverId = null;
                }

                await context.SaveChangesAsync();
                Logger.Information("Successfully removed driver from route {RouteId}", routeId);
                return true;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error removing driver from route {RouteId}", routeId);
                throw;
            }
        }

        public async Task<List<Route>> GetDriverRoutesAsync(int driverId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                Logger.Information("Getting routes for driver {DriverId} from {StartDate} to {EndDate}",
                    driverId, startDate?.ToShortDateString() ?? "all past", endDate?.ToShortDateString() ?? "all future");

                using var context = _contextFactory.CreateDbContext();

                var query = context.Routes
                    .AsNoTracking()
                    .Where(r => r.AMDriverId == driverId || r.PMDriverId == driverId);

                if (startDate.HasValue)
                {
                    query = query.Where(r => r.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(r => r.Date <= endDate.Value.Date);
                }

                // Include vehicle information
                return await query
                    .Include(r => r.AMVehicle)
                    .Include(r => r.PMVehicle)
                    .OrderBy(r => r.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error getting routes for driver {DriverId}", driverId);
                throw;
            }
        }

        public async Task<bool> IsDriverAvailableForRouteAsync(int driverId, DateTime routeDate, bool isAMRoute)
        {
            try
            {
                Logger.Information("Checking if driver {DriverId} is available on {RouteDate}, AM: {IsAMRoute}",
                    driverId, routeDate.ToShortDateString(), isAMRoute);

                var (context, dispose) = GetReadContext();

                // Check if the driver exists and is qualified
                var driver = await context.Drivers.FindAsync(driverId);
                if (driver == null || driver.Status != "Active" || !driver.TrainingComplete || driver.LicenseStatus == "Expired")
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                    return false;
                }

                // Check if the driver is already assigned to another route at the same time
                var isAssigned = await context.Routes
                    .AnyAsync(r => r.Date.Date == routeDate.Date &&
                                  (isAMRoute ? r.AMDriverId == driverId : r.PMDriverId == driverId));
                var available = !isAssigned;
                if (dispose)
                {
                    await context.DisposeAsync();
                }
                return available;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error checking driver {DriverId} availability", driverId);
                throw;
            }
        }

        #endregion

        #region License and Qualification Management

        public async Task<bool> UpdateDriverLicenseInfoAsync(int driverId, string licenseNumber, string licenseClass,
            DateTime expiryDate, string? endorsements = null)
        {
            try
            {
                Logger.Information("Updating license info for driver {DriverId}", driverId);

                var (context, dispose) = GetWriteContext();
                try
                {
                    var driver = await context.Drivers.FindAsync(driverId);
                    if (driver == null)
                    {
                        Logger.Warning("Driver with ID {DriverId} not found", driverId);
                        return false;
                    }

                    // Validate license information
                    if (string.IsNullOrWhiteSpace(licenseNumber))
                    {
                        throw new ArgumentException("License number cannot be empty");
                    }

                    if (string.IsNullOrWhiteSpace(licenseClass))
                    {
                        throw new ArgumentException("License class cannot be empty");
                    }

                    if (expiryDate < DateTime.Today)
                    {
                        throw new ArgumentException("License expiry date cannot be in the past");
                    }

                    // Update license information
                    driver.LicenseNumber = licenseNumber;
                    driver.LicenseClass = licenseClass;
                    driver.LicenseExpiryDate = expiryDate;
                    driver.Endorsements = endorsements;
                    driver.UpdatedDate = DateTime.UtcNow;

                    await context.SaveChangesAsync();
                    Logger.Information("Successfully updated license info for driver {DriverId}", driverId);
                    return true;
                }
                finally
                {
                    if (dispose)
                    {
                        await context.DisposeAsync();
                    }
                }
            }
            catch (ArgumentException)
            {
                // Rethrow validation exceptions
                throw;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating license info for driver {DriverId}", driverId);
                throw;
            }
        }

        public async Task<bool> UpdateDriverQualificationAsync(int driverId, bool trainingComplete,
            DateTime? backgroundCheckDate = null, DateTime? drugTestDate = null, DateTime? physicalExamDate = null)
        {
            try
            {
                Logger.Information("Updating qualification info for driver {DriverId}", driverId);

                using var context = _contextFactory.CreateWriteDbContext();

                var driver = await context.Drivers.FindAsync(driverId);
                if (driver == null)
                {
                    Logger.Warning("Driver with ID {DriverId} not found", driverId);
                    return false;
                }

                // Update qualification information
                driver.TrainingComplete = trainingComplete;

                if (backgroundCheckDate.HasValue)
                {
                    driver.BackgroundCheckDate = backgroundCheckDate;
                    // Standard 2-year expiry for background checks
                    driver.BackgroundCheckExpiry = backgroundCheckDate.Value.AddYears(2);
                }

                if (drugTestDate.HasValue)
                {
                    driver.DrugTestDate = drugTestDate;
                    // Standard 1-year expiry for drug tests
                    driver.DrugTestExpiry = drugTestDate.Value.AddYears(1);
                }

                if (physicalExamDate.HasValue)
                {
                    driver.PhysicalExamDate = physicalExamDate;
                    // Standard 2-year expiry for physical exams
                    driver.PhysicalExamExpiry = physicalExamDate.Value.AddYears(2);
                }

                driver.UpdatedDate = DateTime.UtcNow;

                await context.SaveChangesAsync();
                Logger.Information("Successfully updated qualification info for driver {DriverId}", driverId);
                return true;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating qualification info for driver {DriverId}", driverId);
                throw;
            }
        }

        public async Task<bool> UpdateDriverStatusAsync(int driverId, string status)
        {
            ArgumentNullException.ThrowIfNull(status);

            try
            {
                Logger.Information("Updating status for driver {DriverId} to {Status}", driverId, status);

                // Validate status — use case-insensitive comparison per docs:
                // Enumerable.Contains with IEqualityComparer → https://learn.microsoft.com/dotnet/api/system.linq.enumerable.contains
                var validStatuses = new[] { "Active", "Inactive", "On Leave", "Suspended", "Terminated" };
                if (!validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Invalid status. Valid values are: {string.Join(", ", validStatuses)}");
                }

                using var context = _contextFactory.CreateWriteDbContext();

                var driver = await context.Drivers.FindAsync(driverId);
                if (driver == null)
                {
                    Logger.Warning("Driver with ID {DriverId} not found", driverId);
                    return false;
                }

                // Soft-retire (Inactive/Terminated) is allowed even with route assignments — history must remain.
                // Clear driver FKs only on today/future route days (do not rewrite historical assignment display).
                if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(driver.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    var cutoffDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                    var assignedRoutes = await context.Routes
                        .Where(r =>
                            r.Date >= cutoffDate
                            && (r.AMDriverId == driverId || r.PMDriverId == driverId))
                        .ToListAsync();

                    if (assignedRoutes.Count > 0)
                    {
                        Logger.Warning(
                            "Soft-retiring driver {DriverId} while assigned to {RouteCount} future route(s) — clearing driver FKs",
                            driverId,
                            assignedRoutes.Count);

                        foreach (var route in assignedRoutes)
                        {
                            if (route.AMDriverId == driverId)
                            {
                                route.AMDriverId = null;
                                context.Entry(route).Property(r => r.AMDriverId).IsModified = true;
                            }

                            if (route.PMDriverId == driverId)
                            {
                                route.PMDriverId = null;
                                context.Entry(route).Property(r => r.PMDriverId).IsModified = true;
                            }
                        }
                    }
                }

                driver.Status = status;
                driver.UpdatedDate = DateTime.UtcNow;
                // HasDefaultValue("Active") marks Status as store-generated on add; force update on soft-retire.
                context.Entry(driver).Property(d => d.Status).IsModified = true;
                context.Entry(driver).Property(d => d.UpdatedDate).IsModified = true;

                await context.SaveChangesAsync();
                _cachingService.InvalidateCache("AllDrivers");
                Logger.Information("Successfully updated status for driver {DriverId} to {Status}", driverId, status);
                return true;
            }
            catch (ArgumentException)
            {
                // Rethrow validation exceptions
                throw;
            }
            catch (InvalidOperationException)
            {
                // Rethrow business rule exceptions
                throw;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error updating status for driver {DriverId}", driverId);
                throw;
            }
        }

        #endregion

        #region Driver Validation

        public async Task<List<string>> ValidateDriverAsync(Driver driver)
        {
            var errors = new List<string>();

            try
            {
                // Required field validation
                if (string.IsNullOrWhiteSpace(driver.DriverName))
                {
                    errors.Add("Driver name is required");
                }

                // License type is required on the model but older rows / edit copies may omit it.
                if (string.IsNullOrWhiteSpace(driver.DriversLicenceType))
                {
                    driver.DriversLicenceType = string.IsNullOrWhiteSpace(driver.LicenseClass)
                        ? "CDL"
                        : (driver.LicenseClass.Contains("Regular", StringComparison.OrdinalIgnoreCase) ? "Regular" : "CDL");
                }

                // Phone number validation
                if (!string.IsNullOrWhiteSpace(driver.DriverPhone))
                {
                    var phonePattern = @"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$";
                    if (!Regex.IsMatch(driver.DriverPhone, phonePattern))
                    {
                        errors.Add("Invalid phone number format");
                    }
                }

                // Email validation
                if (!string.IsNullOrWhiteSpace(driver.DriverEmail))
                {
                    var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                    if (!Regex.IsMatch(driver.DriverEmail, emailPattern))
                    {
                        errors.Add("Invalid email format");
                    }
                }

                // State validation
                if (!string.IsNullOrWhiteSpace(driver.State))
                {
                    if (driver.State.Length != 2)
                    {
                        errors.Add("State must be a 2-letter abbreviation");
                    }
                }

                // ZIP code validation
                if (!string.IsNullOrWhiteSpace(driver.Zip))
                {
                    var zipPattern = @"^\d{5}(-\d{4})?$";
                    if (!Regex.IsMatch(driver.Zip, zipPattern))
                    {
                        errors.Add("Invalid ZIP code format");
                    }
                }

                // License expiry validation
                if (driver.LicenseExpiryDate.HasValue && driver.LicenseExpiryDate < DateTime.Today)
                {
                    errors.Add("License is expired");
                }

                // Status validation — use case-insensitive comparison per docs:
                // Enumerable.Contains with IEqualityComparer → https://learn.microsoft.com/dotnet/api/system.linq.enumerable.contains
                var validStatuses = new[] { "Active", "Inactive", "On Leave", "Suspended", "Terminated" };
                if (!string.IsNullOrWhiteSpace(driver.Status) &&
                    !validStatuses.Contains(driver.Status, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Invalid status. Valid values are: {string.Join(", ", validStatuses)}");
                }

                // Check for uniqueness of license number
                if (!string.IsNullOrWhiteSpace(driver.LicenseNumber))
                {
                    using var context = _contextFactory.CreateDbContext();
                    var existingDriver = await context.Drivers
                        .Where(d => d.LicenseNumber == driver.LicenseNumber && d.DriverId != driver.DriverId)
                        .FirstOrDefaultAsync();

                    if (existingDriver != null)
                    {
                        errors.Add($"License number '{driver.LicenseNumber}' is already assigned to another driver");
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error during driver validation");
                errors.Add("Validation error occurred");
            }

            return errors;
        }

        #endregion

        #region Analytics and Reporting

        public async Task<Dictionary<string, int>> GetDriverStatisticsAsync()
        {
            try
            {
                Logger.Information("Calculating driver statistics");

                using var context = _contextFactory.CreateDbContext();
                var stats = new Dictionary<string, int>
                {
                    ["TotalDrivers"] = await context.Drivers.CountAsync(),
                    ["ActiveDrivers"] = await context.Drivers.CountAsync(d => d.Status == "Active"),
                    ["InactiveDrivers"] = await context.Drivers.CountAsync(d => d.Status != "Active"),
                    ["QualifiedDrivers"] = await context.Drivers.CountAsync(d => d.Status == "Active" && d.TrainingComplete),
                    ["TrainingIncompleteDrivers"] = await context.Drivers.CountAsync(d => !d.TrainingComplete)
                };

                // We need to handle license status in memory since it's a computed property
                var driversWithLicenseInfo = await context.Drivers
                    .AsNoTracking()
                    .Where(d => d.LicenseExpiryDate.HasValue)
                    .ToListAsync();

                stats["ExpiredLicenses"] = driversWithLicenseInfo.Count(d => d.LicenseStatus == "Expired");
                stats["ExpiringLicenses"] = driversWithLicenseInfo.Count(d => d.LicenseStatus == "Expiring Soon");
                stats["CurrentLicenses"] = driversWithLicenseInfo.Count(d => d.LicenseStatus == "Current");

                // Routes and driver assignment stats
                stats["DriversWithRouteAssignments"] = await context.Routes
                    .Where(r => r.Date >= DateTime.Today)
                    .Select(r => r.AMDriverId)
                    .Union(context.Routes.Where(r => r.Date >= DateTime.Today).Select(r => r.PMDriverId))
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .CountAsync();

                return stats;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error calculating driver statistics");
                throw;
            }
        }

        public async Task<List<Driver>> GetDriversNeedingRenewalAsync()
        {
            try
            {
                Logger.Information("Finding drivers needing renewal");

                using var context = _contextFactory.CreateDbContext();

                // Get all active drivers
                var activeDrivers = await context.Drivers
                    .AsNoTracking()
                    .Where(d => d.Status == "Active")
                    .ToListAsync();

                // Filter in memory based on computed properties
                var driversNeedingRenewal = activeDrivers
                    .Where(d => d.LicenseStatus == "Expired" || d.LicenseStatus == "Expiring Soon")
                    .ToList();

                // Also add drivers with expiring qualifications
                var thirtyDaysFromNow = DateTime.Today.AddDays(30);
                var driversWithExpiringQualifications = await context.Drivers
                    .AsNoTracking()
                    .Where(d => d.Status == "Active" &&
                               (d.BackgroundCheckExpiry.HasValue && d.BackgroundCheckExpiry <= thirtyDaysFromNow ||
                                d.DrugTestExpiry.HasValue && d.DrugTestExpiry <= thirtyDaysFromNow ||
                                d.PhysicalExamExpiry.HasValue && d.PhysicalExamExpiry <= thirtyDaysFromNow))
                    .ToListAsync();

                // Combine the lists avoiding duplicates
                var result = driversNeedingRenewal.Union(driversWithExpiringQualifications, new DriverIdComparer()).ToList();

                return result;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error finding drivers needing renewal");
                throw;
            }
        }

        public async Task<Dictionary<string, double>> GetDriverAssignmentMetricsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                Logger.Information("Calculating driver assignment metrics from {StartDate} to {EndDate}",
                    startDate.ToShortDateString(), endDate.ToShortDateString());

                using var context = _contextFactory.CreateDbContext();

                // Get all routes in the date range
                var routes = await context.Routes
                    .Where(r => r.Date >= startDate && r.Date <= endDate)
                    .ToListAsync();

                var metrics = new Dictionary<string, double>();

                // Calculate total routes
                var totalRoutes = routes.Count * 2; // AM and PM
                metrics["TotalRoutePeriods"] = totalRoutes;

                // Calculate assigned routes
                var amAssignments = routes.Count(r => r.AMDriverId.HasValue);
                var pmAssignments = routes.Count(r => r.PMDriverId.HasValue);
                var totalAssignments = amAssignments + pmAssignments;

                metrics["AssignedRoutePeriods"] = totalAssignments;
                metrics["UnassignedRoutePeriods"] = totalRoutes - totalAssignments;

                // Calculate assignment percentage
                metrics["AssignmentPercentage"] = totalRoutes > 0
                    ? Math.Round((double)totalAssignments / totalRoutes * 100, 2)
                    : 0;

                // Calculate metrics per driver
                var allDriverIds = routes
                    .Where(r => r.AMDriverId.HasValue)
                    .Select(r => r.AMDriverId!.Value)
                    .Union(routes
                        .Where(r => r.PMDriverId.HasValue)
                        .Select(r => r.PMDriverId!.Value))
                    .Distinct()
                    .ToList();

                metrics["DriversWithAssignments"] = allDriverIds.Count;

                // Calculate average routes per driver
                metrics["AverageAssignmentsPerDriver"] = allDriverIds.Count > 0
                    ? Math.Round((double)totalAssignments / allDriverIds.Count, 2)
                    : 0;

                // Calculate driver utilization
                var totalActiveDrivers = await context.Drivers.CountAsync(d => d.Status == "Active");
                metrics["DriverUtilizationPercentage"] = totalActiveDrivers > 0
                    ? Math.Round((double)allDriverIds.Count / totalActiveDrivers * 100, 2)
                    : 0;

                return metrics;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error calculating driver assignment metrics");
                throw;
            }
        }

        public async Task<string> ExportDriversToCsvAsync()
        {
            try
            {
                var drivers = await GetAllDriversAsync();
                return DriverCsvExporter.ToCsv(drivers);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error exporting drivers to CSV");
                throw;
            }
        }

        #endregion

        #region NULL Value Handling

        /// <summary>
        /// Fix NULL values in the Drivers table to prevent SqlNullValueException
        /// </summary>
        private async Task FixNullDriverValuesIfNeeded(BusBuddyDbContext context)
        {
            // Skip if already fixed in this session
            if (_nullValuesFixed)
            {
                return;
            }

            try
            {
                // This scrubber is SQL Server–oriented (unquoted Drivers, + concat). On Postgres EF
                // creates quoted "Drivers"; unquoted Drivers folds to drivers and 42P01's.
                var provider = context.Database.ProviderName ?? string.Empty;
                if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug("Skipping SQL Server NULL scrubber on Npgsql provider");
                    _nullValuesFixed = true;
                    return;
                }

                // Check if there are any NULL values in required columns
                var hasNullValues = await context.Database.ExecuteSqlRawAsync(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM Drivers
                        WHERE DriverName IS NULL OR DriversLicenseType IS NULL OR Status IS NULL
                    ) THEN 1 ELSE 0 END
                ");

                if (hasNullValues > 0)
                {
                    Logger.Warning("Found NULL values in required Driver columns. Fixing automatically.");

                    // Fix NULL values
                    await context.Database.ExecuteSqlRawAsync(@"
                        UPDATE Drivers
                        SET DriverName = COALESCE(NULLIF(LTRIM(RTRIM(DriverName)), ''), 'Driver-' + CAST(DriverId AS VARCHAR(10)))
                        WHERE DriverName IS NULL OR LTRIM(RTRIM(DriverName)) = '';

                        UPDATE Drivers
                        SET DriversLicenseType = COALESCE(NULLIF(LTRIM(RTRIM(DriversLicenseType)), ''), 'Standard')
                        WHERE DriversLicenseType IS NULL OR LTRIM(RTRIM(DriversLicenseType)) = '';

                        UPDATE Drivers
                        SET Status = COALESCE(NULLIF(LTRIM(RTRIM(Status)), ''), 'Active')
                        WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';

                        UPDATE Drivers
                        SET FirstName = NULL
                        WHERE FirstName = '';

                        UPDATE Drivers
                        SET LastName = NULL
                        WHERE LastName = '';

                        UPDATE Drivers
                        SET DriverPhone = NULL
                        WHERE DriverPhone = '';

                        UPDATE Drivers
                        SET DriverEmail = NULL
                        WHERE DriverEmail = '';

                        UPDATE Drivers
                        SET Address = NULL
                        WHERE Address = '';

                        UPDATE Drivers
                        SET City = NULL
                        WHERE City = '';

                        UPDATE Drivers
                        SET State = NULL
                        WHERE State = '';

                        UPDATE Drivers
                        SET Zip = NULL
                        WHERE Zip = '';
                    ");

                    Logger.Information("Successfully fixed NULL values in Drivers table.");
                }

                // Mark as fixed for this session
                _nullValuesFixed = true;
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error while attempting to fix NULL values in Drivers table");
                // Don't throw — let the calling method handle the original exception
            }
        }

        #endregion

        #region DEBUG Instrumentation

#if DEBUG
        public Task<Dictionary<string, object>> GetDriverDiagnosticsAsync(int driverId) =>
            DriverDiagnostics.GetDriverDiagnosticsAsync(_contextFactory, driverId);

        public Task<Dictionary<string, object>> GetDriverOperationMetricsAsync() =>
            DriverDiagnostics.GetDriverOperationMetricsAsync(
                _contextFactory,
                async isAm => (await GetAvailableDriversForRouteAsync(DateTime.Today, isAm)).Count);
#endif

        #endregion

        #region Helper Classes

        private sealed class DriverIdComparer : IEqualityComparer<Driver>
        {
            public bool Equals(Driver? x, Driver? y)
            {
                if (x == null || y == null)
                {
                    return false;
                }

                return x.DriverId == y.DriverId;
            }

            public int GetHashCode(Driver obj)
            {
                return obj.DriverId.GetHashCode();
            }
        }

        #endregion
    }
}
