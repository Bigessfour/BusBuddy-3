using System;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service for vehicle operations including VIN validation
/// </summary>
public class VehicleService
{
    private readonly BusBuddyDbContext _context;
    private static readonly ILogger Logger = Log.ForContext<VehicleService>();

    public VehicleService(BusBuddyDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Validates VIN format and uniqueness in the database
    /// </summary>
    public async Task<bool> ValidateVinAsync(string vin)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
        {
            Logger.Warning("VIN validation failed: invalid length or empty");
            return false;
        }
        var exists = await _context.Buses.AnyAsync(v => v.VINNumber == vin);
        if (exists)
        {
            Logger.Warning("VIN validation failed: duplicate VIN {VIN}", vin);
            return false;
        }
        return true;
    }
}
