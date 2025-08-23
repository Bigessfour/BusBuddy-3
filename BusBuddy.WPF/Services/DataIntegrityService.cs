using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Models;
using Serilog;
using BusBuddy.Core.Utilities;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service for validating data integrity across all transportation entities
    /// Provides comprehensive validation for routes, activities, students, drivers, and vehicles
    /// </summary>
    public class DataIntegrityService : IDataIntegrityService
    {
        private static readonly ILogger Logger = Log.ForContext<DataIntegrityService>();
        private readonly IRouteService _routeService;
        private readonly IDriverService _driverService;
        private readonly IBusService _busService;
        private readonly IActivityService _activityService;
        private readonly IStudentService _studentService;

        public DataIntegrityService(
            IRouteService routeService,
            IDriverService driverService,
            IBusService busService,
            IActivityService activityService,
            IStudentService studentService)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _driverService = driverService ?? throw new ArgumentNullException(nameof(driverService));
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));
            _activityService = activityService ?? throw new ArgumentNullException(nameof(activityService));
            _studentService = studentService ?? throw new ArgumentNullException(nameof(studentService));
        }

        /// <summary>
        /// Perform comprehensive data integrity validation across all entities
        /// </summary>
        public async Task<DataIntegrityReport> ValidateAllDataAsync()
        {
            Logger.Information("Starting comprehensive data integrity validation");
            var report = new DataIntegrityReport();

            try
            {
                // Validate each entity type
                var routeValidation = await ValidateRoutesAsync();
                var activityValidation = await ValidateActivitiesAsync();
                var studentValidation = await ValidateStudentsAsync();
                var driverValidation = await ValidateDriversAsync();
                var vehicleValidation = await ValidateVehiclesAsync();
                var crossValidation = await ValidateCrossEntityRelationshipsAsync();

                // Combine all validation results
                report.RouteIssues = routeValidation;
                report.ActivityIssues = activityValidation;
                report.StudentIssues = studentValidation;
                report.DriverIssues = driverValidation;
                report.VehicleIssues = vehicleValidation;
                report.CrossEntityIssues = crossValidation;

                report.TotalIssuesFound =
                    routeValidation.Count +
                    activityValidation.Count +
                    studentValidation.Count +
                    driverValidation.Count +
                    vehicleValidation.Count +
                    crossValidation.Count;

                Logger.Information("Data integrity validation completed. Total issues found: {IssueCount}", report.TotalIssuesFound);
                return report;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to complete data integrity validation");
                report.ValidationError = ex.Message;
                return report;
            }
        }

        /// <summary>
        /// Validate route data integrity
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateRoutesAsync()
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                var result = await _routeService.GetAllActiveRoutesAsync();
                var routes = result.IsSuccess && result.Value != null
                    ? result.Value
                    : Enumerable.Empty<BusBuddy.Core.Models.Route>();

                foreach (var route in routes)
                {
                    // Validate basic route properties
                    if (string.IsNullOrWhiteSpace(route.RouteName))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Route name is null or empty",
                            Severity = "High"
                        });
                    }

                    // Validate date is not in the past
                    if (route.Date.Date < DateTime.Today)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "Route date is in the past",
                            Severity = "Medium"
                        });
                    }

                    // Validate vehicle assignments
                    if (route.AMVehicleId.HasValue && route.AMVehicleId <= 0)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Invalid Data Format",
                            Description = "AM Vehicle ID must be greater than 0 if assigned",
                            Severity = "High"
                        });
                    }

                    if (route.PMVehicleId.HasValue && route.PMVehicleId <= 0)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Invalid Data Format",
                            Description = "PM Vehicle ID must be greater than 0 if assigned",
                            Severity = "High"
                        });
                    }

                    // Validate driver assignments
                    if (route.AMDriverId.HasValue && route.AMDriverId <= 0)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Invalid Data Format",
                            Description = "AM Driver ID must be greater than 0 if assigned",
                            Severity = "High"
                        });
                    }

                    if (route.PMDriverId.HasValue && route.PMDriverId <= 0)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Invalid Data Format",
                            Description = "PM Driver ID must be greater than 0 if assigned",
                            Severity = "High"
                        });
                    }

                    // Validate mileage consistency
                    if (route.AMBeginMiles.HasValue && route.AMEndMiles.HasValue &&
                        route.AMEndMiles < route.AMBeginMiles)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "AM end miles must be greater than or equal to begin miles",
                            Severity = "High"
                        });
                    }

                    if (route.PMBeginMiles.HasValue && route.PMEndMiles.HasValue &&
                        route.PMEndMiles < route.PMBeginMiles)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Route",
                            EntityId = route.RouteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "PM end miles must be greater than or equal to begin miles",
                            Severity = "High"
                        });
                    }
                }

                // Check for duplicate route names on the same date
                var result2 = await _routeService.GetAllActiveRoutesAsync();
                var routes2 = result2.IsSuccess && result2.Value != null
                    ? result2.Value
                    : Enumerable.Empty<BusBuddy.Core.Models.Route>();

                var duplicateRouteNames = routes2
                    .GroupBy(r => new { r.RouteName, r.Date.Date })
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                foreach (var duplicate in duplicateRouteNames)
                {
                    issues.Add(new DataIntegrityIssue
                    {
                        EntityType = "Route",
                        EntityId = "Multiple",
                        IssueType = "Duplicate Data",
                        Description = $"Route name '{duplicate.RouteName}' is used by multiple routes on {duplicate.Date:yyyy-MM-dd}",
                        Severity = "High"
                    });
                }

                Logger.Information("Route validation completed. Found {IssueCount} issues", issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during route validation");
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = "Route",
                    EntityId = "System",
                    IssueType = "Validation Error",
                    Description = $"Route validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validate activity data integrity
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateActivitiesAsync()
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                var activities = await _activityService.GetAllActivitiesAsync();
                var drivers = await _driverService.GetAllDriversAsync();
                var vehicles = await _busService.GetAllBusesAsync();
                var routes = await _routeService.GetAllActiveRoutesAsync();

                foreach (var activity in activities)
                {
                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(activity.ActivityType))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Activity",
                            EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Activity type is null or empty",
                            Severity = "High"
                        });
                    }

                    // Validate date logic
                    if (activity.LeaveTime >= activity.EventTime)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Activity",
                            EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "Activity leave time must be before event time",
                            Severity = "High"
                        });
                    }

                    // Validate future dates for scheduled activities
                    if (activity.Status == "Scheduled" && activity.Date.Date < DateTime.Today)
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Activity",
                            EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "Scheduled activity cannot have date in the past",
                            Severity = "Medium"
                        });
                    }

                    // Validate driver assignment
                    if (activity.DriverId.HasValue)
                    {
                        var assignedDriver = drivers.FirstOrDefault(d => d.DriverId == activity.DriverId.Value);
                        if (assignedDriver == null)
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                IssueType = "Reference Integrity",
                                Description = $"Assigned driver ID {activity.DriverId} does not exist",
                                Severity = "High"
                            });
                        }
                        else if (assignedDriver.Status != "Active")
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(),
                                IssueType = "Business Logic Violation",
                                Description = $"Driver {assignedDriver.FullName} is not active",
                                Severity = "Medium"
                            });
                        }
                    }

                    // Validate vehicle assignment
                    if (activity.AssignedVehicleId > 0)
                    {
                        var assignedVehicle = vehicles.FirstOrDefault(v => v.BusId == activity.AssignedBusId);
                        if (assignedVehicle == null)
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                IssueType = "Reference Integrity",
                                Description = $"Assigned vehicle ID {activity.AssignedBusId} does not exist",
                                Severity = "High"
                            });
                        }
                        else if (assignedVehicle.Status != "Active")
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                IssueType = "Business Logic Violation",
                                Description = $"Vehicle {assignedVehicle.BusNumber} is not active",
                                Severity = "Medium"
                            });
                        }
                    }

                    // Validate route assignment for route-based activities
                    if (activity.RouteId.HasValue)
                    {
                        var assignedRoute = routes.Value?.FirstOrDefault(r => r.RouteId == activity.RouteId.Value);
                        if (assignedRoute == null)
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(),
                                IssueType = "Reference Integrity",
                                Description = $"Assigned route ID {activity.RouteId} does not exist",
                                Severity = "High"
                            });
                        }
                    }
                }

                Logger.Information("Activity validation completed. Found {IssueCount} issues", issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during activity validation");
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = "Activity",
                    EntityId = "System",
                    IssueType = "Validation Error",
                    Description = $"Activity validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validate student data integrity
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateStudentsAsync()
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                var students = await _studentService.GetAllStudentsAsync();
                var routes = await _routeService.GetAllActiveRoutesAsync();

                foreach (var student in students)
                {
                    // Validate required fields - Student model uses StudentName, not FirstName/LastName
                    if (string.IsNullOrWhiteSpace(student.StudentName))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Student name is null or empty",
                            Severity = "High"
                        });
                    }

                    // Validate student number format
                    if (string.IsNullOrWhiteSpace(student.StudentNumber))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Student number is required",
                            Severity = "High"
                        });
                    }

                    // Validate grade level - Grade is string in Student model
                    if (string.IsNullOrWhiteSpace(student.Grade))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Grade is required",
                            Severity = "Medium"
                        });
                    }

                    // Validate home address
                    if (string.IsNullOrWhiteSpace(student.HomeAddress))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Home address is required for transportation",
                            Severity = "High"
                        });
                    }

                    // Validate contact information - Use correct property names
                    if (string.IsNullOrWhiteSpace(student.EmergencyPhone) && string.IsNullOrWhiteSpace(student.ParentGuardian))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Student has no emergency phone or parent guardian contact",
                            Severity = "Critical"
                        });
                    }

                    // Validate bus stop assignments if present
                    if (!string.IsNullOrWhiteSpace(student.AMRoute) && string.IsNullOrWhiteSpace(student.BusStop))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "Student assigned to AM route but no bus stop specified",
                            Severity = "Medium"
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(student.PMRoute) && string.IsNullOrWhiteSpace(student.BusStop))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Student",
                            EntityId = student.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Business Logic Violation",
                            Description = "Student assigned to PM route but no bus stop specified",
                            Severity = "Medium"
                        });
                    }
                }

                // Check for duplicate student numbers
                var duplicateStudentNumbers = students
                    .Where(s => !string.IsNullOrWhiteSpace(s.StudentNumber))
                    .GroupBy(s => s.StudentNumber)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                foreach (var duplicateNumber in duplicateStudentNumbers)
                {
                    issues.Add(new DataIntegrityIssue
                    {
                        EntityType = "Student",
                        EntityId = "Multiple",
                        IssueType = "Duplicate Data",
                        Description = $"Student number {duplicateNumber} is used by multiple students",
                        Severity = "High"
                    });
                }

                Logger.Information("Student validation completed. Found {IssueCount} issues", issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during student validation");
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = "Student",
                    EntityId = "System",
                    IssueType = "Validation Error",
                    Description = $"Student validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validate driver data integrity
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateDriversAsync()
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                var drivers = await _driverService.GetAllDriversAsync();

                foreach (var driver in drivers)
                {
                    // Validate license expiration - Driver model uses LicenseExpiryDate
                    if (driver.LicenseExpiryDate.HasValue && driver.LicenseExpiryDate < DateTime.Now.AddDays(30))
                    {
                        var severity = driver.LicenseExpiryDate < DateTime.Now ? "Critical" : "High";
                        var description = driver.LicenseExpiryDate < DateTime.Now
                            ? "Driver license has expired"
                            : "Driver license expires within 30 days";

                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Driver",
                            EntityId = driver.DriverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "License Issue",
                            Description = description,
                            Severity = severity
                        });
                    }

                    // Validate contact information - Driver model uses DriverPhone
                    if (string.IsNullOrWhiteSpace(driver.DriverPhone))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Driver",
                            EntityId = driver.DriverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Driver has no phone number",
                            Severity = "High"
                        });
                    }

                    // Validate driver name
                    if (string.IsNullOrWhiteSpace(driver.DriverName))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Driver",
                            EntityId = driver.DriverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Driver name is required",
                            Severity = "High"
                        });
                    }

                    // Validate driver status
                    if (string.IsNullOrWhiteSpace(driver.Status))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Driver",
                            EntityId = driver.DriverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Driver status is required",
                            Severity = "Medium"
                        });
                    }
                }

                Logger.Information("Driver validation completed. Found {IssueCount} issues", issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during driver validation");
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = "Driver",
                    EntityId = "System",
                    IssueType = "Validation Error",
                    Description = $"Driver validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validate vehicle data integrity
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateVehiclesAsync()
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                var vehicles = await _busService.GetAllBusesAsync();

                foreach (var vehicle in vehicles)
                {
                    // Validate vehicle number/bus number
                    if (string.IsNullOrWhiteSpace(vehicle.BusNumber))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Vehicle",
                            EntityId = vehicle.BusId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Bus number is required",
                            Severity = "High"
                        });
                    }

                    // Validate vehicle status
                    if (string.IsNullOrWhiteSpace(vehicle.Status))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Vehicle",
                            EntityId = vehicle.BusId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Vehicle status is required",
                            Severity = "Medium"
                        });
                    }

                    // Validate make and model for better tracking
                    if (string.IsNullOrWhiteSpace(vehicle.Make) && string.IsNullOrWhiteSpace(vehicle.Model))
                    {
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "Vehicle",
                            EntityId = vehicle.BusId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            IssueType = "Missing Required Data",
                            Description = "Vehicle make and model information is missing",
                            Severity = "Low"
                        });
                    }
                }

                Logger.Information("Vehicle validation completed. Found {IssueCount} issues", issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during vehicle validation");
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = "Vehicle",
                    EntityId = "System",
                    IssueType = "Validation Error",
                    Description = $"Vehicle validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validate cross-entity relationships and business rules
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateCrossEntityRelationshipsAsync()
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                var activities = await _activityService.GetAllActivitiesAsync();
                var drivers = await _driverService.GetAllDriversAsync();
                var vehicles = await _busService.GetAllBusesAsync();

                // Check for double-booked drivers
                var driverConflicts = activities
                    .Where(a => a.DriverId.HasValue && a.Status == "Scheduled")
                    .GroupBy(a => new { a.DriverId, Date = a.Date.Date })
                    .Where(g => g.Count() > 1);

                foreach (var conflict in driverConflicts)
                {
                    var conflictingActivities = conflict.ToList();
                    foreach (var activity in conflictingActivities)
                    {
                        var overlaps = conflictingActivities
                            .Where(other => other.ActivityId != activity.ActivityId)
                            .Any(other => activity.LeaveTime < other.EventTime && activity.EventTime > other.LeaveTime);

                        if (overlaps)
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                IssueType = "Scheduling Conflict",
                                Description = $"Driver {conflict.Key.DriverId} has overlapping activities",
                                Severity = "High"
                            });
                        }
                    }
                }

                // Check for double-booked vehicles
                var vehicleConflicts = activities
                    .Where(a => a.AssignedVehicleId > 0 && a.Status == "Scheduled")
                    .GroupBy(a => new { BusId = a.AssignedBusId, Date = a.Date.Date })
                    .Where(g => g.Count() > 1);

                foreach (var conflict in vehicleConflicts)
                {
                    var conflictingActivities = conflict.ToList();
                    foreach (var activity in conflictingActivities)
                    {
                        var overlaps = conflictingActivities
                            .Where(other => other.ActivityId != activity.ActivityId)
                            .Any(other => activity.LeaveTime < other.EventTime && activity.EventTime > other.LeaveTime);

                        if (overlaps)
                        {
                            issues.Add(new DataIntegrityIssue
                            {
                                EntityType = "Activity",
                                EntityId = activity.ActivityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                IssueType = "Scheduling Conflict",
                                Description = $"Vehicle {conflict.Key.BusId} has overlapping activities",
                                Severity = "High"
                            });
                        }
                    }
                }

                Logger.Information("Cross-entity validation completed. Found {IssueCount} issues", issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during cross-entity validation");
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = "System",
                    EntityId = "CrossEntity",
                    IssueType = "Validation Error",
                    Description = $"Cross-entity validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validate specific entity by ID
        /// </summary>
        public async Task<List<DataIntegrityIssue>> ValidateEntityAsync(string entityType, int entityId)
        {
            var issues = new List<DataIntegrityIssue>();

            try
            {
                switch (entityType.ToLowerInvariant())
                {
                    case "route":
                        var routeIssues = await ValidateRoutesAsync();
                        issues.AddRange(routeIssues.Where(i => i.EntityId == entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                        break;

                    case "activity":
                        var activityIssues = await ValidateActivitiesAsync();
                        issues.AddRange(activityIssues.Where(i => i.EntityId == entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                        break;

                    case "student":
                        var studentIssues = await ValidateStudentsAsync();
                        issues.AddRange(studentIssues.Where(i => i.EntityId == entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                        break;

                    case "driver":
                        var driverIssues = await ValidateDriversAsync();
                        issues.AddRange(driverIssues.Where(i => i.EntityId == entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                        break;

                    case "vehicle":
                        var vehicleIssues = await ValidateVehiclesAsync();
                        issues.AddRange(vehicleIssues.Where(i => i.EntityId == entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                        break;

                    default:
                        issues.Add(new DataIntegrityIssue
                        {
                            EntityType = "System",
                            EntityId = "Validation",
                            IssueType = "Invalid Request",
                            Description = $"Unknown entity type: {entityType}",
                            Severity = "Medium"
                        });
                        break;
                }

                Logger.Information("Entity validation completed for {EntityType} {EntityId}. Found {IssueCount} issues",
                    entityType, entityId, issues.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during entity validation for {EntityType} {EntityId}", entityType, entityId);
                issues.Add(new DataIntegrityIssue
                {
                    EntityType = entityType,
                    EntityId = entityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    IssueType = "Validation Error",
                    Description = $"Entity validation failed: {ex.Message}",
                    Severity = "Critical"
                });
            }

            return issues;
        }
    }
}
