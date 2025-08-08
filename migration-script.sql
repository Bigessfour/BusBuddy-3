IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [ActivityLogs] (
        [Id] int NOT NULL IDENTITY,
        [Timestamp] datetime2 NOT NULL,
        [Action] nvarchar(200) NOT NULL DEFAULT N'',
        [User] nvarchar(100) NOT NULL DEFAULT N'',
        [Details] nvarchar(1000) NULL,
        CONSTRAINT [PK_ActivityLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Destinations] (
        [DestinationId] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL DEFAULT N'',
        [Address] nvarchar(300) NOT NULL DEFAULT N'',
        [City] nvarchar(100) NOT NULL DEFAULT N'',
        [State] nvarchar(2) NOT NULL DEFAULT N'',
        [ZipCode] nvarchar(10) NOT NULL DEFAULT N'',
        [ContactName] nvarchar(100) NULL,
        [ContactPhone] nvarchar(20) NULL,
        [ContactEmail] nvarchar(100) NULL,
        [DestinationType] nvarchar(50) NOT NULL DEFAULT N'',
        [MaxCapacity] int NULL,
        [SpecialRequirements] nvarchar(500) NULL,
        [Latitude] decimal(10,8) NULL,
        [Longitude] decimal(11,8) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Destinations] PRIMARY KEY ([DestinationId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Drivers] (
        [DriverID] int NOT NULL IDENTITY,
        [DriverName] nvarchar(100) NOT NULL DEFAULT N'',
        [DriverPhone] nvarchar(20) NULL,
        [DriverEmail] nvarchar(100) NULL,
        [Address] nvarchar(200) NULL,
        [City] nvarchar(50) NULL,
        [State] nvarchar(20) NULL,
        [Zip] nvarchar(10) NULL,
        [DriversLicenseType] nvarchar(20) NOT NULL DEFAULT N'',
        [TrainingComplete] bit NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'',
        [FirstName] nvarchar(50) NULL,
        [LastName] nvarchar(50) NULL,
        [LicenseNumber] nvarchar(20) NULL,
        [LicenseClass] nvarchar(10) NULL,
        [LicenseIssueDate] datetime2 NULL,
        [LicenseExpiryDate] datetime2 NULL,
        [Endorsements] nvarchar(100) NULL,
        [HireDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedDate] datetime2 NULL,
        [EmergencyContactName] nvarchar(100) NULL,
        [EmergencyContactPhone] nvarchar(20) NULL,
        [MedicalRestrictions] nvarchar(100) NULL,
        [BackgroundCheckDate] datetime2 NULL,
        [BackgroundCheckExpiry] datetime2 NULL,
        [DrugTestDate] datetime2 NULL,
        [DrugTestExpiry] datetime2 NULL,
        [PhysicalExamDate] datetime2 NULL,
        [PhysicalExamExpiry] datetime2 NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Drivers] PRIMARY KEY ([DriverID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [SchoolCalendar] (
        [CalendarId] int NOT NULL IDENTITY,
        [Date] datetime2 NOT NULL,
        [EventType] nvarchar(50) NOT NULL DEFAULT N'',
        [EventName] nvarchar(100) NOT NULL DEFAULT N'',
        [SchoolYear] nvarchar(10) NOT NULL DEFAULT N'',
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [RoutesRequired] bit NOT NULL,
        [Description] nvarchar(200) NULL,
        [Notes] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_SchoolCalendar] PRIMARY KEY ([CalendarId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Students] (
        [StudentId] int NOT NULL IDENTITY,
        [StudentName] nvarchar(100) NOT NULL DEFAULT N'',
        [StudentNumber] nvarchar(20) NULL,
        [Grade] nvarchar(20) NULL,
        [DateOfBirth] datetime2 NULL,
        [Gender] nvarchar(10) NULL,
        [HomeAddress] nvarchar(200) NULL,
        [City] nvarchar(50) NULL,
        [State] nvarchar(2) NULL,
        [Zip] nvarchar(10) NULL,
        [HomePhone] nvarchar(20) NULL,
        [ParentGuardian] nvarchar(100) NULL,
        [EmergencyPhone] nvarchar(20) NULL,
        [School] nvarchar(100) NULL,
        [BusStop] nvarchar(50) NULL,
        [AMRoute] nvarchar(50) NULL,
        [PMRoute] nvarchar(50) NULL,
        [Active] bit NOT NULL,
        [SpecialNeeds] bit NOT NULL,
        [SpecialAccommodations] nvarchar(1000) NULL,
        [Allergies] nvarchar(200) NULL,
        [Medications] nvarchar(200) NULL,
        [MedicalNotes] nvarchar(1000) NULL,
        [DoctorName] nvarchar(100) NULL,
        [DoctorPhone] nvarchar(20) NULL,
        [FieldTripPermission] bit NOT NULL,
        [PhotoPermission] bit NOT NULL,
        [PickupAddress] nvarchar(200) NULL,
        [DropoffAddress] nvarchar(200) NULL,
        [TransportationNotes] nvarchar(1000) NULL,
        [AlternativeContact] nvarchar(100) NULL,
        [AlternativePhone] nvarchar(20) NULL,
        [EnrollmentDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Students] PRIMARY KEY ([StudentId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Vehicles] (
        [VehicleId] int NOT NULL IDENTITY,
        [BusNumber] nvarchar(20) NOT NULL DEFAULT N'',
        [Year] int NOT NULL,
        [Make] nvarchar(50) NOT NULL DEFAULT N'',
        [Model] nvarchar(50) NOT NULL DEFAULT N'',
        [SeatingCapacity] int NOT NULL,
        [VIN] nvarchar(17) NOT NULL DEFAULT N'',
        [LicenseNumber] nvarchar(20) NOT NULL DEFAULT N'',
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [DateLastInspection] datetime2 NULL,
        [CurrentOdometer] int NULL,
        [PurchaseDate] datetime2 NULL,
        [PurchasePrice] decimal(10,2) NULL,
        [InsurancePolicyNumber] nvarchar(100) NULL,
        [InsuranceExpiryDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedDate] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [Department] nvarchar(50) NULL,
        [FleetType] nvarchar(20) NULL,
        [FuelCapacity] decimal(8,2) NULL,
        [FuelType] nvarchar(20) NULL,
        [MilesPerGallon] decimal(6,2) NULL,
        [NextMaintenanceDue] datetime2 NULL,
        [NextMaintenanceMileage] int NULL,
        [LastServiceDate] datetime2 NULL,
        [SpecialEquipment] nvarchar(1000) NULL,
        [GPSTracking] bit NOT NULL,
        [GPSDeviceId] nvarchar(100) NULL,
        [Notes] nvarchar(1000) NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([VehicleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Fuel] (
        [FuelId] int NOT NULL IDENTITY,
        [FuelDate] datetime2 NOT NULL,
        [FuelLocation] nvarchar(100) NOT NULL DEFAULT N'',
        [VehicleFueledId] int NOT NULL,
        [VehicleOdometerReading] int NOT NULL,
        [FuelType] nvarchar(20) NOT NULL DEFAULT N'Gasoline',
        [Gallons] decimal(8,3) NULL,
        [PricePerGallon] decimal(8,3) NULL,
        [TotalCost] decimal(10,2) NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_Fuel] PRIMARY KEY ([FuelId]),
        CONSTRAINT [FK_Fuel_Vehicle] FOREIGN KEY ([VehicleFueledId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Maintenance] (
        [MaintenanceId] int NOT NULL IDENTITY,
        [Date] datetime2 NOT NULL,
        [VehicleId] int NOT NULL,
        [OdometerReading] int NOT NULL,
        [MaintenanceCompleted] nvarchar(100) NOT NULL DEFAULT N'',
        [Vendor] nvarchar(100) NOT NULL DEFAULT N'',
        [RepairCost] decimal(10,2) NOT NULL,
        [Description] nvarchar(500) NULL,
        [PerformedBy] nvarchar(100) NULL,
        [NextServiceDue] datetime2 NULL,
        [NextServiceOdometer] int NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'',
        [Notes] nvarchar(1000) NULL,
        [WorkOrderNumber] nvarchar(100) NULL,
        [Priority] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [Warranty] bit NOT NULL,
        [WarrantyExpiry] datetime2 NULL,
        [PartsUsed] nvarchar(1000) NULL,
        [LaborHours] decimal(8,2) NULL,
        [LaborCost] decimal(10,2) NULL,
        [PartsCost] decimal(10,2) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedDate] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Maintenance] PRIMARY KEY ([MaintenanceId]),
        CONSTRAINT [FK_Maintenance_Vehicle] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Routes] (
        [RouteID] int NOT NULL IDENTITY,
        [Date] datetime2 NOT NULL,
        [RouteName] nvarchar(50) NOT NULL DEFAULT N'',
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [AMVehicleID] int NULL,
        [AMBeginMiles] decimal(10,2) NULL,
        [AMEndMiles] decimal(10,2) NULL,
        [AMRiders] int NULL,
        [AMDriverID] int NULL,
        [PMVehicleID] int NULL,
        [PMBeginMiles] decimal(10,2) NULL,
        [PMEndMiles] decimal(10,2) NULL,
        [PMRiders] int NULL,
        [PMDriverID] int NULL,
        [Distance] decimal(10,2) NULL,
        [EstimatedDuration] int NULL,
        [StudentCount] int NULL,
        [StopCount] int NULL,
        [AMBeginTime] time NULL,
        [PMBeginTime] time NULL,
        [DriverName] nvarchar(100) NULL,
        [BusNumber] nvarchar(20) NULL,
        [School] nvarchar(max) NOT NULL DEFAULT N'',
        CONSTRAINT [PK_Routes] PRIMARY KEY ([RouteID]),
        CONSTRAINT [FK_Routes_AMDriver] FOREIGN KEY ([AMDriverID]) REFERENCES [Drivers] ([DriverID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Routes_AMVehicle] FOREIGN KEY ([AMVehicleID]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Routes_PMDriver] FOREIGN KEY ([PMDriverID]) REFERENCES [Drivers] ([DriverID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Routes_PMVehicle] FOREIGN KEY ([PMVehicleID]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Activities] (
        [ActivityId] int NOT NULL IDENTITY,
        [Date] datetime2 NOT NULL,
        [ActivityType] nvarchar(50) NOT NULL DEFAULT N'',
        [Destination] nvarchar(200) NOT NULL DEFAULT N'',
        [DestinationId] int NULL,
        [DestinationOverride] nvarchar(200) NULL,
        [LeaveTime] time NOT NULL,
        [EventTime] time NOT NULL,
        [RequestedBy] nvarchar(100) NOT NULL DEFAULT N'',
        [AssignedVehicleId] int NOT NULL,
        [DriverId] int NULL,
        [StudentsCount] int NULL,
        [Notes] nvarchar(500) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Scheduled',
        [RouteId] int NULL,
        [Description] nvarchar(500) NULL DEFAULT N'Activity',
        [ReturnTime] time NOT NULL,
        [ExpectedPassengers] int NULL,
        [RecurringSeriesId] int NULL,
        [ActivityCategory] nvarchar(100) NULL,
        [EstimatedCost] decimal(10,2) NULL,
        [ActualCost] decimal(10,2) NULL,
        [ApprovalRequired] bit NOT NULL,
        [Approved] bit NOT NULL,
        [ApprovedBy] nvarchar(100) NULL,
        [ApprovalDate] datetime2 NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedDate] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [DestinationLatitude] decimal(10,8) NULL,
        [DestinationLongitude] decimal(11,8) NULL,
        [DistanceMiles] decimal(8,2) NULL,
        [EstimatedTravelTime] time NULL,
        [Directions] nvarchar(500) NULL,
        [PickupLocation] nvarchar(200) NULL,
        [PickupLatitude] decimal(10,8) NULL,
        [PickupLongitude] decimal(11,8) NULL,
        [ActivityName] nvarchar(max) NOT NULL DEFAULT N'',
        [DepartureTime] time NOT NULL,
        [EstimatedArrival] time NOT NULL,
        [AssignedDriverId] int NOT NULL,
        CONSTRAINT [PK_Activities] PRIMARY KEY ([ActivityId]),
        CONSTRAINT [FK_Activities_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([DestinationId]),
        CONSTRAINT [FK_Activities_Driver] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([DriverID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Activities_Route] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteID]) ON DELETE SET NULL,
        CONSTRAINT [FK_Activities_Vehicle] FOREIGN KEY ([AssignedVehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [RouteStops] (
        [RouteStopId] int NOT NULL IDENTITY,
        [RouteId] int NOT NULL,
        [StopName] nvarchar(100) NOT NULL DEFAULT N'',
        [StopAddress] nvarchar(200) NOT NULL DEFAULT N'',
        [Latitude] decimal(10,8) NULL,
        [Longitude] decimal(11,8) NULL,
        [StopOrder] int NOT NULL,
        [ScheduledArrival] time NOT NULL,
        [ScheduledDeparture] time NOT NULL,
        [StopDuration] int NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'',
        [Notes] nvarchar(500) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [EstimatedArrivalTime] datetime2 NOT NULL,
        [EstimatedDepartureTime] datetime2 NOT NULL,
        CONSTRAINT [PK_RouteStops] PRIMARY KEY ([RouteStopId]),
        CONSTRAINT [FK_RouteStops_Route] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [Schedules] (
        [ScheduleId] int NOT NULL IDENTITY,
        [VehicleId] int NOT NULL,
        [RouteId] int NOT NULL,
        [DriverId] int NOT NULL,
        [DepartureTime] datetime2 NOT NULL,
        [ArrivalTime] datetime2 NOT NULL,
        [ScheduleDate] datetime2 NOT NULL,
        [SportsCategory] nvarchar(50) NULL,
        [Opponent] nvarchar(200) NULL,
        [Location] nvarchar(200) NULL,
        [DestinationTown] nvarchar(100) NULL,
        [DepartTime] time NULL,
        [ScheduledTime] time NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'',
        [Notes] nvarchar(500) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Schedules] PRIMARY KEY ([ScheduleId]),
        CONSTRAINT [FK_Schedules_Bus] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Schedules_Driver] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([DriverID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Schedules_Route] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteID]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [TripEvents] (
        [TripEventId] int NOT NULL IDENTITY,
        [Type] int NOT NULL,
        [CustomType] nvarchar(100) NULL,
        [POCName] nvarchar(100) NOT NULL DEFAULT N'',
        [POCPhone] nvarchar(20) NULL,
        [POCEmail] nvarchar(100) NULL,
        [LeaveTime] datetime2 NOT NULL,
        [ReturnTime] datetime2 NULL,
        [VehicleId] int NULL,
        [DriverId] int NULL,
        [RouteId] int NULL,
        [StudentCount] int NOT NULL,
        [AdultSupervisorCount] int NOT NULL,
        [Destination] nvarchar(200) NULL,
        [SpecialRequirements] nvarchar(500) NULL,
        [TripNotes] nvarchar(1000) NULL,
        [ApprovalRequired] bit NOT NULL,
        [IsApproved] bit NOT NULL,
        [ApprovedBy] nvarchar(100) NULL,
        [ApprovalDate] datetime2 NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Scheduled',
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedDate] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_TripEvents] PRIMARY KEY ([TripEventId]),
        CONSTRAINT [FK_TripEvents_Driver] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([DriverID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TripEvents_Route] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteID]) ON DELETE SET NULL,
        CONSTRAINT [FK_TripEvents_Vehicle] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [ActivitySchedule] (
        [ActivityScheduleId] int NOT NULL IDENTITY,
        [ScheduledDate] datetime2 NOT NULL,
        [TripType] nvarchar(50) NOT NULL DEFAULT N'',
        [ScheduledVehicleId] int NOT NULL,
        [ScheduledDestination] nvarchar(200) NOT NULL DEFAULT N'',
        [ScheduledLeaveTime] time NOT NULL,
        [ScheduledEventTime] time NOT NULL,
        [ScheduledRiders] int NULL,
        [ScheduledDriverId] int NOT NULL,
        [RequestedBy] nvarchar(100) NOT NULL DEFAULT N'',
        [Status] nvarchar(20) NOT NULL DEFAULT N'',
        [Notes] nvarchar(500) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [TripEventId] int NULL,
        [ActivityId] int NULL,
        CONSTRAINT [PK_ActivitySchedule] PRIMARY KEY ([ActivityScheduleId]),
        CONSTRAINT [FK_ActivitySchedule_Activities_ActivityId] FOREIGN KEY ([ActivityId]) REFERENCES [Activities] ([ActivityId]),
        CONSTRAINT [FK_ActivitySchedule_Driver] FOREIGN KEY ([ScheduledDriverId]) REFERENCES [Drivers] ([DriverID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivitySchedule_TripEvents_TripEventId] FOREIGN KEY ([TripEventId]) REFERENCES [TripEvents] ([TripEventId]),
        CONSTRAINT [FK_ActivitySchedule_Vehicle] FOREIGN KEY ([ScheduledVehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE TABLE [StudentSchedules] (
        [StudentScheduleId] int NOT NULL IDENTITY,
        [StudentId] int NOT NULL,
        [ScheduleId] int NULL,
        [ActivityScheduleId] int NULL,
        [AssignmentType] nvarchar(20) NOT NULL DEFAULT N'',
        [PickupLocation] nvarchar(100) NULL,
        [DropoffLocation] nvarchar(100) NULL,
        [Confirmed] bit NOT NULL,
        [Attended] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_StudentSchedules] PRIMARY KEY ([StudentScheduleId]),
        CONSTRAINT [FK_StudentSchedules_ActivitySchedule] FOREIGN KEY ([ActivityScheduleId]) REFERENCES [ActivitySchedule] ([ActivityScheduleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_StudentSchedules_Schedule] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedules] ([ScheduleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_StudentSchedules_Student] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([StudentId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'DriverID', N'Address', N'City', N'CreatedBy', N'CreatedDate', N'DriverEmail', N'DriverName', N'DriverPhone', N'DriversLicenseType', N'EmergencyContactName', N'EmergencyContactPhone', N'LicenseExpiryDate', N'Notes', N'State', N'TrainingComplete', N'UpdatedBy', N'UpdatedDate', N'Zip') AND [object_id] = OBJECT_ID(N'[Drivers]'))
        SET IDENTITY_INSERT [Drivers] ON;
    EXEC(N'INSERT INTO [Drivers] ([DriverID], [Address], [City], [CreatedBy], [CreatedDate], [DriverEmail], [DriverName], [DriverPhone], [DriversLicenseType], [EmergencyContactName], [EmergencyContactPhone], [LicenseExpiryDate], [Notes], [State], [TrainingComplete], [UpdatedBy], [UpdatedDate], [Zip])
    VALUES (1, NULL, NULL, NULL, ''2025-01-01T00:00:00.0000000Z'', N''john.smith@school.edu'', N''John Smith'', N''555-0123'', N''CDL'', NULL, NULL, NULL, NULL, NULL, CAST(1 AS bit), NULL, NULL, NULL),
    (2, NULL, NULL, NULL, ''2025-01-01T00:00:00.0000000Z'', N''mary.johnson@school.edu'', N''Mary Johnson'', N''555-0456'', N''CDL'', NULL, NULL, NULL, NULL, NULL, CAST(1 AS bit), NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'DriverID', N'Address', N'City', N'CreatedBy', N'CreatedDate', N'DriverEmail', N'DriverName', N'DriverPhone', N'DriversLicenseType', N'EmergencyContactName', N'EmergencyContactPhone', N'LicenseExpiryDate', N'Notes', N'State', N'TrainingComplete', N'UpdatedBy', N'UpdatedDate', N'Zip') AND [object_id] = OBJECT_ID(N'[Drivers]'))
        SET IDENTITY_INSERT [Drivers] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'VehicleId', N'BusNumber', N'CreatedBy', N'CreatedDate', N'CurrentOdometer', N'DateLastInspection', N'Department', N'FleetType', N'FuelCapacity', N'FuelType', N'GPSDeviceId', N'GPSTracking', N'InsuranceExpiryDate', N'InsurancePolicyNumber', N'LastServiceDate', N'LicenseNumber', N'Make', N'MilesPerGallon', N'Model', N'NextMaintenanceDue', N'NextMaintenanceMileage', N'Notes', N'PurchaseDate', N'PurchasePrice', N'SeatingCapacity', N'SpecialEquipment', N'Status', N'UpdatedBy', N'UpdatedDate', N'VIN', N'Year') AND [object_id] = OBJECT_ID(N'[Vehicles]'))
        SET IDENTITY_INSERT [Vehicles] ON;
    EXEC(N'INSERT INTO [Vehicles] ([VehicleId], [BusNumber], [CreatedBy], [CreatedDate], [CurrentOdometer], [DateLastInspection], [Department], [FleetType], [FuelCapacity], [FuelType], [GPSDeviceId], [GPSTracking], [InsuranceExpiryDate], [InsurancePolicyNumber], [LastServiceDate], [LicenseNumber], [Make], [MilesPerGallon], [Model], [NextMaintenanceDue], [NextMaintenanceMileage], [Notes], [PurchaseDate], [PurchasePrice], [SeatingCapacity], [SpecialEquipment], [Status], [UpdatedBy], [UpdatedDate], [VIN], [Year])
    VALUES (1, N''001'', NULL, ''2025-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(0 AS bit), NULL, NULL, NULL, N''TX123456'', N''Blue Bird'', NULL, N''Vision'', NULL, NULL, NULL, ''2020-08-15T00:00:00.0000000'', 85000.0, 72, NULL, N''Active'', NULL, NULL, N''1BAANKCL7LF123456'', 2020),
    (2, N''002'', NULL, ''2025-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(0 AS bit), NULL, NULL, NULL, N''TX654321'', N''Thomas Built'', NULL, N''Saf-T-Liner C2'', NULL, NULL, NULL, ''2019-07-10T00:00:00.0000000'', 82000.0, 66, NULL, N''Active'', NULL, NULL, N''4DRBTAAN7KB654321'', 2019)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'VehicleId', N'BusNumber', N'CreatedBy', N'CreatedDate', N'CurrentOdometer', N'DateLastInspection', N'Department', N'FleetType', N'FuelCapacity', N'FuelType', N'GPSDeviceId', N'GPSTracking', N'InsuranceExpiryDate', N'InsurancePolicyNumber', N'LastServiceDate', N'LicenseNumber', N'Make', N'MilesPerGallon', N'Model', N'NextMaintenanceDue', N'NextMaintenanceMileage', N'Notes', N'PurchaseDate', N'PurchasePrice', N'SeatingCapacity', N'SpecialEquipment', N'Status', N'UpdatedBy', N'UpdatedDate', N'VIN', N'Year') AND [object_id] = OBJECT_ID(N'[Vehicles]'))
        SET IDENTITY_INSERT [Vehicles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_ActivityType] ON [Activities] ([ActivityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_ApprovalRequired] ON [Activities] ([ApprovalRequired]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_Date] ON [Activities] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_DateTimeRange] ON [Activities] ([Date], [LeaveTime], [EventTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_DestinationId] ON [Activities] ([DestinationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_DriverId] ON [Activities] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_DriverSchedule] ON [Activities] ([DriverId], [Date], [LeaveTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_RouteId] ON [Activities] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_Status] ON [Activities] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_VehicleId] ON [Activities] ([AssignedVehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_VehicleSchedule] ON [Activities] ([AssignedVehicleId], [Date], [LeaveTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivityLogs_Timestamp] ON [ActivityLogs] ([Timestamp] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivitySchedule_ActivityId] ON [ActivitySchedule] ([ActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivitySchedule_Date] ON [ActivitySchedule] ([ScheduledDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivitySchedule_DriverId] ON [ActivitySchedule] ([ScheduledDriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivitySchedule_TripEventId] ON [ActivitySchedule] ([TripEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivitySchedule_TripType] ON [ActivitySchedule] ([TripType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ActivitySchedule_VehicleId] ON [ActivitySchedule] ([ScheduledVehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Drivers_Email] ON [Drivers] ([DriverEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Drivers_LicenseExpiration] ON [Drivers] ([LicenseExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Drivers_LicenseType] ON [Drivers] ([DriversLicenseType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Drivers_Phone] ON [Drivers] ([DriverPhone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Drivers_TrainingComplete] ON [Drivers] ([TrainingComplete]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Fuel_FuelDate] ON [Fuel] ([FuelDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Fuel_Location] ON [Fuel] ([FuelLocation]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Fuel_Type] ON [Fuel] ([FuelType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Fuel_VehicleDate] ON [Fuel] ([VehicleFueledId], [FuelDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Fuel_VehicleId] ON [Fuel] ([VehicleFueledId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Maintenance_Date] ON [Maintenance] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Maintenance_Priority] ON [Maintenance] ([Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Maintenance_Type] ON [Maintenance] ([MaintenanceCompleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Maintenance_VehicleDate] ON [Maintenance] ([VehicleId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Maintenance_VehicleId] ON [Maintenance] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Routes_AMDriverId] ON [Routes] ([AMDriverID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Routes_AMVehicleId] ON [Routes] ([AMVehicleID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Routes_Date] ON [Routes] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Routes_DateRouteName] ON [Routes] ([Date], [RouteName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Routes_PMDriverId] ON [Routes] ([PMDriverID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Routes_PMVehicleId] ON [Routes] ([PMVehicleID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Routes_RouteName] ON [Routes] ([RouteName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RouteStops_RouteId] ON [RouteStops] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RouteStops_RouteOrder] ON [RouteStops] ([RouteId], [StopOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Schedules_BusId] ON [Schedules] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Schedules_Date] ON [Schedules] ([ScheduleDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Schedules_DriverId] ON [Schedules] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Schedules_RouteBusDeparture] ON [Schedules] ([RouteId], [VehicleId], [DepartureTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Schedules_RouteId] ON [Schedules] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SchoolCalendar_Date] ON [SchoolCalendar] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SchoolCalendar_EventType] ON [SchoolCalendar] ([EventType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SchoolCalendar_RoutesRequired] ON [SchoolCalendar] ([RoutesRequired]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SchoolCalendar_SchoolYear] ON [SchoolCalendar] ([SchoolYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Students_Active] ON [Students] ([Active]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Students_Grade] ON [Students] ([Grade]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Students_Name] ON [Students] ([StudentName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Students_School] ON [Students] ([School]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentSchedules_ActivityScheduleId] ON [StudentSchedules] ([ActivityScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentSchedules_AssignmentType] ON [StudentSchedules] ([AssignmentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentSchedules_ScheduleId] ON [StudentSchedules] ([ScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentSchedules_StudentId] ON [StudentSchedules] ([StudentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StudentSchedules_StudentSchedule] ON [StudentSchedules] ([StudentId], [ScheduleId]) WHERE [ScheduleId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_ApprovalRequired] ON [TripEvents] ([ApprovalRequired]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_DriverId] ON [TripEvents] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_DriverSchedule] ON [TripEvents] ([DriverId], [LeaveTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_LeaveTime] ON [TripEvents] ([LeaveTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_RouteId] ON [TripEvents] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_Status] ON [TripEvents] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_Type] ON [TripEvents] ([Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_VehicleId] ON [TripEvents] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripEvents_VehicleSchedule] ON [TripEvents] ([VehicleId], [LeaveTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vehicles_BusNumber] ON [Vehicles] ([BusNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_DateLastInspection] ON [Vehicles] ([DateLastInspection]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_FleetType] ON [Vehicles] ([FleetType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_InsuranceExpiryDate] ON [Vehicles] ([InsuranceExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vehicles_LicenseNumber] ON [Vehicles] ([LicenseNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_MakeModelYear] ON [Vehicles] ([Make], [Model], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_Status] ON [Vehicles] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vehicles_VINNumber] ON [Vehicles] ([VIN]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804210443_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250804210443_InitialCreate', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    ALTER TABLE [Students] ADD [FamilyId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE TABLE [Families] (
        [FamilyId] int NOT NULL IDENTITY,
        [ParentGuardian] nvarchar(100) NOT NULL DEFAULT N'',
        [Address] nvarchar(200) NOT NULL DEFAULT N'',
        [City] nvarchar(50) NOT NULL DEFAULT N'',
        [County] nvarchar(50) NOT NULL DEFAULT N'',
        [HomePhone] nvarchar(20) NULL,
        [CellPhone] nvarchar(20) NULL,
        [EmergencyContact] nvarchar(100) NULL,
        [JointParent] nvarchar(100) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedDate] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Families] PRIMARY KEY ([FamilyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE TABLE [SportsEvents] (
        [Id] int NOT NULL IDENTITY,
        [EventName] nvarchar(200) NOT NULL DEFAULT N'',
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NOT NULL,
        [Location] nvarchar(500) NOT NULL DEFAULT N'',
        [TeamSize] int NOT NULL,
        [VehicleId] int NULL,
        [DriverId] int NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'',
        [SafetyNotes] nvarchar(1000) NOT NULL DEFAULT N'',
        [Sport] nvarchar(100) NOT NULL DEFAULT N'',
        [IsHomeGame] bit NOT NULL,
        [EmergencyContact] nvarchar(500) NOT NULL DEFAULT N'',
        [WeatherConditions] nvarchar(200) NOT NULL DEFAULT N'',
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SportsEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SportsEvents_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([DriverID]),
        CONSTRAINT [FK_SportsEvents_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([VehicleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE INDEX [IX_Students_FamilyId] ON [Students] ([FamilyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE INDEX [IX_Families_City] ON [Families] ([City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE INDEX [IX_Families_ParentAddress] ON [Families] ([ParentGuardian], [Address]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE INDEX [IX_SportsEvents_DriverId] ON [SportsEvents] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    CREATE INDEX [IX_SportsEvents_VehicleId] ON [SportsEvents] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    ALTER TABLE [Students] ADD CONSTRAINT [FK_Students_Family] FOREIGN KEY ([FamilyId]) REFERENCES [Families] ([FamilyId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250804230922_AddFamilySupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250804230922_AddFamilySupport', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    ALTER TABLE [Vehicles] ADD [Description] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Students]') AND [c].[name] = N'SpecialNeeds');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Students] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [Students] ALTER COLUMN [SpecialNeeds] nvarchar(max) NOT NULL;
    ALTER TABLE [Students] ADD DEFAULT N'' FOR [SpecialNeeds];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    ALTER TABLE [Students] ADD [RouteAssignmentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    ALTER TABLE [Routes] ADD [Boundaries] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    ALTER TABLE [Routes] ADD [Path] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    ALTER TABLE [Routes] ADD [RouteDescription] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    CREATE TABLE [RouteAssignments] (
        [RouteAssignmentId] int NOT NULL IDENTITY,
        [RouteId] int NOT NULL,
        [VehicleId] int NOT NULL,
        [AssignmentDate] datetime2 NOT NULL,
        CONSTRAINT [PK_RouteAssignments] PRIMARY KEY ([RouteAssignmentId]),
        CONSTRAINT [FK_RouteAssignments_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Routes] ([RouteID]) ON DELETE CASCADE,
        CONSTRAINT [FK_RouteAssignments_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([VehicleId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    EXEC(N'UPDATE [Vehicles] SET [Description] = NULL
    WHERE [VehicleId] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    EXEC(N'UPDATE [Vehicles] SET [Description] = NULL
    WHERE [VehicleId] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    CREATE INDEX [IX_Students_RouteAssignmentId] ON [Students] ([RouteAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    CREATE INDEX [IX_RouteAssignments_RouteId] ON [RouteAssignments] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    CREATE INDEX [IX_RouteAssignments_VehicleId] ON [RouteAssignments] ([VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    ALTER TABLE [Students] ADD CONSTRAINT [FK_Students_RouteAssignments_RouteAssignmentId] FOREIGN KEY ([RouteAssignmentId]) REFERENCES [RouteAssignments] ([RouteAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250805014747_UpdateBusDescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250805014747_UpdateBusDescription', N'9.0.8');
END;

COMMIT;
GO

