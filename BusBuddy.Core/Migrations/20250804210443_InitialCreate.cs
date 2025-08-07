using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    User = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Destinations",
                columns: table => new
                {
                    DestinationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false, defaultValue: ""),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DestinationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    MaxCapacity = table.Column<int>(type: "int", nullable: true),
                    SpecialRequirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinations", x => x.DestinationId);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    DriverID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    DriverPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DriverEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DriversLicenseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    TrainingComplete = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LicenseClass = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LicenseIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LicenseExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Endorsements = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MedicalRestrictions = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BackgroundCheckDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BackgroundCheckExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DrugTestDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DrugTestExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhysicalExamDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhysicalExamExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.DriverID);
                });

            migrationBuilder.CreateTable(
                name: "SchoolCalendar",
                columns: table => new
                {
                    CalendarId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    SchoolYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RoutesRequired = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolCalendar", x => x.CalendarId);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    StudentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HomeAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HomePhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ParentGuardian = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmergencyPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    School = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusStop = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AMRoute = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PMRoute = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    SpecialNeeds = table.Column<bool>(type: "bit", nullable: false),
                    SpecialAccommodations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Allergies = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Medications = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MedicalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DoctorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DoctorPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FieldTripPermission = table.Column<bool>(type: "bit", nullable: false),
                    PhotoPermission = table.Column<bool>(type: "bit", nullable: false),
                    PickupAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DropoffAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransportationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AlternativeContact = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AlternativePhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EnrollmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.StudentId);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Make = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    SeatingCapacity = table.Column<int>(type: "int", nullable: false),
                    VIN = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false, defaultValue: ""),
                    LicenseNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    DateLastInspection = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentOdometer = table.Column<int>(type: "int", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    InsurancePolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InsuranceExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FleetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FuelCapacity = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    FuelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MilesPerGallon = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    NextMaintenanceDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextMaintenanceMileage = table.Column<int>(type: "int", nullable: true),
                    LastServiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SpecialEquipment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GPSTracking = table.Column<bool>(type: "bit", nullable: false),
                    GPSDeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.VehicleId);
                });

            migrationBuilder.CreateTable(
                name: "Fuel",
                columns: table => new
                {
                    FuelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FuelDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FuelLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    VehicleFueledId = table.Column<int>(type: "int", nullable: false),
                    VehicleOdometerReading = table.Column<int>(type: "int", nullable: false),
                    FuelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Gasoline"),
                    Gallons = table.Column<decimal>(type: "decimal(8,3)", nullable: true),
                    PricePerGallon = table.Column<decimal>(type: "decimal(8,3)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fuel", x => x.FuelId);
                    table.ForeignKey(
                        name: "FK_Fuel_Vehicle",
                        column: x => x.VehicleFueledId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Maintenance",
                columns: table => new
                {
                    MaintenanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    OdometerReading = table.Column<int>(type: "int", nullable: false),
                    MaintenanceCompleted = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    Vendor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    RepairCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NextServiceDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextServiceOdometer = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    Warranty = table.Column<bool>(type: "bit", nullable: false),
                    WarrantyExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PartsUsed = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LaborHours = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    LaborCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PartsCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maintenance", x => x.MaintenanceId);
                    table.ForeignKey(
                        name: "FK_Maintenance_Vehicle",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    RouteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RouteName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AMVehicleID = table.Column<int>(type: "int", nullable: true),
                    AMBeginMiles = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AMEndMiles = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AMRiders = table.Column<int>(type: "int", nullable: true),
                    AMDriverID = table.Column<int>(type: "int", nullable: true),
                    PMVehicleID = table.Column<int>(type: "int", nullable: true),
                    PMBeginMiles = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PMEndMiles = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PMRiders = table.Column<int>(type: "int", nullable: true),
                    PMDriverID = table.Column<int>(type: "int", nullable: true),
                    Distance = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedDuration = table.Column<int>(type: "int", nullable: true),
                    StudentCount = table.Column<int>(type: "int", nullable: true),
                    StopCount = table.Column<int>(type: "int", nullable: true),
                    AMBeginTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    PMBeginTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    School = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.RouteID);
                    table.ForeignKey(
                        name: "FK_Routes_AMDriver",
                        column: x => x.AMDriverID,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Routes_AMVehicle",
                        column: x => x.AMVehicleID,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Routes_PMDriver",
                        column: x => x.PMDriverID,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Routes_PMVehicle",
                        column: x => x.PMVehicleID,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    DestinationId = table.Column<int>(type: "int", nullable: true),
                    DestinationOverride = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaveTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EventTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    AssignedVehicleId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    StudentsCount = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Scheduled"),
                    RouteId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, defaultValue: "Activity"),
                    ReturnTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ExpectedPassengers = table.Column<int>(type: "int", nullable: true),
                    RecurringSeriesId = table.Column<int>(type: "int", nullable: true),
                    ActivityCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ActualCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DestinationLatitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    DestinationLongitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    DistanceMiles = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    EstimatedTravelTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Directions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PickupLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PickupLatitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    PickupLongitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    ActivityName = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    DepartureTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EstimatedArrival = table.Column<TimeSpan>(type: "time", nullable: false),
                    AssignedDriverId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_Activities_Destinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "Destinations",
                        principalColumn: "DestinationId");
                    table.ForeignKey(
                        name: "FK_Activities_Driver",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Route",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Activities_Vehicle",
                        column: x => x.AssignedVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteStops",
                columns: table => new
                {
                    RouteStopId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    StopName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    StopAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    StopOrder = table.Column<int>(type: "int", nullable: false),
                    ScheduledArrival = table.Column<TimeSpan>(type: "time", nullable: false),
                    ScheduledDeparture = table.Column<TimeSpan>(type: "time", nullable: false),
                    StopDuration = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedArrivalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstimatedDepartureTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStops", x => x.RouteStopId);
                    table.ForeignKey(
                        name: "FK_RouteStops_Route",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    ScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArrivalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SportsCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Opponent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DestinationTown = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    ScheduledTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_Schedules_Bus",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Schedules_Driver",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Schedules_Route",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripEvents",
                columns: table => new
                {
                    TripEventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CustomType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    POCName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    POCPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    POCEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaveTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    RouteId = table.Column<int>(type: "int", nullable: true),
                    StudentCount = table.Column<int>(type: "int", nullable: false),
                    AdultSupervisorCount = table.Column<int>(type: "int", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SpecialRequirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TripNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Scheduled"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripEvents", x => x.TripEventId);
                    table.ForeignKey(
                        name: "FK_TripEvents_Driver",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripEvents_Route",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TripEvents_Vehicle",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySchedule",
                columns: table => new
                {
                    ActivityScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TripType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    ScheduledVehicleId = table.Column<int>(type: "int", nullable: false),
                    ScheduledDestination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    ScheduledLeaveTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ScheduledEventTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ScheduledRiders = table.Column<int>(type: "int", nullable: true),
                    ScheduledDriverId = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TripEventId = table.Column<int>(type: "int", nullable: true),
                    ActivityId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySchedule", x => x.ActivityScheduleId);
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "ActivityId");
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_Driver",
                        column: x => x.ScheduledDriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_TripEvents_TripEventId",
                        column: x => x.TripEventId,
                        principalTable: "TripEvents",
                        principalColumn: "TripEventId");
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_Vehicle",
                        column: x => x.ScheduledVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentSchedules",
                columns: table => new
                {
                    StudentScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: true),
                    ActivityScheduleId = table.Column<int>(type: "int", nullable: true),
                    AssignmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    PickupLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DropoffLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    Attended = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSchedules", x => x.StudentScheduleId);
                    table.ForeignKey(
                        name: "FK_StudentSchedules_ActivitySchedule",
                        column: x => x.ActivityScheduleId,
                        principalTable: "ActivitySchedule",
                        principalColumn: "ActivityScheduleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentSchedules_Schedule",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentSchedules_Student",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Drivers",
                columns: new[] { "DriverID", "Address", "City", "CreatedBy", "CreatedDate", "DriverEmail", "DriverName", "DriverPhone", "DriversLicenseType", "EmergencyContactName", "EmergencyContactPhone", "LicenseExpiryDate", "Notes", "State", "TrainingComplete", "UpdatedBy", "UpdatedDate", "Zip" },
                values: new object[,]
                {
                    { 1, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "john.smith@school.edu", "John Smith", "555-0123", "CDL", null, null, null, null, null, true, null, null, null },
                    { 2, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "mary.johnson@school.edu", "Mary Johnson", "555-0456", "CDL", null, null, null, null, null, true, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "Vehicles",
                columns: new[] { "VehicleId", "BusNumber", "CreatedBy", "CreatedDate", "CurrentOdometer", "DateLastInspection", "Department", "FleetType", "FuelCapacity", "FuelType", "GPSDeviceId", "GPSTracking", "InsuranceExpiryDate", "InsurancePolicyNumber", "LastServiceDate", "LicenseNumber", "Make", "MilesPerGallon", "Model", "NextMaintenanceDue", "NextMaintenanceMileage", "Notes", "PurchaseDate", "PurchasePrice", "SeatingCapacity", "SpecialEquipment", "Status", "UpdatedBy", "UpdatedDate", "VIN", "Year" },
                values: new object[,]
                {
                    { 1, "001", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, null, false, null, null, null, "TX123456", "Blue Bird", null, "Vision", null, null, null, new DateTime(2020, 8, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 85000.00m, 72, null, "Active", null, null, "1BAANKCL7LF123456", 2020 },
                    { 2, "002", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, null, null, null, false, null, null, null, "TX654321", "Thomas Built", null, "Saf-T-Liner C2", null, null, null, new DateTime(2019, 7, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 82000.00m, 66, null, "Active", null, null, "4DRBTAAN7KB654321", 2019 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActivityType",
                table: "Activities",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ApprovalRequired",
                table: "Activities",
                column: "ApprovalRequired");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Date",
                table: "Activities",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DateTimeRange",
                table: "Activities",
                columns: new[] { "Date", "LeaveTime", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DestinationId",
                table: "Activities",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DriverId",
                table: "Activities",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DriverSchedule",
                table: "Activities",
                columns: new[] { "DriverId", "Date", "LeaveTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_RouteId",
                table: "Activities",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Status",
                table: "Activities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_VehicleId",
                table: "Activities",
                column: "AssignedVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_VehicleSchedule",
                table: "Activities",
                columns: new[] { "AssignedVehicleId", "Date", "LeaveTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs",
                column: "Timestamp",
                descending: System.Array.Empty<bool>());

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_ActivityId",
                table: "ActivitySchedule",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_Date",
                table: "ActivitySchedule",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_DriverId",
                table: "ActivitySchedule",
                column: "ScheduledDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_TripEventId",
                table: "ActivitySchedule",
                column: "TripEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_TripType",
                table: "ActivitySchedule",
                column: "TripType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_VehicleId",
                table: "ActivitySchedule",
                column: "ScheduledVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Email",
                table: "Drivers",
                column: "DriverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_LicenseExpiration",
                table: "Drivers",
                column: "LicenseExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_LicenseType",
                table: "Drivers",
                column: "DriversLicenseType");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Phone",
                table: "Drivers",
                column: "DriverPhone");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TrainingComplete",
                table: "Drivers",
                column: "TrainingComplete");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_FuelDate",
                table: "Fuel",
                column: "FuelDate");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_Location",
                table: "Fuel",
                column: "FuelLocation");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_Type",
                table: "Fuel",
                column: "FuelType");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_VehicleDate",
                table: "Fuel",
                columns: new[] { "VehicleFueledId", "FuelDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_VehicleId",
                table: "Fuel",
                column: "VehicleFueledId");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_Date",
                table: "Maintenance",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_Priority",
                table: "Maintenance",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_Type",
                table: "Maintenance",
                column: "MaintenanceCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_VehicleDate",
                table: "Maintenance",
                columns: new[] { "VehicleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_VehicleId",
                table: "Maintenance",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_AMDriverId",
                table: "Routes",
                column: "AMDriverID");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_AMVehicleId",
                table: "Routes",
                column: "AMVehicleID");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Date",
                table: "Routes",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_DateRouteName",
                table: "Routes",
                columns: new[] { "Date", "RouteName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_PMDriverId",
                table: "Routes",
                column: "PMDriverID");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_PMVehicleId",
                table: "Routes",
                column: "PMVehicleID");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_RouteName",
                table: "Routes",
                column: "RouteName");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_RouteId",
                table: "RouteStops",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteStops_RouteOrder",
                table: "RouteStops",
                columns: new[] { "RouteId", "StopOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_BusId",
                table: "Schedules",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_Date",
                table: "Schedules",
                column: "ScheduleDate");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_DriverId",
                table: "Schedules",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_RouteBusDeparture",
                table: "Schedules",
                columns: new[] { "RouteId", "VehicleId", "DepartureTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_RouteId",
                table: "Schedules",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCalendar_Date",
                table: "SchoolCalendar",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCalendar_EventType",
                table: "SchoolCalendar",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCalendar_RoutesRequired",
                table: "SchoolCalendar",
                column: "RoutesRequired");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolCalendar_SchoolYear",
                table: "SchoolCalendar",
                column: "SchoolYear");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Active",
                table: "Students",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Grade",
                table: "Students",
                column: "Grade");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Name",
                table: "Students",
                column: "StudentName");

            migrationBuilder.CreateIndex(
                name: "IX_Students_School",
                table: "Students",
                column: "School");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSchedules_ActivityScheduleId",
                table: "StudentSchedules",
                column: "ActivityScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSchedules_AssignmentType",
                table: "StudentSchedules",
                column: "AssignmentType");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSchedules_ScheduleId",
                table: "StudentSchedules",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSchedules_StudentId",
                table: "StudentSchedules",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSchedules_StudentSchedule",
                table: "StudentSchedules",
                columns: new[] { "StudentId", "ScheduleId" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_ApprovalRequired",
                table: "TripEvents",
                column: "ApprovalRequired");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_DriverId",
                table: "TripEvents",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_DriverSchedule",
                table: "TripEvents",
                columns: new[] { "DriverId", "LeaveTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_LeaveTime",
                table: "TripEvents",
                column: "LeaveTime");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_RouteId",
                table: "TripEvents",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_Status",
                table: "TripEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_Type",
                table: "TripEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_VehicleId",
                table: "TripEvents",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_VehicleSchedule",
                table: "TripEvents",
                columns: new[] { "VehicleId", "LeaveTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_BusNumber",
                table: "Vehicles",
                column: "BusNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DateLastInspection",
                table: "Vehicles",
                column: "DateLastInspection");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_FleetType",
                table: "Vehicles",
                column: "FleetType");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_InsuranceExpiryDate",
                table: "Vehicles",
                column: "InsuranceExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_LicenseNumber",
                table: "Vehicles",
                column: "LicenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_MakeModelYear",
                table: "Vehicles",
                columns: new[] { "Make", "Model", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Status",
                table: "Vehicles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VINNumber",
                table: "Vehicles",
                column: "VIN",
                unique: true);
        }
    }
}
