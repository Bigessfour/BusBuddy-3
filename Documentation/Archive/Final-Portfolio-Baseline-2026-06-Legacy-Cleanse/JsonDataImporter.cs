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
    /// <summary>
    /// JSON-based student data importer.
    /// DEPRECATED for MVP: Will be replaced by CSV-based import leveraging Syncfusion CSV pipeline.
    /// </summary>
    [Obsolete("JsonDataImporter is deprecated for MVP. Use forthcoming CsvDataImporter.")]
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
                // Handle Wiley JSON structure with robust shape handling
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                // If the root is an array, import will proceed, but log a gentle warning to encourage the preferred wrapped format
                // Preferred shape: { "Students": [ ... ] } — this improves future schema evolution and validation
                if (root.ValueKind == JsonValueKind.Array)
                {
                    Logger.Warning("JSON root is an array; preferred format is an object with a 'Students' array for better validation and stability");
                }

                var families = new List<FamilyImportDto>();

                // Case 1: Top-level object with families[] and students[]
                if (root.ValueKind == JsonValueKind.Object &&
                    TryGetPropertyCaseInsensitive(root, "families", out var familiesElement) && familiesElement.ValueKind == JsonValueKind.Array &&
                    TryGetPropertyCaseInsensitive(root, "students", out var studentsElement) && studentsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fam in familiesElement.EnumerateArray())
                    {
                        if (fam.ValueKind != JsonValueKind.Object)
                            continue;

                        var family = new FamilyImportDto
                        {
                            Id = TryGetPropertyCaseInsensitive(fam, "id", out var famIdElem) && famIdElem.ValueKind == JsonValueKind.Number ? famIdElem.GetInt32() : (int?)null,
                            ParentGuardian = GetStringCaseInsensitive(fam, "parentGuardian", "ParentGuardian") ?? string.Empty,
                            Address = GetStringCaseInsensitive(fam, "address", "Address", "HomeAddress") ?? string.Empty,
                            City = GetStringCaseInsensitive(fam, "city", "City") ?? string.Empty,
                            County = GetStringCaseInsensitive(fam, "county", "County") ?? string.Empty,
                            HomePhone = GetStringCaseInsensitive(fam, "homePhone", "HomePhone"),
                            CellPhone = GetStringCaseInsensitive(fam, "cellPhone", "CellPhone"),
                            EmergencyContact = GetStringCaseInsensitive(fam, "emergencyContact", "EmergencyContact"),
                            JointParent = GetStringCaseInsensitive(fam, "jointParent", "JointParent"),
                            Students = new List<StudentImportDto>()
                        };
                        families.Add(family);
                    }

                    // Deserialize students and group by familyId
                    try
                    {
                        foreach (var stu in studentsElement.EnumerateArray())
                        {
                            if (stu.ValueKind != JsonValueKind.Object)
                                continue;

                            StudentImportDto student;
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
                            int? familyId = TryGetPropertyCaseInsensitive(stu, "familyId", out var fidElem) && fidElem.ValueKind == JsonValueKind.Number ? fidElem.GetInt32() : (int?)null;
                            var family = families.FirstOrDefault(f => f.Id.HasValue && familyId.HasValue && f.Id.Value == familyId.Value);

                            // Fallback: map by ParentGuardian + Address when familyId is missing or unmatched
                            if (family == null)
                            {
                                var parentFromStudent = GetStringCaseInsensitive(stu, "ParentGuardian", "parentGuardian");
                                var addressFromStudent = GetStringCaseInsensitive(stu, "HomeAddress", "address", "Address");

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
                // Case 2: Top-level object with Students[]
                else if (root.ValueKind == JsonValueKind.Object &&
                         TryGetPropertyCaseInsensitive(root, "Students", out var studentsArrayObj) && studentsArrayObj.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stu in studentsArrayObj.EnumerateArray())
                    {
                        ProcessStudentJsonElement(stu, families);
                    }
                }
                // Case 3: Top-level array of student entries (nested Guardian or flat)
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stu in root.EnumerateArray())
                    {
                        ProcessStudentJsonElement(stu, families);
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

        // Helpers for robust JSON parsing (case-insensitive)
        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
        {
            value = default;
            if (element.ValueKind != JsonValueKind.Object)
                return false;
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            return false;
        }

        private static string? GetStringCaseInsensitive(JsonElement element, params string[] names)
        {
            foreach (var n in names)
            {
                if (TryGetPropertyCaseInsensitive(element, n, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString();
            }
            return null;
        }

        private static void ProcessStudentJsonElement(JsonElement stu, List<FamilyImportDto> families)
        {
            if (stu.ValueKind != JsonValueKind.Object)
            {
                Logger.Warning("Skipping non-object student entry in JSON array");
                return;
            }

            // Try nested Guardian first
            if (TryGetPropertyCaseInsensitive(stu, "Guardian", out var guardian) && guardian.ValueKind == JsonValueKind.Object)
            {
                var parentName = $"{GetStringCaseInsensitive(guardian, "FirstName")} {GetStringCaseInsensitive(guardian, "LastName")}".Trim();
                var address = GetStringCaseInsensitive(guardian, "Address") ?? string.Empty;
                var city = GetStringCaseInsensitive(guardian, "City") ?? string.Empty;
                var county = GetStringCaseInsensitive(guardian, "County") ?? string.Empty;
                var state = GetStringCaseInsensitive(guardian, "State");
                var homePhone = GetStringCaseInsensitive(guardian, "HomePhone");
                var cellPhone = GetStringCaseInsensitive(guardian, "CellPhone");

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
                    FirstName = GetStringCaseInsensitive(stu, "FirstName") ?? string.Empty,
                    LastName = GetStringCaseInsensitive(stu, "LastName") ?? string.Empty,
                    Grade = GetStringCaseInsensitive(stu, "Grade") ?? string.Empty,
                    TransportationNotes = GetStringCaseInsensitive(stu, "School", "TransportationNotes")
                };
                famDto.Students.Add(studentDto);
                return;
            }

            // Fallback: flat student object containing guardian/address fields at top level
            var parent = GetStringCaseInsensitive(stu, "ParentGuardian", "parentGuardian", "GuardianName") ?? string.Empty;
            var addr = GetStringCaseInsensitive(stu, "HomeAddress", "address", "Address") ?? string.Empty;
            var city2 = GetStringCaseInsensitive(stu, "City") ?? string.Empty;
            var county2 = GetStringCaseInsensitive(stu, "County") ?? string.Empty;
            var state2 = GetStringCaseInsensitive(stu, "State");
            var homePhone2 = GetStringCaseInsensitive(stu, "HomePhone");
            var cellPhone2 = GetStringCaseInsensitive(stu, "CellPhone");

            var famDtoFlat = families.FirstOrDefault(f => f.ParentGuardian == parent && f.Address == addr);
            if (famDtoFlat == null)
            {
                famDtoFlat = new FamilyImportDto
                {
                    ParentGuardian = parent,
                    Address = addr,
                    City = city2,
                    State = state2,
                    County = county2,
                    HomePhone = homePhone2,
                    CellPhone = cellPhone2,
                    Students = new List<StudentImportDto>()
                };
                families.Add(famDtoFlat);
            }
            else
            {
                // Backfill common fields if missing
                if (string.IsNullOrWhiteSpace(famDtoFlat.City) && !string.IsNullOrWhiteSpace(city2)) famDtoFlat.City = city2;
                if (string.IsNullOrWhiteSpace(famDtoFlat.State) && !string.IsNullOrWhiteSpace(state2)) famDtoFlat.State = state2;
                if (string.IsNullOrWhiteSpace(famDtoFlat.County) && !string.IsNullOrWhiteSpace(county2)) famDtoFlat.County = county2;
                if (string.IsNullOrWhiteSpace(famDtoFlat.HomePhone) && !string.IsNullOrWhiteSpace(homePhone2)) famDtoFlat.HomePhone = homePhone2;
                if (string.IsNullOrWhiteSpace(famDtoFlat.CellPhone) && !string.IsNullOrWhiteSpace(cellPhone2)) famDtoFlat.CellPhone = cellPhone2;
            }

            var studentDtoFlat = new StudentImportDto
            {
                FirstName = GetStringCaseInsensitive(stu, "FirstName", "firstName") ?? string.Empty,
                LastName = GetStringCaseInsensitive(stu, "LastName", "lastName") ?? string.Empty,
                Grade = GetStringCaseInsensitive(stu, "Grade") ?? string.Empty,
                TransportationNotes = GetStringCaseInsensitive(stu, "TransportationNotes", "School")
            };

            // If FirstName/LastName are missing but a combined StudentName exists, split it
            if (string.IsNullOrWhiteSpace(studentDtoFlat.FirstName) && string.IsNullOrWhiteSpace(studentDtoFlat.LastName))
            {
                var combined = GetStringCaseInsensitive(stu, "StudentName", "studentName");
                if (!string.IsNullOrWhiteSpace(combined))
                {
                    // Basic split: first token as FirstName, remainder as LastName
                    var parts = combined.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 1)
                    {
                        studentDtoFlat.FirstName = parts[0];
                        studentDtoFlat.LastName = string.Empty;
                    }
                    else if (parts.Length > 1)
                    {
                        studentDtoFlat.FirstName = parts[0];
                        studentDtoFlat.LastName = string.Join(' ', parts.Skip(1));
                    }
                }
            }
            famDtoFlat.Students.Add(studentDtoFlat);
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
                    // Fast pre-check (requested optimization): examine first non-whitespace character.
                    // If it is '[', assume a root array and skip probing for an object property named 'students'.
                    // This avoids unnecessary property enumeration on very large array-only files.
                    // Ref: Optional enhancement note (2025-08-11).
                    string rawJson = await File.ReadAllTextAsync(jsonFilePath);
                    // NOTE: Avoid ReadOnlySpan<char> (ref struct) inside async method (would trigger CS9202 with current LangVersion).
                    int i = 0;
                    while (i < rawJson.Length && char.IsWhiteSpace(rawJson[i])) i++;
                    bool rootLikelyArray = i < rawJson.Length && rawJson[i] == '[';

                    using var jsonDoc = JsonDocument.Parse(rawJson);
                    var rootElement = jsonDoc.RootElement;

                    if (rootLikelyArray)
                    {
                        if (rootElement.ValueKind == JsonValueKind.Array)
                        {
                            jsonStudentsCount = rootElement.GetArrayLength();
                            Logger.Information("Detected root-array JSON student dataset (fast path) with {Count} entries: {Path}", jsonStudentsCount, jsonFilePath);
                        }
                        else
                        {
                            // Fallback: the heuristic said array but actual kind differs — proceed with normal probing.
                            if (rootElement.TryGetProperty("students", out var studentsElemFast) && studentsElemFast.ValueKind == JsonValueKind.Array)
                            {
                                jsonStudentsCount = studentsElemFast.GetArrayLength();
                            }
                        }
                    }
                    else
                    {
                        // Normal path: expect an object wrapper first.
                        if (rootElement.TryGetProperty("students", out var studentsElem) && studentsElem.ValueKind == JsonValueKind.Array)
                        {
                            jsonStudentsCount = studentsElem.GetArrayLength();
                        }
                        else if (rootElement.ValueKind == JsonValueKind.Array)
                        {
                            // Unexpected but still support legacy root array.
                            jsonStudentsCount = rootElement.GetArrayLength();
                            Logger.Information("Detected root-array JSON student dataset (no 'students' wrapper) with {Count} entries: {Path}", jsonStudentsCount, jsonFilePath);
                        }
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
