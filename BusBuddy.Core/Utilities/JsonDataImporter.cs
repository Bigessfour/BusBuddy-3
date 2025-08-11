using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Service for importing JSON student data into BusBuddy database
    /// Handles family and student data from extracted form data
    /// Follows BusBuddy coding standards with comprehensive error handling and logging
    /// </summary>
    public class JsonDataImporter
    {
        private readonly BusBuddy.Core.Data.BusBuddyDbContext _context;
        private static readonly ILogger Logger = Log.ForContext<JsonDataImporter>();

        // Cache JsonSerializerOptions to avoid creating new instances for every operation
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public JsonDataImporter(BusBuddyDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Imports student and family data from JSON file into the database
        /// Skips existing records to prevent duplicates
        /// </summary>
        /// <param name="context">Database context for data operations</param>
        /// <param name="jsonFilePath">Path to the JSON file containing family/student data</param>
        /// <returns>Import statistics and results</returns>
        public static async Task<JsonImportResult> ImportStudentDataAsync(BusBuddyDbContext context, string jsonFilePath)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(jsonFilePath);

            var result = new JsonImportResult();
            try
            {
                // Try to resolve the JSON path from appsettings.json if available
                string resolvedPath = jsonFilePath;
                try
                {
                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    if (File.Exists(configPath))
                    {
                        var configJson = File.ReadAllText(configPath);
                        using var configDoc = JsonDocument.Parse(configJson);
                        if (configDoc.RootElement.TryGetProperty("WileyJsonPath", out var wileyPathProp))
                        {
                            var overridePath = wileyPathProp.GetString();
                            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
                            {
                                resolvedPath = overridePath;
                                Logger.Information("Using WileyJsonPath override from appsettings.json: {Path}", resolvedPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to read WileyJsonPath from appsettings.json, falling back to default path.");
                }

                // Fallback to absolute path if file not found
                if (!File.Exists(resolvedPath))
                {
                    var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "BusBuddy.Core", "Data", "wiley-school-district-data.json");
                    if (File.Exists(fallbackPath))
                    {
                        resolvedPath = Path.GetFullPath(fallbackPath);
                        Logger.Information("Using fallback Wiley JSON path: {Path}", resolvedPath);
                    }
                }

                Logger.Information("Starting JSON student data import from: {FilePath}", resolvedPath);
                if (!File.Exists(resolvedPath))
                {
                    var errorMessage = $"JSON file not found: {resolvedPath}";
                    Logger.Error(errorMessage);
                    result.ErrorMessages.Add(errorMessage);
                    result.Success = false;
                    return result;
                }
                var jsonContent = await File.ReadAllTextAsync(resolvedPath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    var errorMessage = "JSON file is empty or contains only whitespace";
                    Logger.Error(errorMessage);
                    result.ErrorMessages.Add(errorMessage);
                    result.Success = false;
                    return result;
                }
                // Handle Wiley JSON structure: top-level object with families and students arrays
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                var families = new List<FamilyImportDto>();

                if (root.TryGetProperty("families", out var familiesElement) && familiesElement.ValueKind == JsonValueKind.Array
                    && root.TryGetProperty("students", out var studentsElement) && studentsElement.ValueKind == JsonValueKind.Array)
                {
                    // Classic format: families[] + students[] with familyId mapping
            foreach (var fam in familiesElement.EnumerateArray())
                    {
                        var family = new FamilyImportDto
                        {
                Id = fam.TryGetProperty("id", out var famId) ? famId.GetInt32() : (int?)null,
                            ParentGuardian = fam.GetProperty("parentGuardian").GetString() ?? string.Empty,
                            Address = fam.GetProperty("address").GetString() ?? string.Empty,
                            City = fam.GetProperty("city").GetString() ?? string.Empty,
                            County = fam.GetProperty("county").GetString() ?? string.Empty,
                            HomePhone = fam.TryGetProperty("homePhone", out var hp) ? hp.GetString() : null,
                            CellPhone = fam.TryGetProperty("cellPhone", out var cp) ? cp.GetString() : null,
                            EmergencyContact = fam.TryGetProperty("emergencyContact", out var ec) ? ec.GetString() : null,
                            JointParent = fam.TryGetProperty("jointParent", out var jp) ? jp.GetString() : null,
                            Students = new List<StudentImportDto>()
                        };
                        families.Add(family);
                    }

                    // Deserialize students and group by familyId
                    try
                    {
                        foreach (var stu in studentsElement.EnumerateArray())
                        {
                            StudentImportDto student = null!;
                            try
                            {
                                student = JsonSerializer.Deserialize<StudentImportDto>(stu.GetRawText(), SerializerOptions)!;
                            }
                            catch (JsonException ex)
                            {
                                Logger.Error(ex, "JSON deserialization error (student): {Message}", ex.Message);
                                result.ErrorMessages.Add($"JSON deserialization error (student): {ex.Message}");
                                continue;
                            }
                            // Primary: map via familyId when available
                            int? familyId = stu.TryGetProperty("familyId", out var fid) ? fid.GetInt32() : (int?)null;
                            var family = families.FirstOrDefault(f => f.Id.HasValue && familyId.HasValue && f.Id.Value == familyId.Value);

                            // Fallback: map by ParentGuardian + Address when familyId is missing or unmatched
                            if (family == null)
                            {
                                // Try to read potential parent/address fields directly from the student node
                                string? parentFromStudent = null;
                                string? addressFromStudent = null;
                                try
                                {
                                    // Common keys seen in extracted forms
                                    if (stu.TryGetProperty("ParentGuardian", out var pg)) parentFromStudent = pg.GetString();
                                    if (string.IsNullOrWhiteSpace(parentFromStudent) && stu.TryGetProperty("parentGuardian", out var pg2)) parentFromStudent = pg2.GetString();
                                    if (stu.TryGetProperty("HomeAddress", out var ha)) addressFromStudent = ha.GetString();
                                    if (string.IsNullOrWhiteSpace(addressFromStudent) && stu.TryGetProperty("address", out var ha2)) addressFromStudent = ha2.GetString();
                                }
                                catch { /* ignore */ }

                                // Note: ParentGuardian and HomeAddress are family-level properties, not student properties
                                // They are handled at the family level during import processing

                                if (!string.IsNullOrWhiteSpace(parentFromStudent) && !string.IsNullOrWhiteSpace(addressFromStudent))
                                {
                                    family = families.FirstOrDefault(f => string.Equals(f.ParentGuardian, parentFromStudent, StringComparison.OrdinalIgnoreCase)
                                                                        && string.Equals(f.Address, addressFromStudent, StringComparison.OrdinalIgnoreCase));
                                }
                            }

                            if (family != null)
                            {
                                family.Students.Add(student);
                            }
                            else
                            {
                                Logger.Warning("Unassigned student during import (no familyId and no match by ParentGuardian/Address): {First} {Last}", student.FirstName, student.LastName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Unexpected error during student deserialization: {Message}", ex.Message);
                        result.ErrorMessages.Add($"Unexpected error during student deserialization: {ex.Message}");
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // New simplified format: array of student entries with nested Guardian
                    foreach (var stu in root.EnumerateArray())
                    {
                        // Build a synthetic family from Guardian
                        var guardian = stu.GetProperty("Guardian");
                        var parentName = $"{guardian.GetProperty("FirstName").GetString()} {guardian.GetProperty("LastName").GetString()}".Trim();
                        var address = guardian.GetProperty("Address").GetString() ?? string.Empty;
                        var city = guardian.GetProperty("City").GetString() ?? string.Empty;
                        var county = guardian.TryGetProperty("County", out var gcounty) ? gcounty.GetString() ?? string.Empty : string.Empty;
                        var state = guardian.TryGetProperty("State", out var gstate) ? gstate.GetString() : null;
                        var homePhone = guardian.TryGetProperty("HomePhone", out var ghp) ? ghp.GetString() : null;
                        var cellPhone = guardian.TryGetProperty("CellPhone", out var gcp) ? gcp.GetString() : null;

                        var famDto = families.FirstOrDefault(f => f.ParentGuardian == parentName && f.Address == address);
                        if (famDto == null)
                        {
                            famDto = new FamilyImportDto
                            {
                                ParentGuardian = parentName,
                                Address = address,
                                City = city,
                                State = state,
                                County = county,
                                HomePhone = homePhone,
                                CellPhone = cellPhone,
                                Students = new List<StudentImportDto>()
                            };
                            families.Add(famDto);
                        }

                        var studentDto = new StudentImportDto
                        {
                            FirstName = stu.GetProperty("FirstName").GetString() ?? string.Empty,
                            LastName = stu.GetProperty("LastName").GetString() ?? string.Empty,
                            Grade = stu.TryGetProperty("Grade", out var grd) ? grd.GetString() ?? string.Empty : string.Empty,
                            TransportationNotes = stu.TryGetProperty("School", out var sc) ? sc.GetString() : null
                        };
                        famDto.Students.Add(studentDto);
                    }
                }
                else
                {
                    var errorMessage = "Unsupported JSON structure for import";
                    Logger.Error(errorMessage);
                    result.ErrorMessages.Add(errorMessage);
                    result.Success = false;
                    return result;
                }

                foreach (var family in families)
                {
                    await ProcessFamilyAsync(context, family, result);
                }
                await context.SaveChangesAsync();
                result.Success = true;
                Logger.Information("JSON import completed: {StudentsAdded} students, {FamiliesAdded} families added", result.StudentsAdded, result.FamiliesAdded);
            }
            catch (JsonException jsonEx)
            {
                var errorMessage = $"JSON parsing error: {jsonEx.Message}";
                Logger.Error(jsonEx, "JSON parsing failed");
                result.ErrorMessages.Add(errorMessage);
                result.Success = false;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Import failed: {ex.Message}";
                Logger.Error(ex, "JSON import failed");
                result.ErrorMessages.Add(errorMessage);
                result.Success = false;
            }
            return result;
        }

        /// <summary>
        /// Processes a single family and its students
        /// </summary>
        // Accept WileyFamily for Wiley JSON import
        private static async Task ProcessFamilyAsync(BusBuddyDbContext context, FamilyImportDto familyDto, JsonImportResult result)
        {
            // Check if family already exists (by parent name and address)
            var existingFamily = await context.Families
                .Include(f => f.Students)
                .FirstOrDefaultAsync(f => f.ParentGuardian == familyDto.ParentGuardian && f.Address == familyDto.Address);

            Family family;
            if (existingFamily != null)
            {
                Logger.Debug("👨‍👩‍👧‍👦 Family already exists: {ParentGuardian} at {Address}", familyDto.ParentGuardian, familyDto.Address);
                family = existingFamily;
                result.SkippedFamilies++;
            }
            else
            {
                // Create new family
                family = new Family
                {
                    ParentGuardian = familyDto.ParentGuardian,
                    Address = familyDto.Address,
                    City = familyDto.City,
                    County = familyDto.County,
                    HomePhone = familyDto.HomePhone,
                    CellPhone = familyDto.CellPhone,
                    EmergencyContact = familyDto.EmergencyContact,
                    JointParent = familyDto.JointParent,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "JsonImporter"
                };

                context.Families.Add(family);
                result.ImportedFamilies++;
                Logger.Debug("➕ Added new family: {ParentGuardian} at {Address}", familyDto.ParentGuardian, familyDto.Address);
            }

            // Process students
            foreach (var studentDto in familyDto.Students)
            {
                await ProcessStudentAsync(context, studentDto, family, familyDto, result);
            }
        }

        /// <summary>
        /// Processes a single student
        /// </summary>
        private static async Task ProcessStudentAsync(BusBuddyDbContext context, StudentImportDto studentDto, Family family, FamilyImportDto familyDto, JsonImportResult result)
        {
            // Check if student already exists (by name and family)
            var fullName = $"{studentDto.FirstName} {studentDto.LastName}".Trim();
            var existingStudent = await context.Students
                .FirstOrDefaultAsync(s => s.StudentName == fullName && s.HomeAddress == familyDto.Address);

            if (existingStudent != null)
            {
                Logger.Debug("👨‍🎓 Student already exists: {StudentName}", fullName);
                result.SkippedStudents++;
                return;
            }

            // Create new student
            var student = new Student
            {
                StudentName = fullName,
                Grade = studentDto.Grade,
                HomeAddress = familyDto.Address,
                City = familyDto.City,
                State = string.IsNullOrWhiteSpace(familyDto.State) ? "CO" : familyDto.State,
                Zip = ExtractZipFromAddress(familyDto.Address),
                HomePhone = familyDto.HomePhone,
                ParentGuardian = familyDto.ParentGuardian,
                EmergencyPhone = familyDto.CellPhone ?? familyDto.HomePhone,
                DateOfBirth = studentDto.DateOfBirth,
                // Map special needs text directly
                SpecialNeeds = studentDto.SpecialNeeds ?? string.Empty,
                MedicalNotes = studentDto.MedicalNotes,
                Allergies = studentDto.Allergies,
                Medications = studentDto.Medications,
                TransportationNotes = CombineTransportationInfo(studentDto),
                AlternativeContact = familyDto.EmergencyContact,
                Active = true,
                EnrollmentDate = DateTime.UtcNow.Date,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "JsonImporter"
            };

            // Assign default route based on city
            // AssignDefaultRoute(student, familyDto.City); // Removed: function does not exist

            context.Students.Add(student);
            result.ImportedStudents++;
            Logger.Debug("➕ Added new student: {StudentName} in {Grade}", fullName, studentDto.Grade);
        }

        /// <summary>
        /// Attempts to extract ZIP code from address string
        /// </summary>
        private static string? ExtractZipFromAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return null;
            }

            // Look for 5-digit ZIP code pattern
            var zipMatch = System.Text.RegularExpressions.Regex.Match(address, @"\b\d{5}\b");
            return zipMatch.Success ? zipMatch.Value : null;
        }

        /// <summary>
        /// Combines transportation-related information from form data
        /// </summary>
        private static string CombineTransportationInfo(StudentImportDto studentDto)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(studentDto.FullTime))
            {
                parts.Add($"Schedule: {studentDto.FullTime}");
            }

            if (!string.IsNullOrEmpty(studentDto.Infrequently))
            {
                parts.Add($"Special Schedule: {studentDto.Infrequently}");
            }

            if (!string.IsNullOrEmpty(studentDto.TransportationNotes))
            {
                parts.Add(studentDto.TransportationNotes);
            }

            return string.Join("; ", parts);
        }

        /// <summary>
        /// Seeds the database with data if no students exist
        /// Called during application startup
        /// </summary>
        public static async Task SeedDatabaseIfEmptyAsync(BusBuddyDbContext context)
        {
            try
            {
                // Count existing students
                var existingCount = await context.Students.CountAsync();

                // Look for JSON data file in multiple locations
                var possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "wiley-school-district-data.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "enhanced-realworld-data.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "student-import-data.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "BusBuddy.Core", "Data", "wiley-school-district-data.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "BusBuddy.Core", "Data", "enhanced-realworld-data.json")
                };

                string? jsonFilePath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        jsonFilePath = path;
                        break;
                    }
                }

                // If no JSON is available
                if (jsonFilePath is null)
                {
                    if (existingCount == 0)
                    {
                        Logger.Warning("⚠️ No JSON data file found for seeding and database is empty — creating sample student");
                        await CreateSampleStudentAsync(context);
                    }
                    else
                    {
                        Logger.Information("📊 No JSON seed file found; database already has {Count} students — skipping top-up seeding", existingCount);
                    }
                    return;
                }

                // Read JSON to determine potential dataset size for top-up decisions
                int jsonStudentsCount = 0;
                try
                {
                    using var jsonDoc = JsonDocument.Parse(await File.ReadAllTextAsync(jsonFilePath));
                    if (jsonDoc.RootElement.TryGetProperty("students", out var studentsElem) && studentsElem.ValueKind == JsonValueKind.Array)
                    {
                        jsonStudentsCount = studentsElem.GetArrayLength();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to read student count from JSON seed file: {Path}", jsonFilePath);
                }

                // Decide whether to import:
                // - If DB is empty: perform full seed
                // - If DB has fewer students than JSON: perform top-up seed (dedupe ensures no duplicates)
                // - Otherwise: skip
                if (existingCount == 0)
                {
                    Logger.Information("🌱 Database is empty, attempting to seed with JSON data from {Path}", jsonFilePath);
                }
                else if (jsonStudentsCount > 0 && existingCount < jsonStudentsCount)
                {
                    Logger.Information("🌱 Detected partial data: existing students = {Existing}, JSON dataset = {Json}. Attempting top-up seeding from {Path}", existingCount, jsonStudentsCount, jsonFilePath);
                }
                else
                {
                    Logger.Information("📊 Database already contains {Existing} students; JSON dataset size = {Json}. Skipping seeding.", existingCount, jsonStudentsCount);
                    return;
                }

                // Import the JSON data (dedupe logic inside handles existing records)
                var result = await ImportStudentDataAsync(context, jsonFilePath);
                if (result.Success)
                {
                    Logger.Information("✅ Seeded from JSON: {Students} students added, {Families} families added (existing: {Existing})",
                        result.ImportedStudents, result.ImportedFamilies, existingCount);
                }
                else
                {
                    if (existingCount == 0)
                    {
                        Logger.Warning("⚠️ Failed to seed from JSON: {Error}, creating sample student", result.ErrorMessage);
                        await CreateSampleStudentAsync(context);
                    }
                    else
                    {
                        Logger.Warning("⚠️ Top-up seeding failed: {Error}. Leaving existing data untouched.", result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error during database seeding");
                // Don't throw - this shouldn't prevent app startup
            }
        }

        /// <summary>
        /// Creates a sample student if JSON import fails
        /// </summary>
        private static async Task CreateSampleStudentAsync(BusBuddyDbContext context)
        {
            var sampleStudent = new Student
            {
                StudentName = "Sample Student",
                Grade = "5",
                HomeAddress = "123 Main St, Springfield, PA 19064",
                City = "Springfield",
                State = "PA",
                Zip = "19064",
                ParentGuardian = "Sample Parent",
                HomePhone = "(610) 555-0100",
                Active = true,
                AMRoute = "Route-001-AM",
                PMRoute = "Route-001-PM",
                BusStop = "Main Street Stop",
                EnrollmentDate = DateTime.UtcNow.Date,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "SampleSeeder"
            };

            context.Students.Add(sampleStudent);
            await context.SaveChangesAsync();
            Logger.Information("➕ Created sample student for testing");
        }
    }

    /// <summary>
    /// Result object for JSON import operations
    /// </summary>
    public class JsonImportResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public string JsonFilePath { get; set; } = string.Empty;

        public int ImportedFamilies { get; set; }
        public int ImportedStudents { get; set; }
        public int SkippedFamilies { get; set; }
        public int SkippedStudents { get; set; }
        public int FailedFamilies { get; set; }

        public List<string> ErrorMessages { get; set; } = new List<string>();

        public int FamiliesAdded => ImportedFamilies;
        public int StudentsAdded => ImportedStudents;

        public string GetSummary()
        {
            return $"Import {(Success ? "Success" : "Failed")}: " +
                   $"{ImportedFamilies} families, {ImportedStudents} students imported. " +
                   $"{SkippedFamilies} families, {SkippedStudents} students skipped. " +
                   $"Duration: {Duration?.TotalSeconds:F1}s";
        }
    }

    /// <summary>
    /// DTO for importing family data from JSON
    /// </summary>
    public class FamilyImportDto
    {
    public int? Id { get; set; }
        public string ParentGuardian { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    public string? State { get; set; }
        public string County { get; set; } = string.Empty;
        public string? HomePhone { get; set; }
        public string? CellPhone { get; set; }
        public string? EmergencyContact { get; set; }
        public string? JointParent { get; set; }
        public List<StudentImportDto> Students { get; set; } = new List<StudentImportDto>();
    }

    /// <summary>
    /// DTO for importing student data from JSON
    /// </summary>
    public class StudentImportDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string? SpecialNeeds { get; set; }
        public string? MedicalNotes { get; set; }
        public string? Allergies { get; set; }
        public string? Medications { get; set; }
        public string? TransportationNotes { get; set; }
        public string? FullTime { get; set; }
        public string? Infrequently { get; set; }
    }
}
