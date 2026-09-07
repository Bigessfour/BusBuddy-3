using System;
using System.Collections.Generic;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Requirements analysis for activity schedule data structure
    /// </summary>
    public class ActivityScheduleRequirements
    {
        public int ExistingCount { get; set; }
        public string[] RequiredFields { get; set; } = Array.Empty<string>();
        public string[] OptionalFields { get; set; } = Array.Empty<string>();
        public string[] DataValidationRules { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Result of data seeding operation
    /// </summary>
    public class SeedDataResult
    {
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DriversSeeded { get; set; }
        public int VehiclesSeeded { get; set; }
        public int ActivitiesSeeded { get; set; }
        public int RoutesSeeded { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Real-world transportation data structure for JSON import
    /// </summary>
    public class RealWorldTransportationData
    {
        public RealWorldDriver[] Drivers { get; set; } = Array.Empty<RealWorldDriver>();
        public RealWorldVehicle[] Vehicles { get; set; } = Array.Empty<RealWorldVehicle>();
        public RealWorldActivity[] Activities { get; set; } = Array.Empty<RealWorldActivity>();
    }

    /// <summary>
    /// Real-world driver data structure
    /// </summary>
    public class RealWorldDriver
    {
        public string DriverName { get; set; } = string.Empty;
        public string? DriverPhone { get; set; }
        public string? DriverEmail { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string DriversLicenceType { get; set; } = string.Empty;
        public bool TrainingComplete { get; set; }
    }

    /// <summary>
    /// Real-world vehicle data structure
    /// </summary>
    public class RealWorldVehicle
    {
        public string BusNumber { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int SeatingCapacity { get; set; }
        public string VinNumber { get; set; } = string.Empty;
        public string? LicenseNumber { get; set; }
        public DateTime? DateLastInspection { get; set; }
        public int? CurrentOdometer { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchasePrice { get; set; }
    }

    /// <summary>
    /// Real-world activity/trip data structure
    /// </summary>
    public class RealWorldActivity
    {
        public DateTime ScheduledDate { get; set; }
        public string TripType { get; set; } = string.Empty;
        public int ScheduledVehicleId { get; set; }
        public string ScheduledDestination { get; set; } = string.Empty;
        public TimeSpan ScheduledLeaveTime { get; set; }
        public TimeSpan ScheduledEventTime { get; set; }
        public int? ScheduledRiders { get; set; }
        public int ScheduledDriverId { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Result of student data seeding operation
    /// </summary>
    public class StudentSeedResult
    {
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int StudentsSeeded { get; set; }
        public int FamiliesProcessed { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }
}
