using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Represents a sports event requiring transportation services
    /// Enhanced for Phase 2 sports scheduling with safety integration
    /// Follows NHTSA safety guidelines and transportation best practices
    /// </summary>
    [Table("SportsEvents")]
    public class SportsEvent : BaseEntity, INotifyPropertyChanged
    {
        private string _eventName = string.Empty;
        private DateTime _startTime;
        private DateTime _endTime;
        private string _location = string.Empty;
        private int _teamSize;
        private int? _vehicleId;
        private int? _driverId;
        private string _status = "Pending";
        private string _safetyNotes = string.Empty;
        private string _sport = string.Empty;
        private bool _isHomeGame;
        private string _emergencyContact = string.Empty;
        private string _weatherConditions = string.Empty;

        /// <summary>
        /// Name of the sports event (e.g., "Football vs. Central High")
        /// </summary>
        [Required]
        [StringLength(200)]
        public string EventName
        {
            get => _eventName;
            set
            {
                if (_eventName != value)
                {
                    _eventName = value;
                    OnPropertyChanged(nameof(EventName));
                }
            }
        }

        /// <summary>
        /// Start time of the event
        /// </summary>
        [Required]
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged(nameof(StartTime));
                }
            }
        }

        /// <summary>
        /// End time of the event
        /// </summary>
        [Required]
        public DateTime EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged(nameof(EndTime));
                }
            }
        }

        /// <summary>
        /// Location/venue of the event
        /// </summary>
        [Required]
        [StringLength(500)]
        public string Location
        {
            get => _location;
            set
            {
                if (_location != value)
                {
                    _location = value;
                    OnPropertyChanged(nameof(Location));
                }
            }
        }

        /// <summary>
        /// Number of team members requiring transportation
        /// </summary>
        [Range(1, 100)]
        public int TeamSize
        {
            get => _teamSize;
            set
            {
                if (_teamSize != value)
                {
                    _teamSize = value;
                    OnPropertyChanged(nameof(TeamSize));
                }
            }
        }

        /// <summary>
        /// Foreign key to assigned vehicle (nullable)
        /// </summary>
        [ForeignKey("Vehicle")]
        public int? VehicleId
        {
            get => _vehicleId;
            set
            {
                if (_vehicleId != value)
                {
                    _vehicleId = value;
                    OnPropertyChanged(nameof(VehicleId));
                }
            }
        }

        /// <summary>
        /// Foreign key to assigned driver (nullable)
        /// </summary>
        [ForeignKey("Driver")]
        public int? DriverId
        {
            get => _driverId;
            set
            {
                if (_driverId != value)
                {
                    _driverId = value;
                    OnPropertyChanged(nameof(DriverId));
                }
            }
        }

        /// <summary>
        /// Status of the event (Pending/Assigned/InProgress/Completed/Cancelled)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        /// <summary>
        /// Safety notes and requirements for the event
        /// Includes NHTSA guidelines: "Ensure 5-min early arrival", "Students stand 10ft back"
        /// </summary>
        [StringLength(1000)]
        public string SafetyNotes
        {
            get => _safetyNotes;
            set
            {
                if (_safetyNotes != value)
                {
                    _safetyNotes = value;
                    OnPropertyChanged(nameof(SafetyNotes));
                }
            }
        }

        /// <summary>
        /// Type of sport (Football, Basketball, Soccer, etc.)
        /// </summary>
        [StringLength(100)]
        public string Sport
        {
            get => _sport;
            set
            {
                if (_sport != value)
                {
                    _sport = value;
                    OnPropertyChanged(nameof(Sport));
                }
            }
        }

        /// <summary>
        /// Whether this is a home game or away game
        /// </summary>
        public bool IsHomeGame
        {
            get => _isHomeGame;
            set
            {
                if (_isHomeGame != value)
                {
                    _isHomeGame = value;
                    OnPropertyChanged(nameof(IsHomeGame));
                }
            }
        }

        /// <summary>
        /// Emergency contact information for the event
        /// </summary>
        [StringLength(500)]
        public string EmergencyContact
        {
            get => _emergencyContact;
            set
            {
                if (_emergencyContact != value)
                {
                    _emergencyContact = value;
                    OnPropertyChanged(nameof(EmergencyContact));
                }
            }
        }

        /// <summary>
        /// Weather conditions affecting safety and routing
        /// </summary>
        [StringLength(200)]
        public string WeatherConditions
        {
            get => _weatherConditions;
            set
            {
                if (_weatherConditions != value)
                {
                    _weatherConditions = value;
                    OnPropertyChanged(nameof(WeatherConditions));
                }
            }
        }

        // Navigation properties
        /// <summary>
        /// Navigation property to assigned bus (was Vehicle, now Bus after merge)
        /// </summary>
        public virtual Bus? Vehicle { get; set; }

        /// <summary>
        /// Navigation property to assigned driver
        /// </summary>
        public virtual Driver? Driver { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Gets default safety notes based on NHTSA guidelines
        /// </summary>
        public string GetDefaultSafetyNotes()
        {
            return "• Students arrive 5 minutes early\n" +
                   "• Stand 10 feet back from vehicle\n" +
                   "• Driver performs blind spot check\n" +
                   "• Group walking for visibility\n" +
                   "• Weather-aware route planning";
        }

        /// <summary>
        /// Validates event for safety requirements
        /// </summary>
        public bool IsEventSafe()
        {
            var hasBasicInfo = !string.IsNullOrEmpty(EventName) &&
                              !string.IsNullOrEmpty(Location) &&
                              TeamSize > 0;

            var hasTimeValidation = StartTime < EndTime &&
                                   StartTime > DateTime.Now;

            var hasSafetyNotes = !string.IsNullOrEmpty(SafetyNotes);

            return hasBasicInfo && hasTimeValidation && hasSafetyNotes;
        }
    }
}
