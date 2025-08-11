/*
Purpose: Create a safe, flexible staging table for rider CSV imports.
Run in Azure Data Studio before using Import Flat File to load your CSV.
*/
IF OBJECT_ID('dbo.Riders_Staging', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Riders_Staging (
        -- Core identity fields
        StudentName            nvarchar(100)    NULL,
        StudentNumber          nvarchar(50)     NULL,
        Grade                  nvarchar(20)     NULL,
        DateOfBirth            nvarchar(50)     NULL,
        Gender                 nvarchar(20)     NULL,

        -- Home and contact
        HomeAddress            nvarchar(200)    NULL,
        City                   nvarchar(80)     NULL,
        [State]                nvarchar(2)      NULL,
        Zip                    nvarchar(20)     NULL,
        HomePhone              nvarchar(30)     NULL,
        ParentGuardian         nvarchar(120)    NULL,
        EmergencyPhone         nvarchar(30)     NULL,

        -- School and routing
        School                 nvarchar(120)    NULL,
        BusStop                nvarchar(120)    NULL,
        AMRoute                nvarchar(80)     NULL,
        PMRoute                nvarchar(80)     NULL,

        -- Medical and permissions (string-tolerant for import; normalized at MERGE)
        SpecialNeeds           nvarchar(max)    NULL,
        SpecialAccommodations  nvarchar(max)    NULL,
        Allergies              nvarchar(max)    NULL,
        Medications            nvarchar(max)    NULL,
        MedicalNotes           nvarchar(max)    NULL,
        DoctorName             nvarchar(120)    NULL,
        DoctorPhone            nvarchar(30)     NULL,
        FieldTripPermission    nvarchar(10)     NULL,
        PhotoPermission        nvarchar(10)     NULL,

        -- Alternative contact and logistics
        PickupAddress          nvarchar(200)    NULL,
        DropoffAddress         nvarchar(200)    NULL,
        TransportationNotes    nvarchar(max)    NULL,
        AlternativeContact     nvarchar(120)    NULL,
        AlternativePhone       nvarchar(30)     NULL,

        -- Enrollment
        EnrollmentDate         nvarchar(50)     NULL
    );
END
GO

-- Optional: Clear staging table before re-imports
-- TRUNCATE TABLE dbo.Riders_Staging;

-- Quick checks
-- SELECT TOP 5 * FROM dbo.Riders_Staging;
