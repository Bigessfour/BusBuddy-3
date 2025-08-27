using System;
using System.Collections.Generic;
using System.Linq;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Builder pattern for creating test data with customizable properties
    /// Provides fluent interface for building test entities with specific characteristics
    /// </summary>
    public static class TestDataBuilder
    {
        /// <summary>
        /// Builder for creating Route entities with customizable properties
        /// </summary>
        public class RouteBuilder
        {
            private int _routeId;
            private string _routeName = "Test Route";
            private DateTime _date = DateTime.Today;
            private bool _isActive = true;
            private string _school = "Test School";
            private string _description = "Test route description";
            private string _boundaries = "Test boundaries";

            public RouteBuilder WithId(int id)
            {
                if (id <= 0)
                    throw new ArgumentException("Route ID must be positive", nameof(id));
                _routeId = id;
                return this;
            }

            public RouteBuilder WithName(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Route name cannot be null or empty", nameof(name));
                _routeName = name;
                return this;
            }

            public RouteBuilder WithDate(DateTime date)
            {
                _date = date;
                return this;
            }

            public RouteBuilder Inactive()
            {
                _isActive = false;
                return this;
            }

            public RouteBuilder WithSchool(string school)
            {
                if (string.IsNullOrWhiteSpace(school))
                    throw new ArgumentException("School cannot be null or empty", nameof(school));
                _school = school;
                return this;
            }

            public RouteBuilder WithDescription(string description)
            {
                _description = description ?? string.Empty;
                return this;
            }

            public RouteBuilder WithBoundaries(string boundaries)
            {
                _boundaries = boundaries ?? string.Empty;
                return this;
            }

            public Route Build()
            {
                return new Route
                {
                    RouteId = _routeId,
                    RouteName = _routeName,
                    Date = _date,
                    IsActive = _isActive,
                    School = _school,
                    Description = _description,
                    Boundaries = _boundaries
                };
            }
        }

        /// <summary>
        /// Builder for creating Student entities with customizable properties
        /// </summary>
        public class StudentBuilder
        {
            private string _studentName = "Test Student";
            private string _grade = "1";
            private string _school = "Test School";
            private string _parentGuardian = "Test Parent";
            private string _emergencyPhone = "555-123-4567";
            private string _homeAddress = "123 Test St";
            private string _city = "Test City";
            private string _state = "TX";
            private string _zip = "12345";
            private string? _amRoute;
            private string? _pmRoute;
            private bool _active = true;

            public StudentBuilder WithName(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Student name cannot be null or empty", nameof(name));
                _studentName = name;
                return this;
            }

            public StudentBuilder WithGrade(string grade)
            {
                if (string.IsNullOrWhiteSpace(grade))
                    throw new ArgumentException("Grade cannot be null or empty", nameof(grade));
                _grade = grade;
                return this;
            }

            public StudentBuilder WithSchool(string school)
            {
                if (string.IsNullOrWhiteSpace(school))
                    throw new ArgumentException("School cannot be null or empty", nameof(school));
                _school = school;
                return this;
            }

            public StudentBuilder WithParentGuardian(string parentGuardian)
            {
                if (string.IsNullOrWhiteSpace(parentGuardian))
                    throw new ArgumentException("Parent/Guardian cannot be null or empty", nameof(parentGuardian));
                _parentGuardian = parentGuardian;
                return this;
            }

            public StudentBuilder WithEmergencyPhone(string phone)
            {
                if (string.IsNullOrWhiteSpace(phone))
                    throw new ArgumentException("Emergency phone cannot be null or empty", nameof(phone));
                _emergencyPhone = phone;
                return this;
            }

            public StudentBuilder WithAddress(string address, string city, string state, string zip)
            {
                if (string.IsNullOrWhiteSpace(address))
                    throw new ArgumentException("Address cannot be null or empty", nameof(address));
                if (string.IsNullOrWhiteSpace(city))
                    throw new ArgumentException("City cannot be null or empty", nameof(city));
                if (string.IsNullOrWhiteSpace(state))
                    throw new ArgumentException("State cannot be null or empty", nameof(state));
                if (string.IsNullOrWhiteSpace(zip))
                    throw new ArgumentException("ZIP cannot be null or empty", nameof(zip));

                _homeAddress = address;
                _city = city;
                _state = state;
                _zip = zip;
                return this;
            }

            public StudentBuilder WithAMRoute(string route)
            {
                _amRoute = route;
                return this;
            }

            public StudentBuilder WithPMRoute(string route)
            {
                _pmRoute = route;
                return this;
            }

            public StudentBuilder Inactive()
            {
                _active = false;
                return this;
            }

            public Student Build()
            {
                return new Student
                {
                    StudentName = _studentName,
                    Grade = _grade,
                    School = _school,
                    ParentGuardian = _parentGuardian,
                    EmergencyPhone = _emergencyPhone,
                    HomeAddress = _homeAddress,
                    City = _city,
                    State = _state,
                    Zip = _zip,
                    AMRoute = _amRoute,
                    PMRoute = _pmRoute,
                    Active = _active
                };
            }
        }

        /// <summary>
        /// Builder for creating Bus entities with customizable properties
        /// </summary>
        public class BusBuilder
        {
            private int _busId;
            private string _busNumber = "TEST001";
            private int _seatingCapacity = 50;
            private string _status = "Active";
            private string _model = "Test Model";
            private string _make = "Test Make";
            private int _year = 2023;

            public BusBuilder WithId(int id)
            {
                if (id <= 0)
                    throw new ArgumentException("Bus ID must be positive", nameof(id));
                _busId = id;
                return this;
            }

            public BusBuilder WithBusNumber(string busNumber)
            {
                if (string.IsNullOrWhiteSpace(busNumber))
                    throw new ArgumentException("Bus number cannot be null or empty", nameof(busNumber));
                _busNumber = busNumber;
                return this;
            }

            public BusBuilder WithCapacity(int capacity)
            {
                if (capacity <= 0)
                    throw new ArgumentException("Seating capacity must be positive", nameof(capacity));
                _seatingCapacity = capacity;
                return this;
            }

            public BusBuilder WithStatus(string status)
            {
                if (string.IsNullOrWhiteSpace(status))
                    throw new ArgumentException("Status cannot be null or empty", nameof(status));
                _status = status;
                return this;
            }

            public BusBuilder WithModel(string make, string model, int year)
            {
                if (string.IsNullOrWhiteSpace(make))
                    throw new ArgumentException("Make cannot be null or empty", nameof(make));
                if (string.IsNullOrWhiteSpace(model))
                    throw new ArgumentException("Model cannot be null or empty", nameof(model));
                if (year < 1900 || year > DateTime.Now.Year + 1)
                    throw new ArgumentException("Year must be valid", nameof(year));

                _make = make;
                _model = model;
                _year = year;
                return this;
            }

            public BusBuilder Maintenance()
            {
                _status = "Maintenance";
                return this;
            }

            public Bus Build()
            {
                return new Bus
                {
                    BusId = _busId,
                    BusNumber = _busNumber,
                    SeatingCapacity = _seatingCapacity,
                    Status = _status,
                    Model = _model,
                    Make = _make,
                    Year = _year
                };
            }
        }

        /// <summary>
        /// Builder for creating Driver entities with customizable properties
        /// </summary>
        public class DriverBuilder
        {
            private string _driverName = "Test Driver";
            private string _licenseNumber = "DL123456";
            private string _driverPhone = "555-123-4567";
            private string _status = "Active";
            private DateTime _hireDate = DateTime.Today.AddYears(-1);

            public DriverBuilder WithName(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Driver name cannot be null or empty", nameof(name));
                _driverName = name;
                return this;
            }

            public DriverBuilder WithLicense(string licenseNumber)
            {
                if (string.IsNullOrWhiteSpace(licenseNumber))
                    throw new ArgumentException("License number cannot be null or empty", nameof(licenseNumber));
                _licenseNumber = licenseNumber;
                return this;
            }

            public DriverBuilder WithPhone(string phone)
            {
                if (string.IsNullOrWhiteSpace(phone))
                    throw new ArgumentException("Phone cannot be null or empty", nameof(phone));
                _driverPhone = phone;
                return this;
            }

            public DriverBuilder WithStatus(string status)
            {
                if (string.IsNullOrWhiteSpace(status))
                    throw new ArgumentException("Status cannot be null or empty", nameof(status));
                _status = status;
                return this;
            }

            public DriverBuilder WithHireDate(DateTime hireDate)
            {
                if (hireDate > DateTime.Today)
                    throw new ArgumentException("Hire date cannot be in the future", nameof(hireDate));
                _hireDate = hireDate;
                return this;
            }

            public DriverBuilder Inactive()
            {
                _status = "Inactive";
                return this;
            }

            public Driver Build()
            {
                return new Driver
                {
                    DriverName = _driverName,
                    LicenseNumber = _licenseNumber,
                    DriverPhone = _driverPhone,
                    Status = _status,
                    HireDate = _hireDate
                };
            }
        }

        /// <summary>
        /// Builder for creating Activity entities with customizable properties
        /// </summary>
        public class ActivityBuilder
        {
            private int _activityId;
            private DateTime _date = DateTime.Today;
            private string _activityType = "Test Activity";
            private string _destination = "Test Destination";
            private TimeSpan _leaveTime = TimeSpan.FromHours(8);
            private TimeSpan _eventTime = TimeSpan.FromHours(9);
            private string _requestedBy = "Test Requester";
            private int? _assignedVehicleId;
            private int? _driverId;
            private string _status = "Scheduled";
            private string _description = "Test activity description";

            public ActivityBuilder WithId(int id)
            {
                if (id <= 0)
                    throw new ArgumentException("Activity ID must be positive", nameof(id));
                _activityId = id;
                return this;
            }

            public ActivityBuilder WithDate(DateTime date)
            {
                _date = date;
                return this;
            }

            public ActivityBuilder WithType(string activityType)
            {
                if (string.IsNullOrWhiteSpace(activityType))
                    throw new ArgumentException("Activity type cannot be null or empty", nameof(activityType));
                _activityType = activityType;
                return this;
            }

            public ActivityBuilder WithDestination(string destination)
            {
                if (string.IsNullOrWhiteSpace(destination))
                    throw new ArgumentException("Destination cannot be null or empty", nameof(destination));
                _destination = destination;
                return this;
            }

            public ActivityBuilder WithTimes(TimeSpan leaveTime, TimeSpan eventTime)
            {
                if (leaveTime >= eventTime)
                    throw new ArgumentException("Leave time must be before event time");
                _leaveTime = leaveTime;
                _eventTime = eventTime;
                return this;
            }

            public ActivityBuilder WithRequester(string requestedBy)
            {
                if (string.IsNullOrWhiteSpace(requestedBy))
                    throw new ArgumentException("Requester cannot be null or empty", nameof(requestedBy));
                _requestedBy = requestedBy;
                return this;
            }

            public ActivityBuilder WithVehicle(int vehicleId)
            {
                if (vehicleId <= 0)
                    throw new ArgumentException("Vehicle ID must be positive", nameof(vehicleId));
                _assignedVehicleId = vehicleId;
                return this;
            }

            public ActivityBuilder WithDriver(int driverId)
            {
                if (driverId <= 0)
                    throw new ArgumentException("Driver ID must be positive", nameof(driverId));
                _driverId = driverId;
                return this;
            }

            public ActivityBuilder WithStatus(string status)
            {
                if (string.IsNullOrWhiteSpace(status))
                    throw new ArgumentException("Status cannot be null or empty", nameof(status));
                _status = status;
                return this;
            }

            public ActivityBuilder WithDescription(string description)
            {
                _description = description ?? string.Empty;
                return this;
            }

            public Activity Build()
            {
                return new Activity
                {
                    ActivityId = _activityId,
                    Date = _date,
                    ActivityType = _activityType,
                    Destination = _destination,
                    LeaveTime = _leaveTime,
                    EventTime = _eventTime,
                    RequestedBy = _requestedBy,
                    AssignedVehicleId = _assignedVehicleId ?? 0,
                    DriverId = _driverId,
                    Status = _status,
                    Description = _description
                };
            }
        }

        // Static factory methods for creating builders
        public static RouteBuilder CreateRoute() => new RouteBuilder();
        public static StudentBuilder CreateStudent() => new StudentBuilder();
        public static BusBuilder CreateBus() => new BusBuilder();
        public static DriverBuilder CreateDriver() => new DriverBuilder();
        public static ActivityBuilder CreateActivity() => new ActivityBuilder();

        /// <summary>
        /// Creates a collection of test data using builders
        /// </summary>
        public static class TestDataCollections
        {
            public static IEnumerable<Route> CreateRoutes(int count = 3)
            {
                if (count <= 0)
                    throw new ArgumentException("Count must be positive", nameof(count));

                for (int i = 1; i <= count; i++)
                {
                    yield return CreateRoute()
                        .WithId(i)
                        .WithName($"Route {i}")
                        .WithDate(DateTime.Today.AddDays(i - 1))
                        .Build();
                }
            }

            public static IEnumerable<Student> CreateStudents(int count = 5)
            {
                if (count <= 0)
                    throw new ArgumentException("Count must be positive", nameof(count));

                var routes = new[] { "East Route", "West Route", "North Route" };
                for (int i = 1; i <= count; i++)
                {
                    yield return CreateStudent()
                        .WithName($"Student {i}")
                        .WithGrade(((i % 6) + 1).ToString())
                        .WithAMRoute(routes[i % routes.Length])
                        .Build();
                }
            }

            public static IEnumerable<Bus> CreateBuses(int count = 3)
            {
                if (count <= 0)
                    throw new ArgumentException("Count must be positive", nameof(count));

                for (int i = 1; i <= count; i++)
                {
                    yield return CreateBus()
                        .WithId(i)
                        .WithBusNumber($"BUS{i:000}")
                        .WithCapacity(45 + (i * 5))
                        .Build();
                }
            }

            public static IEnumerable<Driver> CreateDrivers(int count = 3)
            {
                if (count <= 0)
                    throw new ArgumentException("Count must be positive", nameof(count));

                for (int i = 1; i <= count; i++)
                {
                    yield return CreateDriver()
                        .WithName($"Driver {i}")
                        .WithLicense($"DL{i:000000}")
                        .WithPhone($"555-100-{i:0000}")
                        .Build();
                }
            }

            public static IEnumerable<Activity> CreateActivities(int count = 3)
            {
                if (count <= 0)
                    throw new ArgumentException("Count must be positive", nameof(count));

                var activityTypes = new[] { "Morning Pickup", "Afternoon Dropoff", "Maintenance Check" };
                for (int i = 1; i <= count; i++)
                {
                    yield return CreateActivity()
                        .WithId(i)
                        .WithType(activityTypes[i % activityTypes.Length])
                        .WithDate(DateTime.Today.AddDays(i - 1))
                        .Build();
                }
            }
        }
    }
}
