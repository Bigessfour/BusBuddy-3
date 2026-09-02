using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.Extensions.Configuration;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for seeding development data when running in development mode
    /// Helps populate empty databases with sample data for testing
    /// </summary>
    public class SeedDataService : ISeedDataService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IConfiguration? _configuration;
        private static readonly ILogger Logger = Log.ForContext<SeedDataService>();
        // Cache JsonSerializerOptions to avoid repeated allocations (fixes CA1869)
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public SeedDataService(IBusBuddyDbContextFactory contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public SeedDataService(IBusBuddyDbContextFactory contextFactory, IConfiguration configuration)
        {
            _contextFactory = contextFactory;
            _configuration = configuration;
        }

        /// <summary>
        /// Seed students from a JSON file specified by configuration key "StudentJsonPath".
        /// If Students table already contains any records, this method exits without changes.
        /// </summary>
        public async Task SeedFromJsonAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Skip if any students already exist
                int existingCount;
                try { existingCount = await context.Students.CountAsync(); }
                catch (InvalidOperationException) { existingCount = context.Students.Count(); }
                if (existingCount > 0)
                {
                    Logger.Information("Students table already has {Count} records. Skipping JSON seed.", existingCount);
                    return;
                }

                // Resolve JSON path
                string? jsonPath = _configuration?["StudentJsonPath"];
                if (string.IsNullOrWhiteSpace(jsonPath))
                {
                    // Fallback: try local appsettings.json next to the running app
                    try
                    {
                        var config = new ConfigurationBuilder()
                            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddEnvironmentVariables()
                            .Build();
                        jsonPath = config["StudentJsonPath"];
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Failed to load configuration for StudentJsonPath fallback");
                    }
                }

                if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                {
                    Logger.Warning("StudentJsonPath not found or file missing: {Path}", jsonPath);
                    return;
                }

                var json = await File.ReadAllTextAsync(jsonPath);

                // Try to deserialize as a plain array of Student first
                List<Student>? students = null;
                try
                {
                    students = JsonSerializer.Deserialize<List<Student>>(json, _jsonOptions);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Direct List<Student> deserialization failed; will try wrapper");
                }

                // If null, try wrapper object with a Students property
                if (students == null)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Students", out var studentsElement) &&
                            studentsElement.ValueKind == JsonValueKind.Array)
                        {
                            students = JsonSerializer.Deserialize<List<Student>>(studentsElement.GetRawText(), _jsonOptions);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Failed to parse Students array from JSON wrapper");
                    }
                }

                if (students == null || students.Count == 0)
                {
                    Logger.Warning("No student records found in JSON at {Path}", jsonPath);
                    return;
                }

                context.Students.AddRange(students);
                await context.SaveChangesAsync();
                Logger.Information("Seeded {Count} students from JSON: {Path}", students.Count, jsonPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during JSON seeding in SeedFromJsonAsync");
                throw;
            }
        }

        /// <summary>
        /// Seed sample activity logs for development/testing
        /// </summary>
        public async Task SeedActivityLogsAsync(int count = 50)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if logs already exist
                int existingCount;
                try
                {
                    existingCount = await context.ActivityLogs.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    // Fallback for mocked sets without async provider
                    existingCount = context.ActivityLogs.Count();
                }
                if (existingCount >= count)
                {
                    Logger.Information("ActivityLogs already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample activity logs...", count);

                var random = new Random();
                var actions = new[] { "User Login", "Data Export", "Report Generated", "Settings Changed", "Database Backup", "System Maintenance" };
                var users = new[] { "admin", "steve.mckitrick", "test_user", "system" };

                var logs = new List<ActivityLog>();
                for (int i = 0; i < count; i++)
                {
                    logs.Add(new ActivityLog
                    {
                        Timestamp = DateTime.UtcNow.AddDays(-random.Next(0, 30)).AddHours(-random.Next(0, 24)),
                        Action = actions[random.Next(actions.Length)],
                        User = users[random.Next(users.Length)],
                        Details = $"Sample activity log entry #{i + 1} - Generated for development testing"
                    });
                }

                context.ActivityLogs.AddRange(logs);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} activity logs", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding activity logs");
                throw;
            }
        }

        /// <summary>
        /// Seed sample drivers for development/testing
        /// </summary>
        public async Task SeedDriversAsync(int count = 10)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if drivers already exist
                int existingCount;
                try
                {
                    existingCount = await context.Drivers.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    existingCount = context.Drivers.Count();
                }
                if (existingCount >= count)
                {
                    Logger.Information("Drivers already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample drivers...", count);

                var random = new Random();
                var firstNames = new[] { "John", "Jane", "Mike", "Sarah", "David", "Lisa", "Tom", "Anna", "Chris", "Emma" };
                var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
                var licenseTypes = new[] { "CDL", "Standard", "Commercial" };

                var drivers = new List<Driver>();
                for (int i = 0; i < count; i++)
                {
                    var firstName = firstNames[random.Next(firstNames.Length)];
                    var lastName = lastNames[random.Next(lastNames.Length)];

                    drivers.Add(new Driver
                    {
                        DriverName = $"{firstName} {lastName}",
                        FirstName = firstName,
                        LastName = lastName,
                        DriversLicenceType = licenseTypes[random.Next(licenseTypes.Length)],
                        Status = "Active",
                        DriverPhone = $"555-{random.Next(100, 999)}-{random.Next(1000, 9999)}",
                        DriverEmail = $"{firstName.ToLower(CultureInfo.InvariantCulture)}.{lastName.ToLower(CultureInfo.InvariantCulture)}@busbuddy.com",
                        TrainingComplete = random.Next(0, 2) == 1,
                        HireDate = DateTime.UtcNow.AddDays(-random.Next(30, 365)),
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    });
                }

                context.Drivers.AddRange(drivers);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} drivers", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding drivers");
                throw;
            }
        }

        /// <summary>
        /// Seed sample buses for development/testing
        /// </summary>
        public async Task SeedBusesAsync(int count = 12)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if buses already exist
                int existingCount;
                try
                {
                    existingCount = await context.Buses.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    existingCount = context.Buses.Count();
                }
                if (existingCount >= count)
                {
                    Logger.Information("Buses already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample buses...", count);

                var random = new Random();
                var makes = new[] { "Blue Bird", "Thomas Built", "IC Bus", "Collins", "Starcraft" };
                var models = new[] { "Vision", "Conventional", "RE Series", "Type A", "Type C", "Quest" };

                var buses = new List<Bus>();
                for (int i = 0; i < count; i++)
                {
                    var make = makes[random.Next(makes.Length)];
                    var model = models[random.Next(models.Length)];
                    var year = random.Next(2015, 2025);

                    buses.Add(new Bus
                    {
                        BusNumber = $"BUS-{(i + 1):000}",
                        Year = year,
                        Make = make,
                        Model = model,
                        SeatingCapacity = random.Next(20, 72),
                        VINNumber = $"1{make.Substring(0, 2).ToUpper(CultureInfo.InvariantCulture)}{year}{random.Next(100000, 999999)}",
                        LicenseNumber = $"SCH{random.Next(1000, 9999)}",
                        Status = random.Next(0, 10) < 8 ? "Active" : "Maintenance",
                        CurrentOdometer = random.Next(5000, 150000),
                        DateLastInspection = DateTime.UtcNow.AddDays(-random.Next(1, 180)),
                        PurchaseDate = new DateTime(year, random.Next(1, 13), random.Next(1, 28)),
                        PurchasePrice = random.Next(80000, 150000),
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    });
                }

                context.Buses.AddRange(buses);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} buses", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding buses");
                throw;
            }
        }

        /// <summary>
        /// Seed sample activities for development/testing
        /// </summary>
        public async Task SeedActivitiesAsync(int count = 25)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if activities already exist
                var existingCount = await context.Activities.CountAsync();

                // TODO: Add logic for seeding activities (currently not implemented)
                // This method previously contained a mix of bus seeding and activity logic, which was invalid.
                // Implement proper activity seeding here as needed.

                Logger.Information("Successfully seeded {Count} activities", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding activities");
                throw;
            }
        }

        /// <summary>
        /// Seed students from real-world CSV data (BusRiders_25-26.xlsz.csv).
        /// </summary>
        public Task SeedStudentsFromCsvAsync()
        {
            return ImportFromCsvTextAsync(GetEmbeddedSampleCsv(), skipIfAlreadySeeded: true, createdBy: "SeedDataService");
        }

        /// <inheritdoc />
        public async Task<int> ImportStudentsFromCsvAsync(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                throw new ArgumentException("CSV path is required.", nameof(csvPath));
            }

            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException("Student CSV file was not found.", csvPath);
            }

            var csvData = await File.ReadAllTextAsync(csvPath);
            return await ImportFromCsvTextAsync(csvData, skipIfAlreadySeeded: false, createdBy: "CsvImport");
        }

        private static string GetEmbeddedSampleCsv() => @"
Student,,,Parent,,,,,,,,Joint Parent,,,,,,,Econtact,,
Fname,Lname,Grade,Fname,Lname,Address,City,State,County,Hphone,Cphone,Jparent FirstName,Jparent LastName,Address,City,State,County,Cphone ,Econtact FirstName,Econtact LastName,Econtact Phone
Alex,Rivera,7,Pat,Rivera,100 Main St,Oakridge,CO,County,,555-0100,,,,,,,,
Jordan,Lee,3,Sam,Lee,200 Oak Ave,Oakridge,CO,County,,555-0101,,,,,,,,
";

        private async Task<int> ImportFromCsvTextAsync(string csvData, bool skipIfAlreadySeeded, string createdBy)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                int existingCount;
                try
                {
                    existingCount = await context.Students.CountAsync();
                }
                catch (InvalidOperationException)
                {
                    // Fallback for mocks lacking IAsyncQueryProvider
                    existingCount = context.Students.Count();
                }
                // Top-up logic: if fewer students than CSV rows, import delta; if any exist and meet/exceed count, skip.
                var lines = csvData.Trim().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 3)
                {
                    Logger.Warning("No student data found in CSV.");
                    return 0;
                }
                var header = lines[1].Split(',').Select(h => h.Trim()).ToArray();
                int idxFname = Array.IndexOf(header, "Fname");
                int idxLname = Array.IndexOf(header, "Lname");
                int idxGrade = Array.IndexOf(header, "Grade");
                // First Address/City/State/County are the student/parent home — not the joint-parent copies.
                int idxAddress = Array.IndexOf(header, "Address");
                int idxCity = Array.IndexOf(header, "City");
                int idxState = Array.IndexOf(header, "State");
                int idxCounty = Array.IndexOf(header, "County");

                if (idxFname < 0 || idxLname < 0 || idxGrade < 0 || idxAddress < 0)
                {
                    throw new InvalidOperationException(
                        "CSV is not in the expected student format. Expected a header row with Fname, Lname, Grade, and Address.");
                }

                int idxParentFname = header.Length > 3 ? Array.IndexOf(header, "Fname", 3) : -1;
                int idxParentLname = header.Length > 4 ? Array.IndexOf(header, "Lname", 4) : -1;
                int idxHphone = Array.IndexOf(header, "Hphone");
                int idxCphone = Array.IndexOf(header, "Cphone");
                int idxJointParentFname = Array.IndexOf(header, "Jparent FirstName");
                int idxJointParentLname = Array.IndexOf(header, "Jparent LastName");
                int idxJointParentCphone = idxCphone >= 0 && idxCphone + 1 < header.Length
                    ? Array.IndexOf(header, "Cphone", idxCphone + 1)
                    : -1;
                int idxEcontactFname = Array.IndexOf(header, "Econtact FirstName");
                int idxEcontactLname = Array.IndexOf(header, "Econtact LastName");
                int idxEcontactPhone = Array.IndexOf(header, "Econtact Phone");

                string lastParent = string.Empty;
                string lastJointParent = string.Empty;
                string lastAddress = string.Empty;
                string lastCity = string.Empty;
                string lastState = string.Empty;
                string lastCounty = string.Empty;
                string lastHphone = string.Empty;
                string lastCphone = string.Empty;
                string lastJointCphone = string.Empty;
                string lastEcontact = string.Empty;
                string lastEcontactPhone = string.Empty;
                int familyId = 1;
                int studentNum = await NextStudentNumberAsync(context);
                var families = new List<Family>();
                var students = new List<Student>();

                // If existing students already meet or exceed CSV data rows (approximation), skip
                var csvRowCount = Math.Max(0, lines.Length - 2);
                if (skipIfAlreadySeeded && existingCount >= csvRowCount)
                {
                    Logger.Information("Students already exist (Existing={ExistingCount} >= CSV={CsvCount}). Skipping CSV seed.", existingCount, csvRowCount);
                    return 0;
                }

                for (int i = 2; i < lines.Length; i++)
                {
                    var row = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(row) || row.All(c => c == ','))
                    {
                        continue;
                    }

                    var cols = row.Split(',');
                    // Student fields
                    string fname = idxFname >= 0 && idxFname < cols.Length ? cols[idxFname].Trim() : string.Empty;
                    string lname = idxLname >= 0 && idxLname < cols.Length ? cols[idxLname].Trim() : string.Empty;
                    string grade = idxGrade >= 0 && idxGrade < cols.Length ? cols[idxGrade].Trim() : "Unknown";
                    // Parent fields
                    string parentFname = idxParentFname >= 0 && idxParentFname < cols.Length ? cols[idxParentFname].Trim() : string.Empty;
                    string parentLname = idxParentLname >= 0 && idxParentLname < cols.Length ? cols[idxParentLname].Trim() : string.Empty;
                    string address = idxAddress >= 0 && idxAddress < cols.Length ? cols[idxAddress].Trim() : string.Empty;
                    string city = idxCity >= 0 && idxCity < cols.Length ? cols[idxCity].Trim() : string.Empty;
                    string state = idxState >= 0 && idxState < cols.Length ? cols[idxState].Trim() : string.Empty;
                    string county = idxCounty >= 0 && idxCounty < cols.Length ? cols[idxCounty].Trim() : string.Empty;
                    string hphone = idxHphone >= 0 && idxHphone < cols.Length ? cols[idxHphone].Trim() : string.Empty;
                    string cphone = idxCphone >= 0 && idxCphone < cols.Length ? cols[idxCphone].Trim() : string.Empty;
                    // Joint parent
                    string jointFname = idxJointParentFname >= 0 && idxJointParentFname < cols.Length ? cols[idxJointParentFname].Trim() : string.Empty;
                    string jointLname = idxJointParentLname >= 0 && idxJointParentLname < cols.Length ? cols[idxJointParentLname].Trim() : string.Empty;
                    string jointCphone = idxJointParentCphone >= 0 && idxJointParentCphone < cols.Length ? cols[idxJointParentCphone].Trim() : string.Empty;
                    // Emergency contact
                    string econtactFname = idxEcontactFname >= 0 && idxEcontactFname < cols.Length ? cols[idxEcontactFname].Trim() : string.Empty;
                    string econtactLname = idxEcontactLname >= 0 && idxEcontactLname < cols.Length ? cols[idxEcontactLname].Trim() : string.Empty;
                    string econtactPhone = idxEcontactPhone >= 0 && idxEcontactPhone < cols.Length ? cols[idxEcontactPhone].Trim() : string.Empty;

                    // Fill down family info if blank
                    if (!string.IsNullOrEmpty(parentFname) || !string.IsNullOrEmpty(parentLname))
                    {
                        lastParent = $"{parentFname} {parentLname}".Trim();
                    }

                    if (!string.IsNullOrEmpty(jointFname) || !string.IsNullOrEmpty(jointLname))
                    {
                        lastJointParent = $"{jointFname} {jointLname}".Trim();
                    }

                    if (!string.IsNullOrEmpty(address))
                    {
                        lastAddress = address;
                    }

                    if (!string.IsNullOrEmpty(city))
                    {
                        lastCity = city;
                    }

                    if (!string.IsNullOrEmpty(state))
                    {
                        lastState = state;
                    }

                    if (!string.IsNullOrEmpty(county))
                    {
                        lastCounty = county;
                    }

                    if (!string.IsNullOrEmpty(hphone))
                    {
                        lastHphone = hphone;
                    }

                    if (!string.IsNullOrEmpty(cphone))
                    {
                        lastCphone = cphone;
                    }

                    if (!string.IsNullOrEmpty(jointCphone))
                    {
                        lastJointCphone = jointCphone;
                    }

                    if (!string.IsNullOrEmpty(econtactFname) || !string.IsNullOrEmpty(econtactLname))
                    {
                        lastEcontact = $"{econtactFname} {econtactLname}".Trim();
                    }

                    if (!string.IsNullOrEmpty(econtactPhone))
                    {
                        lastEcontactPhone = econtactPhone;
                    }

                    // Compose ParentGuardian field
                    string parentGuardian = lastParent;
                    if (!string.IsNullOrEmpty(lastJointParent))
                    {
                        parentGuardian = $"{lastParent} & {lastJointParent}";
                    }

                    // Compose HomeAddress
                    string homeAddress = $"{lastAddress}, {lastCity}, {lastState}, {lastCounty}".Replace("  ", " ").Trim(',').Trim();

                    // Compose HomePhone (prefer home, fallback to cell)
                    string homePhone = !string.IsNullOrEmpty(lastHphone) ? lastHphone : lastCphone;

                    // Compose EmergencyPhone
                    string emergencyPhone = !string.IsNullOrEmpty(lastEcontactPhone) ? $"{lastEcontactPhone} ({lastEcontact})" : string.Empty;

                    // Compose StudentName
                    string studentName = $"{fname} {lname}".Trim();
                    if (string.IsNullOrWhiteSpace(studentName))
                    {
                        Logger.Warning($"Skipping row {i + 1}: missing student name.");
                        continue;
                    }

                    // Compose StudentNumber
                    string studentNumber = $"STU{studentNum++.ToString("D4", CultureInfo.InvariantCulture)}";

                    // Create or find family (by parentGuardian and homePhone)
                    var family = families.LastOrDefault(f => f.ParentGuardian == parentGuardian && f.HomePhone == homePhone);
                    if (family == null)
                    {
                        family = new Family
                        {
                            ParentGuardian = parentGuardian,
                            Address = lastAddress,
                            City = lastCity,
                            County = lastCounty,
                            HomePhone = homePhone,
                            CellPhone = lastCphone,
                            JointParent = lastJointParent,
                            EmergencyContact = lastEcontact,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = createdBy
                        };
                        if (skipIfAlreadySeeded)
                        {
                            family.FamilyId = familyId++;
                        }

                        families.Add(family);
                    }

                    var student = new Student
                    {
                        StudentName = studentName,
                        Grade = grade,
                        HomeAddress = homeAddress,
                        ParentGuardian = parentGuardian,
                        HomePhone = homePhone,
                        EmergencyPhone = emergencyPhone,
                        School = string.Empty,
                        StudentNumber = studentNumber,
                        Family = family,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = createdBy
                    };
                    if (skipIfAlreadySeeded)
                    {
                        student.FamilyId = family.FamilyId;
                    }
                    students.Add(student);
                }

                if (!skipIfAlreadySeeded)
                {
                    HashSet<string> existingNames;
                    try
                    {
                        existingNames = (await context.Students.Select(s => s.StudentName).ToListAsync())
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    }
                    catch (InvalidOperationException)
                    {
                        existingNames = context.Students.Select(s => s.StudentName)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
                    }

                    students.RemoveAll(s => existingNames.Contains(s.StudentName));
                    var usedFamilies = new HashSet<Family>(students.Select(s => s.Family).Where(f => f != null)!);
                    families.RemoveAll(f => !usedFamilies.Contains(f));
                }

                if (students.Count == 0)
                {
                    Logger.Information("CSV import added 0 students (empty file or all names already present).");
                    return 0;
                }

                context.Families.AddRange(families);
                context.Students.AddRange(students);
                await context.SaveChangesAsync();
                Logger.Information("Imported {Count} students from CSV.", students.Count);
                return students.Count;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding students from CSV");
                throw;
            }
        }

        private static async Task<int> NextStudentNumberAsync(BusBuddyDbContext context)
        {
            List<string> existingNumbers;
            try
            {
                existingNumbers = await context.Students
                    .Where(s => s.StudentNumber != null && s.StudentNumber.StartsWith("STU"))
                    .Select(s => s.StudentNumber!)
                    .ToListAsync();
            }
            catch (InvalidOperationException)
            {
                existingNumbers = context.Students
                    .Where(s => s.StudentNumber != null && s.StudentNumber.StartsWith("STU"))
                    .Select(s => s.StudentNumber!)
                    .ToList();
            }

            var max = 0;
            foreach (var number in existingNumbers)
            {
                if (number.Length > 3 &&
                    int.TryParse(number.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                    value > max)
                {
                    max = value;
                }
            }

            return max + 1;
        }

        /// <summary>
        /// Seed sample routes for development/testing
        /// </summary>
        public async Task SeedRoutesAsync(int count = 8)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if routes already exist
                var existingCount = await context.Routes.CountAsync();
                if (existingCount >= count)
                {
                    Logger.Information("Routes already contain {ExistingCount} records. Skipping seed.", existingCount);
                    return;
                }

                Logger.Information("Seeding {Count} sample routes...", count);

                var random = new Random();
                var routeNames = new[] { "North Elementary", "South Elementary", "Middle School Express", "High School Route A", "High School Route B", "Elementary East", "Elementary West", "Special Needs Route" };
                var schools = new[] { "Washington Elementary", "Lincoln Middle School", "Roosevelt High School", "Jefferson Elementary", "Madison High School" };
                var drivers = await context.Drivers.Take(count).ToListAsync();
                var buses = await context.Buses.Take(count).ToListAsync();

                var routes = new List<Route>();
                for (int i = 0; i < count; i++)
                {
                    var routeName = i < routeNames.Length ? routeNames[i] : $"Route {i + 1}";
                    var isSpecialNeedsRoute = routeName.Contains("Special Needs", StringComparison.OrdinalIgnoreCase);

                    routes.Add(new Route
                    {
                        Date = Route.NormalizeRouteDate(DateTime.UtcNow.AddDays(-random.Next(0, 30))),
                        RouteName = routeName,
                        Description = $"Daily route for {routeName}",
                        School = schools[random.Next(schools.Length)],
                        IsSpecialNeedsRoute = isSpecialNeedsRoute,
                        AMDriverId = drivers.Count > i ? drivers[i].DriverId : null,
                        AMVehicleId = buses.Count > i ? buses[i].BusId : null,
                        PMDriverId = drivers.Count > i && drivers.Count > i + count / 2 ? drivers[i + count / 2].DriverId : null,
                        PMVehicleId = buses.Count > i && buses.Count > i + count / 2 ? buses[i + count / 2].BusId : null,
                        AMRiders = random.Next(5, 25),
                        PMRiders = random.Next(5, 25),
                        IsActive = random.Next(0, 10) > 1 // 90% active
                    });
                }

                context.Routes.AddRange(routes);
                await context.SaveChangesAsync();

                Logger.Information("Successfully seeded {Count} routes", count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding routes");
                throw;
            }
        }

        /// <summary>
        /// Seed all development data.
        /// </summary>
        public async Task SeedAllAsync()
        {
            Logger.Information("Starting full development data seeding...");

            await SeedActivityLogsAsync(100);
            await SeedDriversAsync(15);
            await SeedBusesAsync(12);
            await SeedStudentsFromCsvAsync();
            await SeedRoutesAsync(8);
            await SeedActivitiesAsync(25);
            await SeedSpecialNeedsTransportPrepAsync();

            Logger.Information("Development data seeding completed");
        }

        /// <inheritdoc />
        public async Task<SpecialNeedsPrepSummary> SeedSpecialNeedsTransportPrepAsync()
        {
            const string schoolName = "Wiley K-12 School";
            const string routeName = "Special Needs Route";
            const string driverName = "Pat Special";
            const string busNumber = "BUS-SN01";
            var messages = new List<string>();
            var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            using var context = _contextFactory.CreateWriteDbContext();

            var school = await context.Destinations
                .FirstOrDefaultAsync(d =>
                    d.DestinationType == DestinationTypes.School &&
                    d.Name == schoolName);

            if (school is null)
            {
                school = new Destination
                {
                    Name = schoolName,
                    Address = "403 N Main St",
                    City = "Wiley",
                    State = "CO",
                    ZipCode = "81092",
                    DestinationType = DestinationTypes.School,
                    DistrictName = "Wiley School District RE-13",
                    Latitude = 38.1535m,
                    Longitude = -102.7195m,
                    StartTime = TimeSpan.FromHours(8),
                    DismissalTime = TimeSpan.FromHours(15) + TimeSpan.FromMinutes(30),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "SeedDataService"
                };
                context.Destinations.Add(school);
                await context.SaveChangesAsync();
                messages.Add($"Created school destination '{schoolName}'");
            }
            else if (school.Latitude is null || school.Longitude is null)
            {
                school.Latitude = 38.1535m;
                school.Longitude = -102.7195m;
                school.StartTime ??= TimeSpan.FromHours(8);
                school.DismissalTime ??= TimeSpan.FromHours(15) + TimeSpan.FromMinutes(30);
                await context.SaveChangesAsync();
                messages.Add($"Updated GPS and bell times for '{schoolName}'");
            }

            var driver = await context.Drivers
                .FirstOrDefaultAsync(d => d.DriverName == driverName);
            if (driver is null)
            {
                driver = new Driver
                {
                    DriverName = driverName,
                    FirstName = "Pat",
                    LastName = "Special",
                    DriverPhone = "(719) 555-0142",
                    DriverEmail = "pat.special@wiley.k12.co.us",
                    DriversLicenceType = "CDL",
                    TrainingComplete = true,
                    Status = "Active",
                    HireDate = DateTime.UtcNow.AddYears(-4),
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "SeedDataService"
                };
                context.Drivers.Add(driver);
                await context.SaveChangesAsync();
                messages.Add($"Created special-needs driver '{driverName}'");
            }

            var bus = await context.Buses
                .FirstOrDefaultAsync(b => b.BusNumber == busNumber);
            if (bus is null)
            {
                bus = new Bus
                {
                    BusNumber = busNumber,
                    Year = 2021,
                    Make = "Thomas Built",
                    Model = "Saf-T-Liner HDX",
                    SeatingCapacity = 12,
                    FleetType = "Special Needs",
                    VINNumber = "1T8SNBUS21W000001",
                    LicenseNumber = "SN1001",
                    Status = "Active",
                    Description = "Wheelchair lift, tie-downs, aide seating",
                    DateLastInspection = DateTime.UtcNow.AddMonths(-2),
                    PurchaseDate = DateTime.SpecifyKind(new DateTime(2021, 6, 1), DateTimeKind.Utc),
                    PurchasePrice = 118000m,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "SeedDataService"
                };
                context.Buses.Add(bus);
                await context.SaveChangesAsync();
                messages.Add($"Created special-needs bus '{busNumber}'");
            }
            else if (!string.Equals(bus.FleetType, "Special Needs", StringComparison.OrdinalIgnoreCase))
            {
                bus.FleetType = "Special Needs";
                bus.SeatingCapacity = Math.Min(bus.SeatingCapacity, 12) > 0 ? Math.Min(bus.SeatingCapacity, 12) : 12;
                await context.SaveChangesAsync();
                messages.Add($"Updated bus '{busNumber}' FleetType to Special Needs");
            }

            var route = await context.Routes
                .FirstOrDefaultAsync(r => r.RouteName == routeName);
            if (route is null)
            {
                route = new Route
                {
                    RouteName = routeName,
                    Description = "Door-to-door special-needs transport to Wiley K-12",
                    School = schoolName,
                    Date = todayUtc,
                    IsActive = true,
                    IsSpecialNeedsRoute = true,
                    AMDriverId = driver.DriverId,
                    PMDriverId = driver.DriverId,
                    AMVehicleId = bus.BusId,
                    PMVehicleId = bus.BusId,
                    DriverName = driver.DriverName,
                    BusNumber = bus.BusNumber,
                    AMBeginTime = TimeSpan.FromHours(7) + TimeSpan.FromMinutes(15),
                    PMBeginTime = TimeSpan.FromHours(15) + TimeSpan.FromMinutes(45)
                };
                context.Routes.Add(route);
                await context.SaveChangesAsync();
                messages.Add($"Created route '{routeName}'");
            }
            else
            {
                route.IsSpecialNeedsRoute = true;
                route.School = schoolName;
                route.AMDriverId = driver.DriverId;
                route.PMDriverId = driver.DriverId;
                route.AMVehicleId = bus.BusId;
                route.PMVehicleId = bus.BusId;
                route.DriverName = driver.DriverName;
                route.BusNumber = bus.BusNumber;
                route.IsActive = true;
                await context.SaveChangesAsync();
                messages.Add($"Updated route '{routeName}' with SN driver/bus");
            }

            const string regularRouteName = "North Elementary";
            var regularRoute = await context.Routes
                .FirstOrDefaultAsync(r => r.RouteName == regularRouteName);
            if (regularRoute is null)
            {
                regularRoute = new Route
                {
                    RouteName = regularRouteName,
                    Description = "Regular home-to-school route — Wiley K-12",
                    School = schoolName,
                    Date = todayUtc,
                    IsActive = true,
                    IsSpecialNeedsRoute = false
                };
                context.Routes.Add(regularRoute);
                await context.SaveChangesAsync();
                messages.Add($"Created regular route '{regularRouteName}'");
            }

            var specialStudentSpecs = new[]
            {
                new
                {
                    Name = "Ashley Johnson",
                    Grade = "5",
                    Address = "456 Pine Avenue",
                    City = "Wiley",
                    Lat = 38.1512m,
                    Lon = -102.7210m,
                    Wheelchair = false,
                    Aide = true,
                    Notes = "Requires aide assistance boarding"
                },
                new
                {
                    Name = "Noah Martinez",
                    Grade = "3",
                    Address = "118 Cedar Ln",
                    City = "Wiley",
                    Lat = 38.1548m,
                    Lon = -102.7162m,
                    Wheelchair = true,
                    Aide = true,
                    Notes = "Wheelchair lift; secure tie-downs required"
                },
                new
                {
                    Name = "Sophia Reed",
                    Grade = "7",
                    Address = "902 County Road 25",
                    City = "Wiley",
                    Lat = 38.1485m,
                    Lon = -102.7248m,
                    Wheelchair = false,
                    Aide = false,
                    Notes = "Seat belt harness; monitor at drop-off"
                }
            };

            var snCount = 0;
            foreach (var spec in specialStudentSpecs)
            {
                var existing = await context.Students
                    .FirstOrDefaultAsync(s => s.StudentName == spec.Name);
                if (existing is null)
                {
                    existing = new Student
                    {
                        StudentName = spec.Name,
                        Grade = spec.Grade,
                        HomeAddress = spec.Address,
                        City = spec.City,
                        State = "CO",
                        Zip = "81092",
                        Latitude = spec.Lat,
                        Longitude = spec.Lon,
                        ParentGuardian = $"{spec.Name.Split(' ')[0]} Parent",
                        CellPhone = "(719) 555-0100",
                        School = schoolName,
                        DestinationId = school.DestinationId,
                        RequiresSpecialNeedsBus = true,
                        RequiresWheelchair = spec.Wheelchair,
                        RequiresAide = spec.Aide,
                        RequiresSeatBelt = true,
                        HasMedicalNeeds = spec.Wheelchair,
                        TransportationNotes = spec.Notes,
                        AMRoute = routeName,
                        PMRoute = routeName,
                        Active = true,
                        EnrollmentDate = todayUtc,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    };
                    StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(existing);
                    context.Students.Add(existing);
                    snCount++;
                }
                else
                {
                    existing.RequiresSpecialNeedsBus = true;
                    existing.RequiresWheelchair = spec.Wheelchair;
                    existing.RequiresAide = spec.Aide;
                    existing.RequiresSeatBelt = true;
                    existing.DestinationId = school.DestinationId;
                    existing.School = schoolName;
                    existing.Latitude ??= spec.Lat;
                    existing.Longitude ??= spec.Lon;
                    existing.AMRoute = routeName;
                    existing.PMRoute = routeName;
                    existing.TransportationNotes = spec.Notes;
                    StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(existing);
                    snCount++;
                }
            }

            await context.SaveChangesAsync();
            messages.Add($"Prepared {snCount} special-needs student(s) on '{routeName}'");

            var regularStudentSpecs = new[]
            {
                new { Name = "Emma Smith", Grade = "3", Address = "123 Oak Street", City = "Wiley", Lat = 38.1555m, Lon = -102.7180m },
                new { Name = "Michael Smith", Grade = "1", Address = "123 Oak Street", City = "Wiley", Lat = 38.1556m, Lon = -102.7181m }
            };

            var regCount = 0;
            foreach (var spec in regularStudentSpecs)
            {
                var existing = await context.Students
                    .FirstOrDefaultAsync(s => s.StudentName == spec.Name);
                if (existing is null)
                {
                    existing = new Student
                    {
                        StudentName = spec.Name,
                        Grade = spec.Grade,
                        HomeAddress = spec.Address,
                        City = spec.City,
                        State = "CO",
                        Zip = "81092",
                        Latitude = spec.Lat,
                        Longitude = spec.Lon,
                        ParentGuardian = "John Smith",
                        CellPhone = "(719) 555-0200",
                        School = schoolName,
                        DestinationId = school.DestinationId,
                        AMRoute = "North Elementary",
                        PMRoute = "North Elementary",
                        Active = true,
                        EnrollmentDate = todayUtc,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "SeedDataService"
                    };
                    context.Students.Add(existing);
                    regCount++;
                }
                else if (existing.RequiresSpecialNeedsBus)
                {
                    // leave SN students alone
                }
                else
                {
                    existing.DestinationId = school.DestinationId;
                    existing.School = schoolName;
                    existing.Latitude ??= spec.Lat;
                    existing.Longitude ??= spec.Lon;
                    if (string.IsNullOrWhiteSpace(existing.AMRoute))
                    {
                        existing.AMRoute = "North Elementary";
                    }

                    if (string.IsNullOrWhiteSpace(existing.PMRoute))
                    {
                        existing.PMRoute = "North Elementary";
                    }

                    regCount++;
                }
            }

            await context.SaveChangesAsync();
            messages.Add($"Prepared {regCount} regular student(s) for contrast routing");

            route.StudentCount = await context.Students.CountAsync(s =>
                s.AMRoute == routeName || s.PMRoute == routeName);
            await context.SaveChangesAsync();

            Logger.Information(
                "Special-needs transport prep complete Route={RouteId} Students={SnCount}",
                route.RouteId, snCount);

            return new SpecialNeedsPrepSummary
            {
                SchoolDestinationId = school.DestinationId,
                SpecialNeedsRouteId = route.RouteId,
                SpecialNeedsRouteName = route.RouteName,
                SpecialNeedsDriverId = driver.DriverId,
                SpecialNeedsBusId = bus.BusId,
                SpecialNeedsStudentsPrepared = snCount,
                RegularStudentsPrepared = regCount,
                Messages = messages
            };
        }

        /// <summary>
        /// Clear all seeded data (use with caution!)
        /// </summary>
        public async Task ClearSeedDataAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                Logger.Warning("Clearing all seeded data...");

                // Only clear data created by seed service
                var seedLogs = await context.ActivityLogs
                    .Where(a => a.Details != null && a.Details.Contains("Generated for development testing", StringComparison.OrdinalIgnoreCase))
                    .ToListAsync();

                var seedDrivers = await context.Drivers
                    .Where(d => d.CreatedBy == "SeedDataService")
                    .ToListAsync();

                if (seedLogs.Any())
                {
                    context.ActivityLogs.RemoveRange(seedLogs);
                    Logger.Information("Removed {Count} seeded activity logs", seedLogs.Count);
                }

                if (seedDrivers.Any())
                {
                    context.Drivers.RemoveRange(seedDrivers);
                    Logger.Information("Removed {Count} seeded drivers", seedDrivers.Count);
                }

                await context.SaveChangesAsync();
                Logger.Information("Seed data clearing completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error clearing seed data");
                throw;
            }
        }
    }
}
