/*
    Purpose: RIGHT-first upsert from dbo.Riders_Staging into dbo.Students.
    Strategy:
      - Normalize/trim inputs in a CTE
      - Parse dates and booleans safely (TRY_CONVERT, CASE patterns)
      - Use StudentNumber when available as the match key; fallback to (StudentName + DOB) cautiously (omitted by default)
      - Insert with defaults where Students has NOT NULL columns

    Prereq: dbo.Students exists with schema from migration-script.sql
*/

SET NOCOUNT ON;

;WITH S AS (
    SELECT
        RS.StagingId,
        LTRIM(RTRIM(RS.StudentNumber)) AS StudentNumber,
        LTRIM(RTRIM(RS.StudentName))   AS StudentName,
        LTRIM(RTRIM(RS.Grade))         AS Grade,
        TRY_CONVERT(datetime2, NULLIF(LTRIM(RTRIM(RS.DateOfBirth)), '')) AS DateOfBirth,
        LTRIM(RTRIM(RS.Gender))        AS Gender,
        LTRIM(RTRIM(RS.HomeAddress))   AS HomeAddress,
        LTRIM(RTRIM(RS.City))          AS City,
        UPPER(LEFT(LTRIM(RTRIM(RS.[State])), 2)) AS [State],
        LTRIM(RTRIM(RS.Zip))           AS Zip,
        LTRIM(RTRIM(RS.HomePhone))     AS HomePhone,
        LTRIM(RTRIM(RS.ParentGuardian)) AS ParentGuardian,
        LTRIM(RTRIM(RS.EmergencyPhone)) AS EmergencyPhone,
        LTRIM(RTRIM(RS.School))        AS School,
        LTRIM(RTRIM(RS.BusStop))       AS BusStop,
        LTRIM(RTRIM(RS.AMRoute))       AS AMRoute,
        LTRIM(RTRIM(RS.PMRoute))       AS PMRoute,
        -- Normalize booleans: accept Y/Yes/True/1 as true
        CASE WHEN RS.FieldTripPermission IS NULL OR LTRIM(RTRIM(RS.FieldTripPermission)) = '' THEN NULL
             WHEN UPPER(LTRIM(RTRIM(RS.FieldTripPermission))) IN ('Y','YES','TRUE','1') THEN CAST(1 AS bit)
             WHEN UPPER(LTRIM(RTRIM(RS.FieldTripPermission))) IN ('N','NO','FALSE','0') THEN CAST(0 AS bit)
             ELSE NULL END AS FieldTripPermission,
        CASE WHEN RS.PhotoPermission IS NULL OR LTRIM(RTRIM(RS.PhotoPermission)) = '' THEN NULL
             WHEN UPPER(LTRIM(RTRIM(RS.PhotoPermission))) IN ('Y','YES','TRUE','1') THEN CAST(1 AS bit)
             WHEN UPPER(LTRIM(RTRIM(RS.PhotoPermission))) IN ('N','NO','FALSE','0') THEN CAST(0 AS bit)
             ELSE NULL END AS PhotoPermission,
        LTRIM(RTRIM(RS.SpecialNeeds))  AS SpecialNeeds,
        RS.SpecialAccommodations,
        RS.Allergies,
        RS.Medications,
        RS.MedicalNotes,
        LTRIM(RTRIM(RS.DoctorName))    AS DoctorName,
        LTRIM(RTRIM(RS.DoctorPhone))   AS DoctorPhone,
        LTRIM(RTRIM(RS.PickupAddress)) AS PickupAddress,
        LTRIM(RTRIM(RS.DropoffAddress)) AS DropoffAddress,
        RS.TransportationNotes,
        LTRIM(RTRIM(RS.AlternativeContact)) AS AlternativeContact,
        LTRIM(RTRIM(RS.AlternativePhone))   AS AlternativePhone,
        TRY_CONVERT(datetime2, NULLIF(LTRIM(RTRIM(RS.EnrollmentDate)), '')) AS EnrollmentDate
    FROM dbo.Riders_Staging RS
)
, Keyed AS (
    SELECT *,
           CASE WHEN StudentNumber IS NOT NULL AND StudentNumber <> '' THEN StudentNumber ELSE NULL END AS MatchStudentNumber
    FROM S
)

, Rowed AS (
    SELECT K.*, ROW_NUMBER() OVER (PARTITION BY K.MatchStudentNumber ORDER BY K.StagingId) AS rn
    FROM Keyed K
)
, Deduped AS (
    -- Keep one row per StudentNumber; keep all rows where StudentNumber is NULL (will INSERT)
    SELECT * FROM Rowed
    WHERE MatchStudentNumber IS NULL OR rn = 1
)

MERGE dbo.Students AS T
USING Deduped AS S
ON (
    -- Preferred key: StudentNumber
    (S.MatchStudentNumber IS NOT NULL AND T.StudentNumber = S.MatchStudentNumber)
)
WHEN MATCHED THEN
    UPDATE SET
        T.StudentName         = COALESCE(S.StudentName, T.StudentName),
        T.Grade               = COALESCE(S.Grade, T.Grade),
        T.DateOfBirth         = COALESCE(S.DateOfBirth, T.DateOfBirth),
        T.Gender              = COALESCE(S.Gender, T.Gender),
        T.HomeAddress         = COALESCE(S.HomeAddress, T.HomeAddress),
        T.City                = COALESCE(S.City, T.City),
        T.[State]             = COALESCE(S.[State], T.[State]),
        T.Zip                 = COALESCE(S.Zip, T.Zip),
        T.HomePhone           = COALESCE(S.HomePhone, T.HomePhone),
        T.ParentGuardian      = COALESCE(S.ParentGuardian, T.ParentGuardian),
        T.EmergencyPhone      = COALESCE(S.EmergencyPhone, T.EmergencyPhone),
        T.School              = COALESCE(S.School, T.School),
        T.BusStop             = COALESCE(S.BusStop, T.BusStop),
        T.AMRoute             = COALESCE(S.AMRoute, T.AMRoute),
        T.PMRoute             = COALESCE(S.PMRoute, T.PMRoute),
        -- Active defaults to true when new; do not flip existing unless explicitly desired
        -- SpecialNeeds is nvarchar(max) in final schema (post-migration adjustments)
        T.SpecialNeeds        = COALESCE(S.SpecialNeeds, T.SpecialNeeds),
        T.SpecialAccommodations = COALESCE(S.SpecialAccommodations, T.SpecialAccommodations),
        T.Allergies           = COALESCE(S.Allergies, T.Allergies),
        T.Medications         = COALESCE(S.Medications, T.Medications),
        T.MedicalNotes        = COALESCE(S.MedicalNotes, T.MedicalNotes),
        T.DoctorName          = COALESCE(S.DoctorName, T.DoctorName),
        T.DoctorPhone         = COALESCE(S.DoctorPhone, T.DoctorPhone),
        T.FieldTripPermission = COALESCE(S.FieldTripPermission, T.FieldTripPermission),
        T.PhotoPermission     = COALESCE(S.PhotoPermission, T.PhotoPermission),
        T.PickupAddress       = COALESCE(S.PickupAddress, T.PickupAddress),
        T.DropoffAddress      = COALESCE(S.DropoffAddress, T.DropoffAddress),
        T.TransportationNotes = COALESCE(S.TransportationNotes, T.TransportationNotes),
        T.AlternativeContact  = COALESCE(S.AlternativeContact, T.AlternativeContact),
        T.AlternativePhone    = COALESCE(S.AlternativePhone, T.AlternativePhone),
        T.EnrollmentDate      = COALESCE(S.EnrollmentDate, T.EnrollmentDate),
        T.UpdatedDate         = SYSUTCDATETIME(),
        T.UpdatedBy           = COALESCE(SUSER_SNAME(), T.UpdatedBy)
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        StudentName, StudentNumber, Grade, DateOfBirth, Gender,
        HomeAddress, City, [State], Zip, HomePhone, ParentGuardian, EmergencyPhone,
        School, BusStop, AMRoute, PMRoute,
    Active, SpecialNeeds, SpecialAccommodations, Allergies, Medications, MedicalNotes,
    DoctorName, DoctorPhone, FieldTripPermission, PhotoPermission,
        PickupAddress, DropoffAddress, TransportationNotes, AlternativeContact, AlternativePhone,
        EnrollmentDate, CreatedDate, CreatedBy
    )
    VALUES (
    ISNULL(S.StudentName, N''), S.StudentNumber, S.Grade, S.DateOfBirth, S.Gender,
        S.HomeAddress, S.City, S.[State], S.Zip, S.HomePhone, S.ParentGuardian, S.EmergencyPhone,
        S.School, S.BusStop, S.AMRoute, S.PMRoute,
    CAST(1 AS bit), ISNULL(S.SpecialNeeds, N''), S.SpecialAccommodations, S.Allergies, S.Medications, S.MedicalNotes,
    S.DoctorName, S.DoctorPhone, ISNULL(S.FieldTripPermission, CAST(0 AS bit)), ISNULL(S.PhotoPermission, CAST(0 AS bit)),
        S.PickupAddress, S.DropoffAddress, S.TransportationNotes, S.AlternativeContact, S.AlternativePhone,
        S.EnrollmentDate, SYSUTCDATETIME(), SUSER_SNAME()
    )
OUTPUT $action AS MergeAction, inserted.StudentId, inserted.StudentNumber, inserted.StudentName;

-- Summary counts
SELECT
    (SELECT COUNT(*) FROM dbo.Riders_Staging) AS StagingCount,
    (SELECT COUNT(*) FROM dbo.Students) AS TotalStudents;
