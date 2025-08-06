using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Interface for sports event scheduling service
    /// Enhanced for Phase 2 sports scheduling with safety integration
    /// Follows NHTSA safety guidelines and transportation best practices
    /// </summary>
    public interface ISportsSchedulingService
    {
        /// <summary>
        /// Creates a new sports event asynchronously
        /// </summary>
        /// <param name="sportsEvent">The sports event details to create</param>
        /// <returns>The created SportsEvent object</returns>
        Task<SportsEvent> CreateSportsEventAsync(SportsEvent sportsEvent);

        /// <summary>
        /// Gets sports events within a date range
        /// </summary>
        /// <param name="startDate">Start date filter (optional)</param>
        /// <param name="endDate">End date filter (optional)</param>
        /// <returns>List of sports events</returns>
        Task<List<SportsEvent>> GetSportsEventsAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Assigns a vehicle and driver to a sports event asynchronously
        /// </summary>
        /// <param name="eventId">The ID of the sports event</param>
        /// <param name="vehicleId">The ID of the vehicle to assign</param>
        /// <param name="driverId">The ID of the driver to assign</param>
        /// <returns>True if assignment successful</returns>
        Task<bool> AssignVehicleAndDriverAsync(int eventId, int vehicleId, int driverId);

        /// <summary>
        /// Validates if there are any scheduling conflicts for the given vehicle or driver during the event time
        /// </summary>
        /// <param name="startTime">Event start time</param>
        /// <param name="endTime">Event end time</param>
        /// <param name="vehicleId">The ID of the vehicle to check (optional)</param>
        /// <param name="driverId">The ID of the driver to check (optional)</param>
        /// <returns>True if no conflicts, false otherwise</returns>
        Task<bool> ValidateSchedulingConflictsAsync(DateTime startTime, DateTime endTime, int? vehicleId = null, int? driverId = null);

        /// <summary>
        /// Retrieves a list of available vehicles for a given time slot
        /// </summary>
        /// <param name="startTime">The start time of the time slot</param>
        /// <param name="endTime">The end time of the time slot</param>
        /// <param name="minimumCapacity">Minimum vehicle capacity required</param>
        /// <returns>A list of available Bus objects</returns>
        Task<List<Bus>> GetAvailableVehiclesAsync(DateTime startTime, DateTime endTime, int minimumCapacity = 1);

        /// <summary>
        /// Retrieves a list of available drivers for a given time slot
        /// </summary>
        /// <param name="startTime">The start time of the time slot</param>
        /// <param name="endTime">The end time of the time slot</param>
        /// <returns>A list of available Driver objects</returns>
        Task<List<Driver>> GetAvailableDriversAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Updates the status of a sports event
        /// </summary>
        /// <param name="eventId">The ID of the sports event</param>
        /// <param name="status">The new status</param>
        /// <returns>True if update successful</returns>
        Task<bool> UpdateEventStatusAsync(int eventId, string status);

        /// <summary>
        /// Gets upcoming sports events within the specified number of days
        /// </summary>
        /// <param name="days">Number of days to look ahead</param>
        /// <returns>List of upcoming sports events</returns>
        Task<List<SportsEvent>> GetUpcomingSportsEventsAsync(int days = 7);
    }
}
