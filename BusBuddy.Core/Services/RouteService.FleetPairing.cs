using BusBuddy.Core.Models;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Utilities;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics; // Added for Stopwatch timing (basic instrumentation)

namespace BusBuddy.Core.Services
{
    /// <summary>Year-default bus and driver on the route row. Does not read RouteAssignments.</summary>
    public partial class RouteService
    {
        public async Task<Result<List<Bus>>> GetAvailableBusesAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var buses = await context.Buses
                        .Where(b => b.Status == "Active" || b.Status == "InService")
                        .OrderBy(b => b.BusNumber)
                        .ToListAsync();
                    return Result.SuccessResult(buses);
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving available buses");
                return Result.FailureResult<List<Bus>>($"Error retrieving buses: {ex.Message}", ex);
            }
        }

        public async Task<Result<List<Driver>>> GetAvailableDriversAsync()
        {
            try
            {
                var (context, dispose) = GetReadContext();
                try
                {
                    var drivers = await context.Drivers
                        .Where(d => d.Status == "Active")
                        .OrderBy(d => d.DriverName)
                        .ToListAsync();
                    return Result.SuccessResult(drivers);
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error retrieving available drivers");
                return Result.FailureResult<List<Driver>>($"Error retrieving drivers: {ex.Message}", ex);
            }
        }

        public async Task<Result<bool>> AssignVehicleToRouteAsync(int routeId, int vehicleId, RouteTimeSlot timeSlot)
        {
            try
            {
                var (opId, sw) = StartOp("AssignVehicle", routeId);
                if (routeId <= 0 || vehicleId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or vehicleId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        Logger.Error("AssignVehicle failed — route {RouteId} not found", routeId);
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    var bus = await context.Buses.FindAsync(vehicleId);
                    if (bus == null)
                    {
                        Logger.Error("AssignVehicle failed — vehicle {VehicleId} not found", vehicleId);
                        return Result.FailureResult<bool>($"Vehicle with ID {vehicleId} not found");
                    }

                    // Availability check (Active / In Service)
                    if (!RouteVehicleLinker.IsAssignableStatus(bus.Status))
                    {
                        Logger.Error("AssignVehicle failed — vehicle {VehicleId} not available (Status: {Status})", vehicleId, bus.Status);
                        return Result.FailureResult<bool>($"Vehicle {bus.BusNumber} is not available (status: {bus.Status})");
                    }

                    RouteVehicleLinker.Apply(route, bus, timeSlot);

                    // BusBuddyDbContext defaults to NoTracking. Find can return an untracked
                    // route, and SaveChanges would then skip the pairing columns.
                    context.Entry(route).State = EntityState.Modified;
                    await context.SaveChangesAsync();

                    Logger.Information("Assigned vehicle {VehicleId} to route {RouteId} for {TimeSlot} OpId={OpId}", vehicleId, routeId, timeSlot, opId);
                    EndOpOk("AssignVehicle", opId, sw, routeId);
                    return Result.SuccessResult(true);
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning vehicle {VehicleId} to route {RouteId}", vehicleId, routeId);
                return Result.FailureResult<bool>($"Error assigning vehicle to route: {ex.Message}");
            }
        }

        public async Task<Result<bool>> AssignDriverToRouteAsync(int routeId, int driverId, RouteTimeSlot timeSlot)
        {
            try
            {
                var (opId, sw) = StartOp("AssignDriver", routeId);
                if (routeId <= 0 || driverId <= 0)
                {
                    return Result.FailureResult<bool>("Invalid routeId or driverId");
                }

                var (context, dispose) = GetWriteContext();
                try
                {
                    var route = await context.Routes.FindAsync(routeId);
                    if (route == null)
                    {
                        return Result.FailureResult<bool>($"Route with ID {routeId} not found");
                    }

                    // Basic verification driver exists
                    var driver = await context.Drivers.FindAsync(driverId);
                    if (driver == null)
                    {
                        return Result.FailureResult<bool>($"Driver with ID {driverId} not found");
                    }

                    switch (timeSlot)
                    {
                        case RouteTimeSlot.AM:
                            route.AMDriverId = driverId;
                            break;
                        case RouteTimeSlot.PM:
                            route.PMDriverId = driverId;
                            break;
                        case RouteTimeSlot.Both:
                            route.AMDriverId = driverId;
                            route.PMDriverId = driverId;
                            break;
                        default:
                            return Result.FailureResult<bool>("Unsupported time slot");
                    }

                    // BusBuddyDbContext defaults to NoTracking; test factories often forget TrackAll
                    // on write. Mark modified so leftover DriverService wrap persists either way.
                    context.Entry(route).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    Logger.Information("Assigned driver {DriverId} to route {RouteId} for {TimeSlot} OpId={OpId}", driverId, routeId, timeSlot, opId);
                    EndOpOk("AssignDriver", opId, sw, routeId);
                    return Result.SuccessResult(true);
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error assigning driver {DriverId} to route {RouteId}", driverId, routeId);
                return Result.FailureResult<bool>($"Error assigning driver to route: {ex.Message}");
            }
        }
    }
}
