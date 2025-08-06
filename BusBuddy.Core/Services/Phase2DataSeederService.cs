using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Interface for the Phase 2 enhanced data seeding service.
    /// </summary>
    public interface IPhase2DataSeederService
    {
        /// <summary>
        /// Seeds the database with enhanced Phase 2 data asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SeedAsync();

        /// <summary>
        /// Seeds enhanced real-world data with geographical coordinates and realistic patterns.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SeedEnhancedRealWorldDataAsync();
    }

    /// <summary>
    /// Phase 2 enhanced data seeding service for comprehensive real-world transportation data.
    /// Includes 50 professional drivers, 25 school buses, 100 diverse routes, 200 scheduled activities,
    /// geographical coordinates, realistic scheduling patterns, maintenance records, and proper entity relationships.
    /// </summary>
    public class Phase2DataSeederService : IPhase2DataSeederService
    {
        private readonly BusBuddyDbContext _context;
        private static readonly ILogger Logger = Log.ForContext<Phase2DataSeederService>();
        private readonly Random _random = new Random(42); // Seeded for reproducible data

        /// <summary>
        /// Constructor for Phase2DataSeederService that accepts BusBuddyDbContext
        /// </summary>

        private static readonly string[] FirstNames = {
            "Alice", "Bob", "Carlos", "Diana", "Ethan", "Fatima", "George", "Hannah", "Isaac", "Jessica",
            "Kevin", "Linda", "Michael", "Nancy", "Oliver", "Patricia", "Quincy", "Rachel", "Samuel", "Teresa",
            "Ulysses", "Victoria", "William", "Ximena", "Yolanda", "Zachary", "Amanda", "Brian", "Catherine", "Daniel",
            "Eleanor", "Frank", "Grace", "Henry", "Isabella", "James", "Katherine", "Lucas", "Maria", "Nathan",
            "Olivia", "Peter", "Quinn", "Rebecca", "Steven", "Tiffany", "Ursula", "Vincent", "Wendy", "Xavier"
        };

        private static readonly string[] LastNames = {
            "Anderson", "Brown", "Clark", "Davis", "Evans", "Fisher", "Garcia", "Harris", "Jackson", "Johnson",
            "King", "Lewis", "Martinez", "Nelson", "O'Brien", "Parker", "Quinn", "Rodriguez", "Smith", "Taylor",
            "Underwood", "Valdez", "Washington", "Xavier", "Young", "Zhang", "AdAMS", "BAKER", "COOPER", "DIXON",
            "EDWARDS", "FORD", "GREEN", "HALL", "IVERSON", "JONES", "KELLY", "LEE", "MILLER", "NEWMAN",
            "OWENS", "PHILLIPS", "RAMIREZ", "STEWART", "THOMPSON", "UPTON", "VARGAS", "WILSON", "XU", "YAMAMOTO"
        };

        private static readonly string[] BusModels = {
            "Blue Bird Vision", "Thomas Saf-T-Liner C2", "IC Bus CE Series", "Micro Bird G5", "Collins Type A",
            "Blue Bird All American", "Thomas Built C2e", "IC Bus RE Series", "Starcraft Quest", "Trans Tech SST-e"
        };

        private static readonly string[] RouteTypes = {
            "Regular School Route", "Special Needs Transport", "Field Trip", "Sports Event Transport", "Late Bus",
            "Early Dismissal", "After School Program", "Summer School", "Band/Choir Transport", "Academic Competition"
        };

        private static readonly string[] Destinations = {
            "Lincoln Elementary", "Washington Middle School", "Roosevelt High School", "Jefferson Academy", "Madison Elementary",
            "Monroe Middle School", "Adams High School", "Jackson Elementary", "Van Buren Academy", "Harrison Elementary",
            "Tyler Middle School", "Polk High School", "Taylor Elementary", "Fillmore Academy", "Pierce Elementary",
            "Buchanan Middle School", "Lincoln High School", "Johnson Elementary", "Grant Academy", "Hayes Elementary"
        };

        public Phase2DataSeederService(BusBuddyDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task SeedAsync()
        {
            try
            {
                Logger.Information("🎯 Starting Phase 2 enhanced data seeding...");

                // Check if enhanced data already exists - CA1829: Use .Count property
                var driverCount = await _context.Drivers.CountAsync();
                var busCount = await _context.Buses.CountAsync();
                var activityCount = await _context.Activities.CountAsync();
                if (driverCount >= 40 && busCount >= 20 && activityCount >= 150)
                {
                    Logger.Information("Enhanced Phase 2 data already exists. Skipping seeding.");
                    return;
                }

                await SeedEnhancedRealWorldDataAsync();
                Logger.Information("✅ Phase 2 enhanced data seeding completed successfully!");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error during Phase 2 data seeding");
                throw;
            }
        }

        public async Task SeedEnhancedRealWorldDataAsync()
        {
            Logger.Information("📊 Seeding enhanced real-world transportation data...");

            // Clear existing data if needed
            if (await _context.Activities.AnyAsync())
            {
                Logger.Information("Clearing existing activity data for fresh seeding...");
                _context.Activities.RemoveRange(_context.Activities);
                await _context.SaveChangesAsync();
            }

            // Seed drivers (expand to 50)
            var drivers = await SeedEnhancedDriversAsync();

            // Seed vehicles (expand to 25)
            var vehicles = await SeedEnhancedVehiclesAsync();

            // Seed routes (expand to 100)
            var routes = await SeedEnhancedRoutesAsync();

            // Seed activities (expand to 200)
            await SeedEnhancedActivitiesAsync(drivers, vehicles, routes);

            await _context.SaveChangesAsync();

            Logger.Information("📊 Enhanced real-world data seeding completed.");
        }

        private async Task<List<Driver>> SeedEnhancedDriversAsync()
        {
            Logger.Information("👥 Seeding 50 professional drivers...");

            var existingDrivers = await _context.Drivers.ToListAsync();
            var driversToAdd = new List<Driver>();

            // Create professional drivers with realistic data
            for (int i = existingDrivers.Count; i < 50; i++)
            {
                var firstName = FirstNames[_random.Next(FirstNames.Length)];
                var lastName = LastNames[_random.Next(LastNames.Length)];
                var hireDate = DateTime.Now.AddYears(-_random.Next(1, 15));
                var experienceYears = _random.Next(2, 25);

                var driver = new Driver
                {
                    FirstName = firstName,
                    LastName = lastName,
                    LicenseNumber = $"CDL-{_random.Next(100000, 999999)}",
                    DriverPhone = $"({_random.Next(200, 999)}) {_random.Next(200, 999)}-{_random.Next(1000, 9999)}",
                    DriverEmail = $"{firstName.ToLower(CultureInfo.InvariantCulture)}.{lastName.ToLower(CultureInfo.InvariantCulture)}@{GetRandomEmailDomain()}",
                    HireDate = hireDate,
                    Address = GenerateRandomAddress(),
                    EmergencyContactName = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                    EmergencyContactPhone = $"({_random.Next(200, 999)}) {_random.Next(200, 999)}-{_random.Next(1000, 9999)}",
                    Status = _random.NextDouble() > 0.05 ? "Active" : "Inactive",
                    Notes = GenerateDriverNotes(experienceYears)
                };

                driversToAdd.Add(driver);
            }

            if (driversToAdd.Count > 0) // CA1829: Use .Count property
            {
                await _context.Drivers.AddRangeAsync(driversToAdd);
                await _context.SaveChangesAsync();
            }

            return await _context.Drivers.ToListAsync();
        }

        private async Task<List<Bus>> SeedEnhancedVehiclesAsync()
        {
            Logger.Information("🚌 Seeding 25 school buses with maintenance records...");

            var existingVehicles = await _context.Buses.ToListAsync();
            var vehiclesToAdd = new List<Bus>();

            for (int i = existingVehicles.Count; i < 25; i++)
            {
                var capacity = _random.Next(35, 78);
                var model = BusModels[_random.Next(BusModels.Length)];
                var busNumber = $"BUS{i + 1:D3}";
                var plateNumber = $"{busNumber}-{_random.Next(1000, 9999)}";
                var isActive = _random.NextDouble() > 0.1; // 90% active
                var operationalStatus = isActive ? "Operational" : "Out of Service";

                var vehicle = new Bus
                {
                    Make = model.Split(' ')[0],
                    Model = model,
                    LicensePlate = plateNumber,
                    Capacity = capacity,
                    BusNumber = busNumber,
                    Status = operationalStatus
                };

                vehiclesToAdd.Add(vehicle);
            }

            if (vehiclesToAdd.Count > 0) // CA1829: Use .Count property
            {
                await _context.Buses.AddRangeAsync(vehiclesToAdd);
                await _context.SaveChangesAsync();
            }

            return await _context.Buses.ToListAsync();
        }

        private async Task<List<Route>> SeedEnhancedRoutesAsync()
        {
            Logger.Information("🗺️ Seeding 100 diverse routes with geographical data...");

            // School names for route assignments
            var schoolNames = new[]
            {
                "Lincoln Elementary",
                "Washington Middle School",
                "Central High School",
                "Roosevelt Elementary",
                "Jefferson Middle School",
                "Madison High School",
                "Wilson Elementary",
                "Kennedy Middle School",
                "Adams High School"
            };

            // Clear existing routes for fresh data
            if (await _context.Routes.AnyAsync())
            {
                _context.Routes.RemoveRange(_context.Routes);
                await _context.SaveChangesAsync();
            }

            var routes = new List<Route>();

            for (int i = 0; i < 100; i++)
            {
                var routeName = $"Route {i + 1:D3}";
                var description = $"Route serving district stops to random destination.";
                var distance = (decimal?)Math.Round(_random.NextDouble() * 20 + 1, 2); // 1-21 miles
                var estimatedTime = _random.Next(10, 60); // 10-60 min
                var studentCount = _random.Next(10, 75);
                var stopCount = _random.Next(3, 15);
                var busNumber = $"BUS{_random.Next(1, 26):D3}";
                var isActive = _random.NextDouble() > 0.1; // 90% active
                var scheduleDate = DateTime.Today.AddDays(-_random.Next(0, 365));

                var route = new Route
                {
                    RouteName = routeName,
                    Description = description,
                    Date = scheduleDate,
                    Distance = distance,
                    EstimatedDuration = estimatedTime,
                    StudentCount = studentCount,
                    StopCount = stopCount,
                    BusNumber = busNumber,
                    IsActive = isActive,
                    School = schoolNames[_random.Next(schoolNames.Length)]
                };

                routes.Add(route);
            }

            await _context.Routes.AddRangeAsync(routes);
            await _context.SaveChangesAsync();

            return routes;
        }

        private async Task SeedEnhancedActivitiesAsync(List<Driver> drivers, List<Bus> vehicles, List<Route> routes)
        {
            Logger.Information("📅 Seeding 200 scheduled activities with realistic patterns...");

            var activities = new List<Activity>();
            var now = DateTime.Now;

            for (int i = 0; i < 200; i++)
            {
                // Generate realistic scheduling patterns
                var scheduleDate = now.AddDays(_random.Next(-30, 60)); // Past 30 days to future 60 days
                var isSchoolDay = scheduleDate.DayOfWeek != DayOfWeek.Saturday && scheduleDate.DayOfWeek != DayOfWeek.Sunday;

                TimeSpan scheduledTime;
                string activityType;

                if (isSchoolDay)
                {
                    // School day activities
                    if (_random.NextDouble() > 0.5)
                    {
                        // Morning pickup (6:30 AM - 8:30 AM)
                        scheduledTime = new TimeSpan(6, 30 + _random.Next(0, 120), _random.Next(0, 60));
                        activityType = "Morning Pickup";
                    }
                    else
                    {
                        // Afternoon dropoff (2:00 PM - 4:00 PM)
                        scheduledTime = new TimeSpan(14, _random.Next(0, 120), _random.Next(0, 60));
                        activityType = "Afternoon Dropoff";
                    }
                }
                else
                {
                    // Weekend/special activities
                    scheduledTime = new TimeSpan(_random.Next(8, 18), _random.Next(0, 60), 0);
                    activityType = _random.NextDouble() > 0.5 ? "Field Trip" : "Sports Event";
                }

                var selectedDriver = drivers[_random.Next(drivers.Count)];
                var selectedVehicle = vehicles[_random.Next(vehicles.Count)];
                var selectedRoute = routes[_random.Next(routes.Count)];

                var activity = new Activity
                {
                    ActivityType = activityType,
                    Date = scheduleDate,
                    LeaveTime = scheduledTime,
                    EventTime = scheduledTime.Add(TimeSpan.FromMinutes(selectedRoute.EstimatedDuration ?? 30)),
                    DriverId = selectedDriver.Id,
                    AssignedVehicleId = selectedVehicle.Id,
                    RouteId = selectedRoute.RouteId,
                    Status = GetActivityStatus(scheduleDate),
                    StudentsCount = selectedRoute.StudentCount,
                    Notes = GenerateActivityNotes(activityType)
                };

                activities.Add(activity);
            }

            await _context.Activities.AddRangeAsync(activities);
            await _context.SaveChangesAsync();
        }

        #region Helper Methods
        private string GetRandomEmailDomain()
        {
            var domains = new[] { "email.com", "mail.org", "webmail.net", "district.edu", "transport.gov" };
            return domains[_random.Next(domains.Length)];
        }

        private string GenerateRandomAddress()
        {
            var streetNumbers = _random.Next(100, 9999);
            var streets = new[] { "Main St", "Oak Ave", "Pine Rd", "Elm Dr", "Maple Ln", "Cedar Blvd", "Park Ave", "School St" };
            var cities = new[] { "Springfield", "Franklin", "Georgetown", "Madison", "Clinton", "Monroe", "Washington", "Jefferson" };

            return $"{streetNumbers} {streets[_random.Next(streets.Length)]}, {cities[_random.Next(cities.Length)]}, PA {_random.Next(19000, 19999)}";
        }

        private string GenerateDriverNotes(int experienceYears)
        {
            var notes = new List<string>();

            if (experienceYears > 15) { notes.Add("Senior driver with excellent safety record"); }
            if (experienceYears > 10) { notes.Add("Experienced in special needs transportation"); }
            if (_random.NextDouble() > 0.7) { notes.Add("Certified for field trip operations"); }
            if (_random.NextDouble() > 0.8) { notes.Add("Multi-lingual capabilities"); }

            return string.Join("; ", notes);
        }

        private string GenerateVIN()
        {
            const string chars = "ABCDEFGHJKLMNPRSTUVWXYZ1234567890";
            return new string(Enumerable.Repeat(chars, 17).Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        private string GetVehicleStatus()
        {
            var statuses = new[] { "Active", "Active", "Active", "Active", "Maintenance", "Out of Service" };
            return statuses[_random.Next(statuses.Length)];
        }

        private string GenerateVehicleNotes()
        {
            var notes = new[] {
                "Recently serviced", "Due for inspection", "New tires installed",
                "Wheelchair accessible", "Air conditioning serviced", "GPS unit updated"
            };
            return _random.NextDouble() > 0.5 ? notes[_random.Next(notes.Length)] : "";
        }

        private string GenerateLocationName(double lat, double lon)
        {
            var areas = new[] { "Downtown", "Northside", "Southside", "Eastside", "Westside", "Midtown", "Suburbs", "Industrial District" };
            return $"{areas[_random.Next(areas.Length)]} ({lat:F4}, {lon:F4})";
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Simple distance calculation (Haversine formula approximation for short distances)
            var R = 3959; // Earth's radius in miles
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(R * c, 2);
        }

        private string GetActivityStatus(DateTime scheduleDate)
        {
            var now = DateTime.Now;
            if (scheduleDate < now.AddDays(-1)) { return "Completed"; }
            if (scheduleDate < now) { return "In Progress"; }
            if (scheduleDate <= now.AddDays(7)) { return "Scheduled"; }
            return "Planned";
        }

        private string GenerateActivityNotes(string activityType)
        {
            var notes = activityType switch
            {
                "Morning Pickup" => new[] { "Standard morning route", "High ridership expected", "Multiple stops" },
                "Afternoon Dropoff" => new[] { "After school dropoff", "Sports equipment transport", "Standard route" },
                "Field Trip" => new[] { "Educational trip", "Packed lunch required", "Return by 3 PM" },
                "Sports Event" => new[] { "Equipment transport needed", "Competitive event", "Late return expected" },
                _ => new[] { "Standard transportation service", "Regular route operation" }
            };

            return notes[_random.Next(notes.Length)];
        }

        private static SportsEvent CreateSportsEvent(string eventName, string sport, DateTime startTime,
            string location, int teamSize, bool isHomeGame, string status = "Scheduled")
        {
            return new SportsEvent
            {
                EventName = eventName,
                Sport = sport,
                StartTime = startTime,
                EndTime = startTime.AddHours(3),
                Location = location,
                TeamSize = teamSize,
                IsHomeGame = isHomeGame,
                Status = status,
                WeatherConditions = "Clear",
                EmergencyContact = "Athletic Director (555) 123-4567",
                SafetyNotes = $"Standard safety protocols for {sport} transportation",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
        #endregion
    }
}
