using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BusBuddy.Core.Data;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Core.Services
{
    public partial class SeedDataService
    {
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

        // Synthetic tokens only. Never put anything resembling a real child, address, or phone
        // in this file — specs/students.md forbids committing student PII to git.
        private static string GetEmbeddedSampleCsv() => @"
Student,,,Parent,,,,,,,,Joint Parent,,,,,,,Econtact,,
Fname,Lname,Grade,Fname,Lname,Address,City,State,County,Hphone,Cphone,Jparent FirstName,Jparent LastName,Address,City,State,County,Cphone ,Econtact FirstName,Econtact LastName,Econtact Phone
TEST_STUDENT_01,SEEDDATA,7,TEST_GUARDIAN_01,SEEDDATA,100 Test St,TESTVILLE,CO,TEST COUNTY,,555-0100,,,,,,,,
TEST_STUDENT_02,SEEDDATA,3,TEST_GUARDIAN_02,SEEDDATA,200 Test St,TESTVILLE,CO,TEST COUNTY,,555-0101,,,,,,,,
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

                // Roster format (single header row, explicit column names) carries campus, route,
                // and special-needs columns that the legacy family-export format cannot express.
                if (lines.Length >= 1 && IsRosterHeader(lines[0]))
                {
                    return await ImportRosterCsvAsync(context, lines, createdBy);
                }

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
                        "CSV is not in the expected student format. Expected either a roster header row containing " +
                        "StudentName, or a legacy family-export header row with Fname, Lname, Grade, and Address.");
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
                    // The legacy family export carries no route or eligibility columns, so this leaves
                    // both runs off. Eligibility is stated, not assumed (specs/students.md) — a clerk
                    // ticks the AM/PM boxes on the form, or a roster CSV that names routes states it.
                    StudentRideModeHelper.ApplyRouteDerivedEligibility(student);

                    if (skipIfAlreadySeeded)
                    {
                        student.FamilyId = family.FamilyId;
                    }
                    students.Add(student);
                }

                return await FinalizeImportAsync(context, students, families, dedupeByName: !skipIfAlreadySeeded);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error seeding students from CSV");
                throw;
            }
        }

        /// <summary>
        /// Shared tail for both CSV formats: drop names that already exist, link campuses to the
        /// Destinations catalog, then persist.
        /// </summary>
        private static async Task<int> FinalizeImportAsync(
            BusBuddyDbContext context,
            List<Student> students,
            List<Family> families,
            bool dedupeByName)
        {
            if (dedupeByName)
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

            List<Destination> activeSchools = [];
            if (context.Destinations is not null)
            {
                try
                {
                    activeSchools = await context.Destinations
                        .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
                        .ToListAsync();
                }
                catch (InvalidOperationException)
                {
                    activeSchools = context.Destinations
                        .Where(d => d.IsActive && !d.IsDeleted && d.DestinationType == DestinationTypes.School)
                        .ToList();
                }
            }

            if (activeSchools.Count > 0)
            {
                var soleSchoolFallbacks = 0;
                foreach (var student in students)
                {
                    // A roster that names the campus per child owns that value. Only fill in from the
                    // catalog when the row left the campus blank, otherwise a single-school district
                    // would silently rewrite every multi-campus roster onto one destination.
                    if (string.IsNullOrWhiteSpace(student.School) && activeSchools.Count == 1)
                    {
                        student.School = activeSchools[0].Name;
                        student.DestinationId = activeSchools[0].DestinationId;
                        soleSchoolFallbacks++;
                        continue;
                    }

                    BusBuddy.Core.Utilities.StudentSchoolLinker.SyncDestinationFromSchoolName(student, activeSchools);
                }

                var unlinked = students.Count(s => s.DestinationId is null or 0);
                Logger.Information(
                    "CSV import campus linking Students={Count} SoleSchoolFallback={Fallback} Unlinked={Unlinked}",
                    students.Count,
                    soleSchoolFallbacks,
                    unlinked);
            }

            context.Families.AddRange(families);
            context.Students.AddRange(students);
            await context.SaveChangesAsync();
            Logger.Information("Imported {Count} students from CSV.", students.Count);
            return students.Count;
        }

        /// <summary>
        /// Columns the roster format understands. Anything else in the header is ignored with a warning
        /// so a clerk's extra bookkeeping column does not fail the whole import.
        /// </summary>
        private static readonly string[] RosterColumns =
        [
            "StopNumber", "PickupTime", "StudentName", "Grade", "School", "SchoolYear",
            "HomeAddress", "City", "State", "Zip",
            "GuardianName", "GuardianPhone", "HomePhone", "EmergencyContactName", "EmergencyContactPhone",
            "PickupMode", "PickupStopId", "AMRoute", "PMRoute", "RidesAm", "RidesPm",
            "RequiresSpecialNeedsBus", "RequiresAide", "RequiresWheelchair", "RequiresSeatBelt",
            "HasMedicalNeeds", "Active", "TransportationNotes"
        ];

        /// <summary>
        /// Columns the roster carries for the clerk's benefit that this importer does not read.
        /// Stop order and times belong to the route (specs/routes.md), not to the child.
        /// </summary>
        private static readonly string[] RosterRouteOnlyColumns = ["StopNumber", "PickupTime"];

        /// <summary>A roster CSV declares its columns on the first line and always includes StudentName.</summary>
        private static bool IsRosterHeader(string line) =>
            SplitCsvLine(line).Any(f => string.Equals(f, "StudentName", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Splits one CSV line honouring double-quoted fields (needed because roster directions and
        /// notes contain commas). Embedded newlines are not supported — keep one record per line.
        /// </summary>
        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c != '"')
                    {
                        current.Append(c);
                    }
                    else if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString().Trim());
            return [.. fields];
        }

        private static bool ParseRosterBool(string value, bool defaultValue = false) =>
            ParseRosterBoolOrNull(value) ?? defaultValue;

        /// <summary>
        /// Tri-state parse: null means the roster said nothing, so the caller can fall back to
        /// inference instead of treating silence as <c>false</c>.
        /// </summary>
        private static bool? ParseRosterBoolOrNull(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "true" or "yes" or "y" or "1" or "x" => true,
                "false" or "no" or "n" or "0" => false,
                _ => null
            };
        }

        /// <summary>
        /// Imports the roster format: one header row of named columns, one row per child.
        /// Unlike the legacy family-export format this carries the assigned campus, AM/PM route,
        /// and the special-needs / aide flags, so a special-needs run imports without losing them.
        /// Latitude/Longitude are deliberately never read here — coordinates come from Address
        /// Validation, not from a typed roster (specs/students.md).
        /// </summary>
        private static async Task<int> ImportRosterCsvAsync(
            BusBuddyDbContext context,
            string[] lines,
            string createdBy)
        {
            var header = SplitCsvLine(lines[0]);
            int Col(string name) =>
                Array.FindIndex(header, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));

            foreach (var column in header.Where(h => !string.IsNullOrWhiteSpace(h)))
            {
                if (!RosterColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                {
                    Logger.Warning("Roster CSV column {Column} is not mapped to a Student field; ignoring it.", column);
                }
                else if (RosterRouteOnlyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                {
                    Logger.Information(
                        "Roster CSV column {Column} is retained for the clerk but is not read by the importer.",
                        column);
                }
            }

            int idxName = Col("StudentName");
            int idxGrade = Col("Grade");
            int idxSchool = Col("School");
            int idxSchoolYear = Col("SchoolYear");
            int idxAddress = Col("HomeAddress");
            int idxCity = Col("City");
            int idxState = Col("State");
            int idxZip = Col("Zip");
            int idxGuardian = Col("GuardianName");
            int idxGuardianPhone = Col("GuardianPhone");
            int idxHomePhone = Col("HomePhone");
            int idxEmergencyName = Col("EmergencyContactName");
            int idxEmergencyPhone = Col("EmergencyContactPhone");
            int idxPickupMode = Col("PickupMode");
            int idxPickupStopId = Col("PickupStopId");
            int idxAmRoute = Col("AMRoute");
            int idxPmRoute = Col("PMRoute");
            int idxRidesAm = Col("RidesAm");
            int idxRidesPm = Col("RidesPm");
            int idxSpecialNeeds = Col("RequiresSpecialNeedsBus");
            int idxAide = Col("RequiresAide");
            int idxWheelchair = Col("RequiresWheelchair");
            int idxSeatBelt = Col("RequiresSeatBelt");
            int idxMedical = Col("HasMedicalNeeds");
            int idxActive = Col("Active");
            int idxNotes = Col("TransportationNotes");

            var studentNum = await NextStudentNumberAsync(context);
            var families = new List<Family>();
            var students = new List<Student>();

            for (var i = 1; i < lines.Length; i++)
            {
                var row = lines[i];
                if (string.IsNullOrWhiteSpace(row) || row.All(c => c == ','))
                {
                    continue;
                }

                var cols = SplitCsvLine(row);
                string Value(int idx) => idx >= 0 && idx < cols.Length ? cols[idx] : string.Empty;

                var studentName = Value(idxName);
                if (string.IsNullOrWhiteSpace(studentName))
                {
                    Logger.Warning("Skipping roster row {Row}: missing StudentName.", i + 1);
                    continue;
                }

                var requiresSpecialNeedsBus = ParseRosterBool(Value(idxSpecialNeeds));
                var pickupMode = Value(idxPickupMode);
                var wantsCatalogStop = string.Equals(
                    pickupMode, LocationTypes.PickupModeCatalogStop, StringComparison.OrdinalIgnoreCase);

                // specs/students.md pickup rule 3: special needs forces home pickup on a special-needs
                // route. Refuse the row rather than importing a contradictory record.
                if (requiresSpecialNeedsBus && wantsCatalogStop)
                {
                    Logger.Warning(
                        "Skipping roster row {Row}: RequiresSpecialNeedsBus is true but PickupMode is {Mode}. " +
                        "Special-needs riders are home pickup.",
                        i + 1,
                        pickupMode);
                    continue;
                }

                int? pickupStopId = null;
                if (int.TryParse(Value(idxPickupStopId), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stopId)
                    && stopId > 0)
                {
                    pickupStopId = stopId;
                }

                // A catalog stop is only real if it points at a published PickupStop.
                if (wantsCatalogStop && pickupStopId is null)
                {
                    Logger.Warning(
                        "Skipping roster row {Row}: PickupMode is CatalogStop but no PickupStopId was supplied.",
                        i + 1);
                    continue;
                }

                if (!wantsCatalogStop)
                {
                    pickupStopId = null;
                }

                var amRoute = Value(idxAmRoute);
                var pmRoute = Value(idxPmRoute);

                // specs/students.md: "MUST allow AM eligibility, PM eligibility, both, or neither,
                // independently." A roster column states it outright; otherwise the assigned route is
                // the statement — the same rule the database backfill uses. Silence is never "both".
                var ridesAm = ParseRosterBoolOrNull(Value(idxRidesAm)) ?? !string.IsNullOrWhiteSpace(amRoute);
                var ridesPm = ParseRosterBoolOrNull(Value(idxRidesPm)) ?? !string.IsNullOrWhiteSpace(pmRoute);

                var guardianName = Value(idxGuardian);
                var guardianPhone = Value(idxGuardianPhone);
                var address = Value(idxAddress);
                var city = Value(idxCity);
                var state = Value(idxState);
                var zip = Value(idxZip);

                // Siblings share one home and one guardian; they are separate students in one family.
                var family = families.LastOrDefault(f =>
                    string.Equals(f.ParentGuardian, guardianName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Address, address, StringComparison.OrdinalIgnoreCase));
                if (family is null && (!string.IsNullOrWhiteSpace(guardianName) || !string.IsNullOrWhiteSpace(address)))
                {
                    family = new Family
                    {
                        ParentGuardian = guardianName,
                        Address = address,
                        City = city,
                        HomePhone = Value(idxHomePhone),
                        CellPhone = guardianPhone,
                        EmergencyContact = Value(idxEmergencyName),
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = createdBy
                    };
                    families.Add(family);
                }

                var student = new Student
                {
                    StudentName = studentName,
                    Grade = string.IsNullOrWhiteSpace(Value(idxGrade)) ? null : Value(idxGrade),
                    School = Value(idxSchool),
                    HomeAddress = address,
                    City = city,
                    State = state,
                    Zip = zip,
                    ParentGuardian = guardianName,
                    CellPhone = guardianPhone,
                    HomePhone = Value(idxHomePhone),
                    EmergencyContactName = Value(idxEmergencyName),
                    EmergencyPhone = Value(idxEmergencyPhone),
                    PickupStopId = pickupStopId,
                    AMRoute = amRoute,
                    PMRoute = pmRoute,
                    RidesAm = ridesAm,
                    RidesPm = ridesPm,
                    SchoolYear = Value(idxSchoolYear),
                    RequiresSpecialNeedsBus = requiresSpecialNeedsBus,
                    RequiresAide = ParseRosterBool(Value(idxAide)),
                    RequiresWheelchair = ParseRosterBool(Value(idxWheelchair)),
                    RequiresSeatBelt = ParseRosterBool(Value(idxSeatBelt)),
                    HasMedicalNeeds = ParseRosterBool(Value(idxMedical)),
                    TransportationNotes = string.IsNullOrWhiteSpace(Value(idxNotes)) ? null : Value(idxNotes),
                    Active = ParseRosterBool(Value(idxActive), defaultValue: true),
                    StudentNumber = $"STU{studentNum++.ToString("D4", CultureInfo.InvariantCulture)}",
                    Family = family,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdBy
                };

                StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(student);
                students.Add(student);
            }

            Logger.Information(
                "Roster CSV parsed Rows={Rows} Students={Students} SpecialNeeds={SpecialNeeds} " +
                "HomePickup={HomePickup} RidesAm={RidesAm} RidesPm={RidesPm}",
                lines.Length - 1,
                students.Count,
                students.Count(s => s.RequiresSpecialNeedsBus),
                students.Count(s => s.PickupStopId is null),
                students.Count(s => s.RidesAm),
                students.Count(s => s.RidesPm));

            // A student whose AMRoute names no route, or more than one, fails ValidateStudentAsync,
            // which would lock the clerk out of editing the record at all.
            await ReconcileRouteAssignmentsAsync(context, students, createdBy);

            return await FinalizeImportAsync(context, students, families, dedupeByName: true);
        }

        /// <summary>
        /// Reduces a route name to its lowercase word set so word-order variants of the same route
        /// collapse together — a roster written "AM Bus 5 Special Needs" names the same run the
        /// database already calls "AM Special Needs Bus 5".
        /// </summary>
        private static string RouteNameKey(string routeName) =>
            string.Join(
                ' ',
                routeName
                    .Split(new[] { ' ', '\t', '-', '_', '#', '.', ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.ToLowerInvariant())
                    .OrderBy(t => t, StringComparer.Ordinal));

        /// <summary>
        /// Makes every imported AM/PM route name resolvable by <c>StudentService.ValidateStudentAsync</c>,
        /// which matches <c>Routes.RouteName</c> exactly:
        /// <list type="number">
        /// <item>A name that differs from an existing route only by word order is rewritten to the
        /// existing spelling, so the database stays the single source of truth for canonical names
        /// and no roster file has to be edited.</item>
        /// <item>A name with no route at all gets a route row created for it.</item>
        /// </list>
        /// </summary>
        private static async Task<int> ReconcileRouteAssignmentsAsync(
            BusBuddyDbContext context,
            List<Student> students,
            string createdBy)
        {
            List<Route> existingRoutes;
            try
            {
                existingRoutes = await context.Routes.ToListAsync();
            }
            catch (InvalidOperationException)
            {
                // Fallback for mocks lacking IAsyncQueryProvider, as elsewhere in this file.
                existingRoutes = context.Routes.ToList();
            }

            var canonicalByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var route in existingRoutes.Where(r => !string.IsNullOrWhiteSpace(r.RouteName)))
            {
                canonicalByKey.TryAdd(RouteNameKey(route.RouteName), route.RouteName);
            }

            string? Canonicalize(string? assigned, string studentName, string slot)
            {
                if (string.IsNullOrWhiteSpace(assigned))
                {
                    return assigned;
                }

                if (!canonicalByKey.TryGetValue(RouteNameKey(assigned), out var canonical))
                {
                    return assigned;
                }

                if (!string.Equals(canonical, assigned, StringComparison.Ordinal))
                {
                    Logger.Information(
                        "Roster {Slot} route {Assigned} matched existing route {Canonical} by name variant; " +
                        "using the existing spelling for {StudentName}.",
                        slot,
                        assigned,
                        canonical,
                        studentName);
                }

                return canonical;
            }

            foreach (var student in students)
            {
                student.AMRoute = Canonicalize(student.AMRoute, student.StudentName, "AM");
                student.PMRoute = Canonicalize(student.PMRoute, student.StudentName, "PM");
            }

            var missing = students
                .SelectMany(s => new[] { s.AMRoute, s.PMRoute })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !canonicalByKey.ContainsKey(RouteNameKey(name)))
                .ToList();

            var createdRoutes = new List<Route>();
            if (missing.Count > 0)
            {
                var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                foreach (var name in missing)
                {
                    var isSpecialNeeds = StudentSpecialNeedsHelper.IsSpecialNeedsRoute(name, false);
                    var route = new Route
                    {
                        RouteName = name,
                        Description = "Created from roster import so assigned students validate.",
                        Date = todayUtc,
                        IsActive = true,
                        IsSpecialNeedsRoute = isSpecialNeeds,
                        Session = RouteSession.Infer(name, isSpecialNeeds, null)
                    };
                    context.Routes.Add(route);
                    createdRoutes.Add(route);

                    Logger.Information(
                        "Created route {RouteName} from roster import Session={Session} SpecialNeeds={SpecialNeeds}",
                        name,
                        RouteSession.Infer(name, isSpecialNeeds, null),
                        isSpecialNeeds);
                }

                await context.SaveChangesAsync();
                existingRoutes.AddRange(createdRoutes);
            }

            DualWriteUniqueRouteKeys(students, existingRoutes);
            return createdRoutes.Count;
        }

        /// <summary>
        /// Dual-write <see cref="Student.AmRouteId"/> / <see cref="Student.PmRouteId"/> when the
        /// mirrored name resolves to exactly one route. Name-only rows stay name-only when the
        /// name is missing or ambiguous (same rule as <c>20260916180546_StudentRouteForeignKeys</c>).
        /// </summary>
        private static void DualWriteUniqueRouteKeys(IEnumerable<Student> students, IReadOnlyList<Route> routes)
        {
            var catalog = routes
                .Select(r => (r.RouteId, (string?)r.RouteName))
                .ToList();
            var byId = routes.ToDictionary(r => r.RouteId);

            foreach (var student in students)
            {
                if (student.AmRouteId is null)
                {
                    var amId = StudentRouteAssignment.UniqueIdForName(catalog, student.AMRoute);
                    if (amId is > 0 && byId.TryGetValue(amId.Value, out var amRoute))
                    {
                        StudentRouteAssignment.SetSlot(student, RouteTimeSlot.AM, amRoute);
                    }
                }

                if (student.PmRouteId is null)
                {
                    var pmId = StudentRouteAssignment.UniqueIdForName(catalog, student.PMRoute);
                    if (pmId is > 0 && byId.TryGetValue(pmId.Value, out var pmRoute))
                    {
                        StudentRouteAssignment.SetSlot(student, RouteTimeSlot.PM, pmRoute);
                    }
                }
            }
        }

    }
}
