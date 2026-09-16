using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusBuddy.Core.Models;

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

        /// <summary>
        /// Leftover FK. Board trips MUST NOT set this. Route != Trip; never add IsTrip to Route.
        /// </summary>
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

        [StringLength(32)]
        [Display(Name = "Status")]
        public string Status { get; set; } = TripStatus.Draft;

        [StringLength(32)]
        [Display(Name = "Ticket #")]
        public string? ExternalTicketNo { get; set; }

        [StringLength(16)]
        [Display(Name = "School Year")]
        public string SchoolYear { get; set; } = string.Empty;

        [StringLength(8)]
        [Display(Name = "Requesting School")]
        public string? RequestingSchool { get; set; }

        [StringLength(200)]
        [Display(Name = "Team / Activity")]
        public string? GroupOrActivity { get; set; }

        [Display(Name = "Trip Date")]
        public DateTime TripDate { get; set; }

        [Display(Name = "Pickup Time")]
        public TimeSpan? PickupTime { get; set; }

        [Display(Name = "Return Clock Time")]
        public TimeSpan? ReturnClockTime { get; set; }

        [Display(Name = "Return Next Day")]
        public bool ReturnIsNextDay { get; set; }

        [StringLength(200)]
        [Display(Name = "Origin Name")]
        public string? OriginName { get; set; }

        [Display(Name = "Origin Location")]
        public int? OriginLocationId { get; set; }

        [ForeignKey(nameof(OriginLocationId))]
        public virtual Destination? OriginLocation { get; set; }

        [StringLength(200)]
        [Display(Name = "Destination Name")]
        public string? DestinationName { get; set; }

        [Display(Name = "Destination Location")]
        public int? DestinationLocationId { get; set; }

        [ForeignKey(nameof(DestinationLocationId))]
        public virtual Destination? DestinationLocation { get; set; }

        [Display(Name = "PAX")]
        public int? PlannedHeadcount { get; set; }

        [Display(Name = "Rode")]
        public int? ActualHeadcount { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        [Display(Name = "Planned Miles")]
        public decimal? PlannedMiles { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        [Display(Name = "Path Miles")]
        public decimal? PathMiles { get; set; }

        [Display(Name = "Linked Trip")]
        public int? LinkedTripId { get; set; }

        [ForeignKey(nameof(LinkedTripId))]
        public virtual TripEvent? LinkedTrip { get; set; }

        [Display(Name = "Multi-asset")]
        public bool IsMultiAsset { get; set; }

        [Display(Name = "Overnight pending")]
        public bool IsOvernightPending { get; set; }

        [StringLength(40)]
        [Display(Name = "Board Bus #")]
        public string? AssignedBusNumber { get; set; }

        /// <summary>Board Driver column — alias for <see cref="DriverId"/>.</summary>
        [NotMapped]
        public int? AssignedDriverId
        {
            get => DriverId;
            set => DriverId = value;
        }

        /// <summary>Board Bus # — alias for <see cref="VehicleId"/>. Null when <see cref="IsMultiAsset"/>.</summary>
        [NotMapped]
        public int? AssignedBusId
        {
            get => VehicleId;
            set => VehicleId = value;
        }

        [NotMapped]
        public bool HasPlace
        {
            get
            {
                if (DestinationLocationId.HasValue)
                {
                    return true;
                }

                var name = !string.IsNullOrWhiteSpace(DestinationName) ? DestinationName : Destination;
                return !LocationTypes.IsUnresolvedPlaceName(name);
            }
        }

        [NotMapped]
        public bool HasTimes => PickupTime.HasValue;

        [NotMapped]
        public bool HasValidatedDestination =>
            DestinationLocation is not null && DestinationLocation.HasValidatedCoordinates;

        [NotMapped]
        public bool HasValidatedOrigin =>
            OriginLocation is not null && OriginLocation.HasValidatedCoordinates;

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
        /// DOT pre-trip plus post-trip inspection pad applied to every trip's billed driver hours.
        /// Dated PreTrip/PostTrip duty logs stay optional proof; this is not a duty-event insert.
        /// </summary>
        public const decimal PrePostTripInspectionHours = 1.5m;

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
        /// Scheduled wheel time plus the permanent 1.5 hour inspection pad. Does not change HomeRouteId.
        /// </summary>
        [NotMapped]
        public decimal DriverHours
        {
            get
            {
                var duration = (decimal)(EndTime - StartTime).TotalHours;
                if (duration < 0)
                {
                    duration = 0;
                }

                return Math.Round(duration + PrePostTripInspectionHours, 2);
            }
        }

        /// <summary>Clerk purpose: Sports (any Athletic_*), Field Trip, or a custom catalog reason.</summary>
        [NotMapped]
        public string Purpose => Type switch
        {
            TripType.Field => "Field Trip",
            TripType.Custom => string.IsNullOrWhiteSpace(CustomType) ? "Custom Trip" : CustomType,
            _ => "Sports"
        };

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
                Status = TripStatus.Draft,
                TripDate = leaveTime.Date,
                PickupTime = leaveTime.TimeOfDay,
                DestinationName = destination
            };
        }

        /// <summary>
        /// Import / board status. Playoff rows with a ticket and PAX but no place/time stay MissingInfo.
        /// Import never returns Confirmed.
        /// </summary>
        public static string InferBoardStatus(TripEvent trip)
        {
            ArgumentNullException.ThrowIfNull(trip);

            if (!trip.HasPlace || !trip.HasTimes)
            {
                return TripStatus.MissingInfo;
            }

            if (trip.IsMultiAsset)
            {
                return trip.DriverId.HasValue ? TripStatus.Assigned : TripStatus.Draft;
            }

            if (trip.DriverId.HasValue || trip.VehicleId.HasValue)
            {
                return TripStatus.Assigned;
            }

            return TripStatus.Draft;
        }
    }
}
