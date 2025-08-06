using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models.Trips
{
    /// <summary>
    /// Trip type enumeration for athletic and field trips
    /// </summary>
    public enum TripType
    {
        Athletic_Football,
        Athletic_Basketball,
        Athletic_Baseball,
        Athletic_Volleyball,
        Athletic_JH_Football,
        Athletic_JH_Basketball,
        Athletic_JH_Baseball,
        Athletic_JH_Volleyball,
        Athletic_Wrestling,
        Athletic_JH_Wrestling,
        Athletic_Softball,
        Field,
        Custom
    }

    /// <summary>
    /// Enhanced trip event model for athletic and field trips
    /// Core domain model without UI dependencies
    /// </summary>
    [Table("TripEvents")]
    public class TripEvent
    {
        [Key]
        public int TripEventId { get; set; }

        [Required]
        [Display(Name = "Trip Type")]
        public TripType Type { get; set; } = TripType.Field;

        [StringLength(100)]
        [Display(Name = "Custom Trip Type")]
        public string? CustomType { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Point of Contact")]
        public string POCName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "POC Phone")]
        public string? POCPhone { get; set; }

        [StringLength(100)]
        [Display(Name = "POC Email")]
        public string? POCEmail { get; set; }

        [Required]
        [Display(Name = "Leave Time")]
        public DateTime LeaveTime { get; set; }

        [Display(Name = "Return Time")]
        public DateTime? ReturnTime { get; set; }

        [ForeignKey("Vehicle")]
        [Display(Name = "Vehicle")]
        public int? VehicleId { get; set; }

        [ForeignKey("Driver")]
        [Display(Name = "Driver")]
        public int? DriverId { get; set; }

        [ForeignKey("Route")]
        [Display(Name = "Route")]
        public int? RouteId { get; set; }

        [Display(Name = "Student Count")]
        public int StudentCount { get; set; } = 0;

        [Display(Name = "Adult Supervisor Count")]
        public int AdultSupervisorCount { get; set; } = 0;

        [StringLength(200)]
        [Display(Name = "Destination")]
        public string? Destination { get; set; }

        [StringLength(500)]
        [Display(Name = "Special Requirements")]
        public string? SpecialRequirements { get; set; }

        [StringLength(1000)]
        [Display(Name = "Trip Notes")]
        public string? TripNotes { get; set; }

        [Display(Name = "Approval Required")]
        public bool ApprovalRequired { get; set; } = false;

        [Display(Name = "Approved")]
        public bool IsApproved { get; set; } = false;

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approval Date")]
        public DateTime? ApprovalDate { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled, Pending

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        // Navigation properties
        public virtual Bus? Vehicle { get; set; }
        public virtual Driver? Driver { get; set; }
        public virtual Route? Route { get; set; }

        /// <summary>
        /// Gets a formatted display name for the trip type
        /// </summary>
        [NotMapped]
        public string DisplayTripType
        {
            get
            {
                return Type switch
                {
                    TripType.Athletic_Football => "Football",
                    TripType.Athletic_Basketball => "Basketball",
                    TripType.Athletic_Baseball => "Baseball",
                    TripType.Athletic_Volleyball => "Volleyball",
                    TripType.Athletic_JH_Football => "JH Football",
                    TripType.Athletic_JH_Basketball => "JH Basketball",
                    TripType.Athletic_JH_Baseball => "JH Baseball",
                    TripType.Athletic_JH_Volleyball => "JH Volleyball",
                    TripType.Athletic_Wrestling => "Wrestling",
                    TripType.Athletic_JH_Wrestling => "JH Wrestling",
                    TripType.Athletic_Softball => "Softball",
                    TripType.Field => "Field Trip",
                    TripType.Custom => CustomType ?? "Custom Trip",
                    _ => "Unknown"
                };
            }
        }

        /// <summary>
        /// Gets whether this trip is an athletic trip
        /// </summary>
        [NotMapped]
        public bool IsAthleticTrip => Type != TripType.Field && Type != TripType.Custom;

        /// <summary>
        /// Gets whether vehicle or driver assignments are missing
        /// </summary>
        [NotMapped]
        public bool HasUnassignedResources => !VehicleId.HasValue || !DriverId.HasValue;

        /// <summary>
        /// Gets formatted assignment status
        /// </summary>
        [NotMapped]
        public string AssignmentStatus
        {
            get
            {
                if (VehicleId.HasValue && DriverId.HasValue)
                {

                    return "Fully Assigned";
                }

                else if (!VehicleId.HasValue && !DriverId.HasValue)
                {

                    return "Unassigned";
                }

                else if (!VehicleId.HasValue)
                {

                    return "Need Vehicle";
                }
                else
                {

                    return "Need Driver";
                }

            }
        }

        /// <summary>
        /// Gets the start time for scheduling purposes
        /// </summary>
        [NotMapped]
        public DateTime StartTime => LeaveTime;

        /// <summary>
        /// Gets the end time for scheduling purposes
        /// </summary>
        [NotMapped]
        public DateTime EndTime => ReturnTime ?? LeaveTime.AddHours(4);

        /// <summary>
        /// Gets the subject/title for display
        /// </summary>
        [NotMapped]
        public string Subject => $"{DisplayTripType} - {Destination ?? "TBD"}";

        /// <summary>
        /// Gets trip notes for display
        /// </summary>
        [NotMapped]
        public string Notes => $"POC: {POCName}\nStudents: {StudentCount}\nStatus: {AssignmentStatus}";

        /// <summary>
        /// Creates a sample trip event for testing
        /// </summary>
        public static TripEvent CreateSample(TripType tripType, DateTime leaveTime, string destination, string pocName)
        {
            return new TripEvent
            {
                Type = tripType,
                LeaveTime = leaveTime,
                ReturnTime = leaveTime.AddHours(4),
                Destination = destination,
                POCName = pocName,
                StudentCount = 25,
                AdultSupervisorCount = 3,
                Status = "Scheduled"
            };
        }
    }
}
