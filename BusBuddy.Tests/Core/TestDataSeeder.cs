using System;
using System.Collections.Generic;
using System.Linq;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Centralized database seeding utility for test data
    /// Provides consistent test data across all test classes
    /// Follows Microsoft EF Core data seeding best practices
    /// </summary>
    public static class TestDataSeeder
    {
        private static readonly object _lock = new object();
        private static bool _isSeeded;

        /// <summary>
        /// Seeds the database with test data once per test run
        /// Thread-safe to prevent duplicate seeding in parallel tests
        /// </summary>
        public static void SeedDatabase(BusBuddyDbContext context)
        {
            lock (_lock)
            {
                if (_isSeeded)
                {
                    return; // Already seeded
                }

                try
                {
                    SeedCoreData(context);
                    SeedRoutes(context);
                    SeedBuses(context);
                    SeedDrivers(context);
                    SeedStudents(context);
                    SeedActivities(context);

                    context.SaveChanges();
                    _isSeeded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database seeding failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Resets the seeding flag for testing purposes
        /// </summary>
        public static void ResetSeedingFlag()
        {
            lock (_lock)
            {
                _isSeeded = false;
            }
        }

        /// <summary>
        /// Seeds database with custom data for specific test scenarios
        /// </summary>
        public static void SeedCustomData(BusBuddyDbContext context, IEnumerable<Route>? routes = null,
                                        IEnumerable<Bus>? buses = null, IEnumerable<Student>? students = null,
                                        IEnumerable<Driver>? drivers = null, IEnumerable<Activity>? activities = null)
        {
            if (routes != null)
            {
                context.Routes.AddRange(routes);
            }

            if (buses != null)
            {
                context.Buses.AddRange(buses);
            }

            if (students != null)
            {
                context.Students.AddRange(students);
            }

            if (drivers != null)
            {
                context.Drivers.AddRange(drivers);
            }

            if (activities != null)
            {
                context.Activities.AddRange(activities);
            }

            context.SaveChanges();
        }

        private static void SeedCoreData(BusBuddyDbContext context)
        {
            // Ensure no existing data conflicts
            if (context.Routes.Any() || context.Buses.Any() || context.Students.Any())
            {
                return; // Data already exists
            }
        }

        private static void SeedRoutes(BusBuddyDbContext context)
        {
            var routes = new[]
            {
                new Route
                {
                    RouteId = 1,
                    RouteName = "East Route",
                    Date = DateTime.Today,
                    IsActive = true,
                    School = "Test School",
                    Description = "Morning east side route",
                    Boundaries = "East District"
                },
                new Route
                {
                    RouteId = 2,
                    RouteName = "West Route",
                    Date = DateTime.Today,
                    IsActive = true,
                    School = "Test School",
                    Description = "Afternoon west side route",
                    Boundaries = "West District"
                },
                new Route
                {
                    RouteId = 3,
                    RouteName = "North Route",
                    Date = DateTime.Today,
                    IsActive = false,
                    School = "Test School",
                    Description = "Inactive north route",
                    Boundaries = "North District"
                },
                new Route
                {
                    RouteId = 4,
                    RouteName = "South Route",
                    Date = DateTime.Today.AddDays(1),
                    IsActive = true,
                    School = "Test School",
                    Description = "South side route",
                    Boundaries = "South District"
                }
            };

            context.Routes.AddRange(routes);
        }

        private static void SeedBuses(BusBuddyDbContext context)
        {
            var buses = new[]
            {
                new Bus
                {
                    BusId = 1,
                    BusNumber = "TEST001",
                    SeatingCapacity = 50,
                    Status = "Active",
                    Model = "Test Model 1",
                    Make = "Test Make",
                    Year = 2023,
                    GPSTracking = true,
                    CurrentLatitude = 40.7128m,
                    CurrentLongitude = -74.0060m,
                    NextMaintenanceDue = DateTime.Today.AddDays(30),
                    DateLastInspection = DateTime.Today.AddDays(-180)
                },
                new Bus
                {
                    BusId = 2,
                    BusNumber = "TEST002",
                    SeatingCapacity = 45,
                    Status = "Active",
                    Model = "Test Model 2",
                    Make = "Test Make",
                    Year = 2022,
                    GPSTracking = true,
                    CurrentLatitude = 40.7589m,
                    CurrentLongitude = -73.9851m,
                    NextMaintenanceDue = DateTime.Today.AddDays(15),
                    DateLastInspection = DateTime.Today.AddDays(-90)
                },
                new Bus
                {
                    BusId = 3,
                    BusNumber = "TEST003",
                    SeatingCapacity = 55,
                    Status = "Maintenance",
                    Model = "Test Model 3",
                    Make = "Test Make",
                    Year = 2021,
                    GPSTracking = false,
                    CurrentLatitude = null,
                    CurrentLongitude = null,
                    NextMaintenanceDue = DateTime.Today.AddDays(-5), // Overdue
                    DateLastInspection = DateTime.Today.AddDays(-400) // Overdue inspection
                }
            };

            context.Buses.AddRange(buses);
        }

        private static void SeedDrivers(BusBuddyDbContext context)
        {
            var drivers = new[]
            {
                new Driver
                {
                    DriverName = "John Driver",
                    LicenseNumber = "DL123456",
                    DriverPhone = "555-100-1234",
                    Status = "Active",
                    HireDate = DateTime.Today.AddYears(-2)
                },
                new Driver
                {
                    DriverName = "Jane Driver",
                    LicenseNumber = "DL789012",
                    DriverPhone = "555-100-5678",
                    Status = "Active",
                    HireDate = DateTime.Today.AddYears(-3)
                },
                new Driver
                {
                    DriverName = "Bob Driver",
                    LicenseNumber = "DL345678",
                    DriverPhone = "555-100-9012",
                    Status = "Inactive",
                    HireDate = DateTime.Today.AddYears(-1)
                }
            };

            context.Drivers.AddRange(drivers);
        }

        private static void SeedStudents(BusBuddyDbContext context)
        {
            var students = new[]
            {
                new Student
                {
                    StudentName = "Alice Johnson",
                    Grade = "3",
                    School = "Test School",
                    ParentGuardian = "Bob Johnson",
                    EmergencyPhone = "555-010-1234",
                    HomeAddress = "123 East St",
                    City = "Test City",
                    State = "TX",
                    Zip = "12345",
                    AMRoute = "East Route",
                    Active = true
                },
                new Student
                {
                    StudentName = "Bob Smith",
                    Grade = "4",
                    School = "Test School",
                    ParentGuardian = "Jane Smith",
                    EmergencyPhone = "555-010-5678",
                    HomeAddress = "456 West Ave",
                    City = "Test City",
                    State = "TX",
                    Zip = "12346",
                    PMRoute = "West Route",
                    Active = true
                },
                new Student
                {
                    StudentName = "Charlie Brown",
                    Grade = "5",
                    School = "Test School",
                    ParentGuardian = "Lucy Brown",
                    EmergencyPhone = "555-010-9012",
                    HomeAddress = "789 North Blvd",
                    City = "Test City",
                    State = "TX",
                    Zip = "12347",
                    AMRoute = "East Route",
                    Active = true
                },
                new Student
                {
                    StudentName = "Diana Prince",
                    Grade = "2",
                    School = "Test School",
                    ParentGuardian = "Steve Prince",
                    EmergencyPhone = "555-010-3456",
                    HomeAddress = "321 South St",
                    City = "Test City",
                    State = "TX",
                    Zip = "12348",
                    AMRoute = "South Route",
                    Active = true
                },
                new Student
                {
                    StudentName = "Eve Adams",
                    Grade = "6",
                    School = "Test School",
                    ParentGuardian = "Frank Adams",
                    EmergencyPhone = "555-010-7890",
                    HomeAddress = "654 Main St",
                    City = "Test City",
                    State = "TX",
                    Zip = "12349",
                    AMRoute = "North Route",
                    Active = false // Inactive student
                }
            };

            context.Students.AddRange(students);
        }

        private static void SeedActivities(BusBuddyDbContext context)
        {
            var activities = new[]
            {
                new Activity
                {
                    ActivityId = 1,
                    Date = DateTime.Today,
                    ActivityType = "Morning Pickup",
                    Destination = "School",
                    LeaveTime = TimeSpan.FromHours(7),
                    EventTime = TimeSpan.FromHours(8),
                    RequestedBy = "School Administration",
                    AssignedVehicleId = 1,
                    DriverId = 1,
                    Status = "Scheduled",
                    Description = "Student pickup for morning routes"
                },
                new Activity
                {
                    ActivityId = 2,
                    Date = DateTime.Today,
                    ActivityType = "Afternoon Dropoff",
                    Destination = "Home",
                    LeaveTime = TimeSpan.FromHours(15),
                    EventTime = TimeSpan.FromHours(16),
                    RequestedBy = "School Administration",
                    AssignedVehicleId = 2,
                    DriverId = 2,
                    Status = "Scheduled",
                    Description = "Student dropoff for afternoon routes"
                },
                new Activity
                {
                    ActivityId = 3,
                    Date = DateTime.Today,
                    ActivityType = "Maintenance Check",
                    Destination = "Garage",
                    LeaveTime = TimeSpan.FromHours(9),
                    EventTime = TimeSpan.FromHours(10),
                    RequestedBy = "Maintenance Department",
                    AssignedVehicleId = 3,
                    DriverId = 1,
                    Status = "Scheduled",
                    Description = "Daily bus maintenance inspection"
                }
            };

            context.Activities.AddRange(activities);
        }
    }
}
