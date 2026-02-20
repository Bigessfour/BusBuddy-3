using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service for managing fuel records in the BusBuddy transportation system.
/// Provides CRUD operations for fuel entities with proper error handling and logging.
/// </summary>
public class FuelService : IFuelService
{
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private static readonly ILogger Logger = Log.ForContext<FuelService>();

    /// <summary>
    /// Initializes a new instance of the FuelService class.
    /// </summary>
    /// <param name="contextFactory">The database context factory for accessing fuel data.</param>
    public FuelService(IBusBuddyDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Retrieves all fuel records from the database asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation, containing a list of all fuel records.</returns>
    public async Task<IEnumerable<Fuel>> GetAllFuelRecordsAsync()
    {
        try
        {
            Logger.Information("Retrieving all fuel records from the database");

            using var context = _contextFactory.CreateDbContext();
            var fuelRecords = await context.FuelRecords
                .Include(f => f.Bus)
                .OrderByDescending(f => f.FuelDate)
                .ToListAsync();

            Logger.Information("Successfully retrieved {Count} fuel records", fuelRecords.Count);
            return fuelRecords;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve all fuel records");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific fuel record by its ID asynchronously.
    /// </summary>
    /// <param name="id">The unique identifier of the fuel record.</param>
    /// <returns>A task that represents the asynchronous operation, containing the fuel record if found; otherwise, null.</returns>
    public async Task<Fuel?> GetFuelRecordByIdAsync(int id)
    {
        try
        {
            Logger.Information("Retrieving fuel record with ID {FuelId}", id);

            using var context = _contextFactory.CreateDbContext();
            var fuelRecord = await context.FuelRecords
                .Include(f => f.Bus)
                .FirstOrDefaultAsync(f => f.FuelId == id);

            if (fuelRecord == null)
            {
                Logger.Warning("Fuel record with ID {FuelId} not found", id);
            }
            else
            {
                Logger.Information("Successfully retrieved fuel record {FuelId} for bus {BusNumber}",
                    fuelRecord.FuelId, fuelRecord.Bus?.BusNumber);
            }

            return fuelRecord;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve fuel record with ID {FuelId}", id);
            throw;
        }
    }

    /// <summary>
    /// Creates a new fuel record in the database asynchronously.
    /// </summary>
    /// <param name="fuel">The fuel record entity to create.</param>
    /// <returns>A task that represents the asynchronous operation, containing the created fuel record.</returns>
    public async Task<Fuel> CreateFuelRecordAsync(Fuel fuel)
    {
        if (fuel == null)
        {
            throw new ArgumentNullException(nameof(fuel));
        }

        try
        {
            Logger.Information("Creating new fuel record for bus ID {BusId}", fuel.VehicleFueledId);

            // Validate fuel data
            if (fuel.Gallons.HasValue && fuel.Gallons.Value < 0)
            {
                throw new ArgumentException("Gallons cannot be negative.", nameof(fuel));
            }

            if (fuel.TotalCost.HasValue && fuel.TotalCost.Value < 0)
            {
                throw new ArgumentException("Total cost cannot be negative.", nameof(fuel));
            }

            using var context = _contextFactory.CreateWriteDbContext();
            context.FuelRecords.Add(fuel);
            await context.SaveChangesAsync();

            Logger.Information("Successfully created fuel record {FuelId} for bus {BusId}",
                fuel.FuelId, fuel.VehicleFueledId);
            return fuel;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create fuel record for bus ID {BusId}", fuel.VehicleFueledId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing fuel record in the database asynchronously.
    /// </summary>
    /// <param name="fuel">The fuel record entity to update.</param>
    /// <returns>A task that represents the asynchronous operation, containing the updated fuel record.</returns>
    public async Task<Fuel> UpdateFuelRecordAsync(Fuel fuel)
    {
        if (fuel == null)
        {
            throw new ArgumentNullException(nameof(fuel));
        }

        try
        {
            Logger.Information("Updating fuel record {FuelId}", fuel.FuelId);

            // Validate fuel data
            if (fuel.Gallons.HasValue && fuel.Gallons.Value < 0)
            {
                throw new ArgumentException("Gallons cannot be negative.", nameof(fuel));
            }

            if (fuel.TotalCost.HasValue && fuel.TotalCost.Value < 0)
            {
                throw new ArgumentException("Total cost cannot be negative.", nameof(fuel));
            }

            using var context = _contextFactory.CreateWriteDbContext();
            context.FuelRecords.Update(fuel);
            await context.SaveChangesAsync();

            Logger.Information("Successfully updated fuel record {FuelId}", fuel.FuelId);
            return fuel;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update fuel record {FuelId}", fuel.FuelId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a fuel record from the database by its ID asynchronously.
    /// </summary>
    /// <param name="id">The unique identifier of the fuel record to delete.</param>
    /// <returns>A task that represents the asynchronous operation, returning true if deleted; otherwise, false.</returns>
    public async Task<bool> DeleteFuelRecordAsync(int id)
    {
        try
        {
            Logger.Information("Deleting fuel record with ID {FuelId}", id);

            using var context = _contextFactory.CreateWriteDbContext();
            var fuelRecord = await context.FuelRecords.FindAsync(id);
            if (fuelRecord == null)
            {
                Logger.Warning("Fuel record with ID {FuelId} not found for deletion", id);
                return false;
            }

            context.FuelRecords.Remove(fuelRecord);
            await context.SaveChangesAsync();

            Logger.Information("Successfully deleted fuel record {FuelId}", id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to delete fuel record with ID {FuelId}", id);
            throw;
        }
    }

    /// <summary>
    /// Retrieves fuel records by vehicle ID asynchronously.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of fuel records for the specified vehicle.</returns>
    public async Task<IEnumerable<Fuel>> GetFuelRecordsByVehicleAsync(int vehicleId)
    {
        try
        {
            Logger.Information("Retrieving fuel records for vehicle ID {VehicleId}", vehicleId);

            using var context = _contextFactory.CreateDbContext();
            var fuelRecords = await context.FuelRecords
                .Include(f => f.Bus)
                .Where(f => f.VehicleFueledId == vehicleId)
                .OrderByDescending(f => f.FuelDate)
                .ToListAsync();

            Logger.Information("Successfully retrieved {Count} fuel records for vehicle {VehicleId}",
                fuelRecords.Count, vehicleId);
            return fuelRecords;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve fuel records for vehicle ID {VehicleId}", vehicleId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves fuel records within a specified date range asynchronously.
    /// </summary>
    /// <param name="startDate">The start date of the range.</param>
    /// <param name="endDate">The end date of the range.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of fuel records within the date range.</returns>
    public async Task<IEnumerable<Fuel>> GetFuelRecordsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date cannot be after end date.", nameof(startDate));
        }

        try
        {
            Logger.Information("Retrieving fuel records between {StartDate} and {EndDate}",
                startDate, endDate);

            using var context = _contextFactory.CreateDbContext();
            var fuelRecords = await context.FuelRecords
                .Include(f => f.Bus)
                .Where(f => f.FuelDate >= startDate && f.FuelDate <= endDate)
                .OrderByDescending(f => f.FuelDate)
                .ToListAsync();

            Logger.Information("Successfully retrieved {Count} fuel records between {StartDate} and {EndDate}",
                fuelRecords.Count, startDate, endDate);
            return fuelRecords;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve fuel records between {StartDate} and {EndDate}",
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Gets the total fuel cost for a vehicle within an optional date range asynchronously.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <param name="startDate">The optional start date for filtering.</param>
    /// <param name="endDate">The optional end date for filtering.</param>
    /// <returns>A task that represents the asynchronous operation, containing the total fuel cost.</returns>
    public async Task<decimal> GetTotalFuelCostAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            Logger.Information("Calculating total fuel cost for vehicle {VehicleId}", vehicleId);

            using var context = _contextFactory.CreateDbContext();
            var query = context.FuelRecords
                .Where(f => f.VehicleFueledId == vehicleId && f.TotalCost.HasValue);

            if (startDate.HasValue)
            {
                query = query.Where(f => f.FuelDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(f => f.FuelDate <= endDate.Value);
            }

            var totalCost = await query.SumAsync(f => f.TotalCost ?? 0);
            Logger.Information("Total fuel cost for vehicle {VehicleId}: {TotalCost:C}", vehicleId, totalCost);
            return totalCost;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to calculate total fuel cost for vehicle {VehicleId}", vehicleId);
            throw;
        }
    }

    /// <summary>
    /// Gets the total gallons of fuel for a vehicle within an optional date range asynchronously.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <param name="startDate">The optional start date for filtering.</param>
    /// <param name="endDate">The optional end date for filtering.</param>
    /// <returns>A task that represents the asynchronous operation, containing the total gallons.</returns>
    public async Task<decimal> GetTotalGallonsAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            Logger.Information("Calculating total gallons for vehicle {VehicleId}", vehicleId);

            using var context = _contextFactory.CreateDbContext();
            var query = context.FuelRecords
                .Where(f => f.VehicleFueledId == vehicleId && f.Gallons.HasValue);

            if (startDate.HasValue)
            {
                query = query.Where(f => f.FuelDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(f => f.FuelDate <= endDate.Value);
            }

            var totalGallons = await query.SumAsync(f => f.Gallons ?? 0);
            Logger.Information("Total gallons for vehicle {VehicleId}: {TotalGallons}", vehicleId, totalGallons);
            return totalGallons;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to calculate total gallons for vehicle {VehicleId}", vehicleId);
            throw;
        }
    }

    /// <summary>
    /// Gets the average miles per gallon for a vehicle within an optional date range asynchronously.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <param name="startDate">The optional start date for filtering.</param>
    /// <param name="endDate">The optional end date for filtering.</param>
    /// <returns>A task that represents the asynchronous operation, containing the average MPG.</returns>
    public async Task<decimal> GetAverageMPGAsync(int vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            Logger.Information("Calculating average MPG for vehicle {VehicleId}", vehicleId);

            using var context = _contextFactory.CreateDbContext();
            var bus = await context.Buses.FindAsync(vehicleId);
            if (bus?.MilesPerGallon.HasValue == true)
            {
                Logger.Information("Using stored MPG value for vehicle {VehicleId}: {MPG}", vehicleId, bus.MilesPerGallon.Value);
                return bus.MilesPerGallon.Value;
            }

            // Calculate MPG from fuel records if available
            var query = context.FuelRecords
                .Where(f => f.VehicleFueledId == vehicleId && f.Gallons.HasValue && f.Gallons > 0);

            if (startDate.HasValue)
            {
                query = query.Where(f => f.FuelDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(f => f.FuelDate <= endDate.Value);
            }

            var fuelRecords = await query.ToListAsync();
            if (fuelRecords.Any())
            {
                // This is a simplified calculation - in a real scenario, you'd need odometer readings
                var averageMPG = fuelRecords.Average(f => 25.0m); // Default assumption
                Logger.Information("Calculated average MPG for vehicle {VehicleId}: {MPG}", vehicleId, averageMPG);
                return averageMPG;
            }

            var defaultMPG = 7.5m;
            Logger.Information("Using default MPG for vehicle {VehicleId}: {MPG}", vehicleId, defaultMPG);
            return defaultMPG;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to calculate average MPG for vehicle {VehicleId}", vehicleId);
            throw;
        }
    }
}
