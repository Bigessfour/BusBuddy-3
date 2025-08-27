using System;
using BusBuddy.Core.Domain;

namespace BusBuddy.Tests.Core.Builders
{
    /// <summary>
    /// Builder pattern for creating test Route entities
    /// </summary>
    public class RouteBuilder
    {
        private Route _route;

        public RouteBuilder()
        {
            _route = new Route
            {
                RouteId = 1,
                RouteName = "Test Route",
                Date = DateTime.Today,
                IsActive = true,
                School = "Test School",
                Description = "Test route description",
                Boundaries = "Test boundaries"
            };
        }

        public RouteBuilder WithId(int id)
        {
            _route.RouteId = id;
            return this;
        }

        public RouteBuilder WithName(string name)
        {
            _route.RouteName = name;
            return this;
        }

        public RouteBuilder WithDate(DateTime date)
        {
            _route.Date = date;
            return this;
        }

        public RouteBuilder Inactive()
        {
            _route.IsActive = false;
            return this;
        }

        public RouteBuilder WithSchool(string school)
        {
            _route.School = school;
            return this;
        }

        public RouteBuilder WithDescription(string description)
        {
            _route.Description = description;
            return this;
        }

        public RouteBuilder WithBoundaries(string boundaries)
        {
            _route.Boundaries = boundaries;
            return this;
        }

        public Route Build()
        {
            return _route;
        }
    }

    /// <summary>
    /// Builder pattern for creating test Student entities
    /// </summary>
    public class StudentBuilder
    {
        private Student _student;

        public StudentBuilder()
        {
            _student = new Student
            {
                StudentName = "Test Student",
                Grade = "5",
                School = "Test School",
                ParentGuardian = "Test Parent",
                EmergencyPhone = "555-123-4567",
                HomeAddress = "123 Test St",
                City = "Test City",
                State = "TX",
                Zip = "12345",
                AMRoute = "Test Route",
                Active = true
            };
        }

        public StudentBuilder WithName(string name)
        {
            _student.StudentName = name;
            return this;
        }

        public StudentBuilder WithGrade(string grade)
        {
            _student.Grade = grade;
            return this;
        }

        public StudentBuilder WithSchool(string school)
        {
            _student.School = school;
            return this;
        }

        public StudentBuilder WithParentGuardian(string parent)
        {
            _student.ParentGuardian = parent;
            return this;
        }

        public StudentBuilder WithEmergencyPhone(string phone)
        {
            _student.EmergencyPhone = phone;
            return this;
        }

        public StudentBuilder WithAddress(string address)
        {
            _student.HomeAddress = address;
            return this;
        }

        public StudentBuilder WithCity(string city)
        {
            _student.City = city;
            return this;
        }

        public StudentBuilder WithState(string state)
        {
            _student.State = state;
            return this;
        }

        public StudentBuilder WithZip(string zip)
        {
            _student.Zip = zip;
            return this;
        }

        public StudentBuilder WithAMRoute(string route)
        {
            _student.AMRoute = route;
            return this;
        }

        public StudentBuilder WithPMRoute(string route)
        {
            _student.PMRoute = route;
            return this;
        }

        public StudentBuilder Inactive()
        {
            _student.Active = false;
            return this;
        }

        public Student Build()
        {
            return _student;
        }
    }

    /// <summary>
    /// Builder pattern for creating test Bus entities
    /// </summary>
    public class BusBuilder
    {
        private Bus _bus;

        public BusBuilder()
        {
            _bus = new Bus
            {
                BusId = 1,
                BusNumber = "TEST001",
                SeatingCapacity = 50,
                Status = "Active",
                Model = "Test Model",
                Make = "Test Make",
                Year = 2023
            };
        }

        public BusBuilder WithId(int id)
        {
            _bus.BusId = id;
            return this;
        }

        public BusBuilder WithBusNumber(string busNumber)
        {
            _bus.BusNumber = busNumber;
            return this;
        }

        public BusBuilder WithSeatingCapacity(int capacity)
        {
            _bus.SeatingCapacity = capacity;
            return this;
        }

        public BusBuilder WithStatus(string status)
        {
            _bus.Status = status;
            return this;
        }

        public BusBuilder WithModel(string model)
        {
            _bus.Model = model;
            return this;
        }

        public BusBuilder WithMake(string make)
        {
            _bus.Make = make;
            return this;
        }

        public BusBuilder WithYear(int year)
        {
            _bus.Year = year;
            return this;
        }

        public Bus Build()
        {
            return _bus;
        }
    }

    /// <summary>
    /// Builder pattern for creating test Driver entities
    /// </summary>
    public class DriverBuilder
    {
        private Driver _driver;

        public DriverBuilder()
        {
            _driver = new Driver
            {
                DriverName = "Test Driver",
                LicenseNumber = "DL123456",
                DriverPhone = "555-123-4567",
                Status = "Active",
                HireDate = DateTime.Today.AddYears(-2)
            };
        }

        public DriverBuilder WithName(string name)
        {
            _driver.DriverName = name;
            return this;
        }

        public DriverBuilder WithLicenseNumber(string licenseNumber)
        {
            _driver.LicenseNumber = licenseNumber;
            return this;
        }

        public DriverBuilder WithPhone(string phone)
        {
            _driver.DriverPhone = phone;
            return this;
        }

        public DriverBuilder WithStatus(string status)
        {
            _driver.Status = status;
            return this;
        }

        public DriverBuilder WithHireDate(DateTime hireDate)
        {
            _driver.HireDate = hireDate;
            return this;
        }

        public Driver Build()
        {
            return _driver;
        }
    }

    /// <summary>
    /// Builder pattern for creating test Activity entities
    /// </summary>
    public class ActivityBuilder
    {
        private Activity _activity;

        public ActivityBuilder()
        {
            _activity = new Activity
            {
                ActivityId = 1,
                Date = DateTime.Today,
                ActivityType = "Test Activity",
                Destination = "Test Destination",
                LeaveTime = TimeSpan.FromHours(8),
                EventTime = TimeSpan.FromHours(9),
                RequestedBy = "Test Requester",
                AssignedVehicleId = 1,
                DriverId = 1,
                Status = "Scheduled",
                Description = "Test activity description"
            };
        }

        public ActivityBuilder WithId(int id)
        {
            _activity.ActivityId = id;
            return this;
        }

        public ActivityBuilder WithDate(DateTime date)
        {
            _activity.Date = date;
            return this;
        }

        public ActivityBuilder WithActivityType(string activityType)
        {
            _activity.ActivityType = activityType;
            return this;
        }

        public ActivityBuilder WithDestination(string destination)
        {
            _activity.Destination = destination;
            return this;
        }

        public ActivityBuilder WithLeaveTime(TimeSpan leaveTime)
        {
            _activity.LeaveTime = leaveTime;
            return this;
        }

        public ActivityBuilder WithEventTime(TimeSpan eventTime)
        {
            _activity.EventTime = eventTime;
            return this;
        }

        public ActivityBuilder WithRequestedBy(string requestedBy)
        {
            _activity.RequestedBy = requestedBy;
            return this;
        }

        public ActivityBuilder WithAssignedVehicleId(int vehicleId)
        {
            _activity.AssignedVehicleId = vehicleId;
            return this;
        }

        public ActivityBuilder WithDriverId(int driverId)
        {
            _activity.DriverId = driverId;
            return this;
        }

        public ActivityBuilder WithStatus(string status)
        {
            _activity.Status = status;
            return this;
        }

        public ActivityBuilder WithDescription(string description)
        {
            _activity.Description = description;
            return this;
        }

        public Activity Build()
        {
            return _activity;
        }
    }
}
