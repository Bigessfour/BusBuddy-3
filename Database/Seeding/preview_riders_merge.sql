/*
Preview checks before MERGE from dbo.Riders_Staging to dbo.Students
Run this after importing your CSV into dbo.Riders_Staging.
*/

PRINT '=== Row counts ===';
SELECT
    (SELECT COUNT(*) FROM dbo.Riders_Staging) AS StagingCount,
    (SELECT COUNT(*) FROM dbo.Students)       AS StudentsCount;

PRINT '=== Missing StudentNumber (will INSERT) ===';
SELECT TOP 50 * FROM dbo.Riders_Staging WHERE NULLIF(LTRIM(RTRIM(StudentNumber)),'') IS NULL;

PRINT '=== Duplicate StudentNumber in staging (dedupe in MERGE) ===';
SELECT StudentNumber, COUNT(*) AS Cnt
FROM dbo.Riders_Staging
WHERE NULLIF(LTRIM(RTRIM(StudentNumber)),'') IS NOT NULL
GROUP BY StudentNumber
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC;

PRINT '=== Sample of staged rows (sanity check) ===';
SELECT TOP 20 StudentName, StudentNumber, Grade, School, HomeAddress, City, [State], AMRoute, PMRoute
FROM dbo.Riders_Staging;

PRINT '=== Potential bad state abbreviations (expect 2 letters) ===';
SELECT DISTINCT [State] FROM dbo.Riders_Staging WHERE LEN(LTRIM(RTRIM([State]))) <> 2;

PRINT '=== Boolean normalization preview (FieldTripPermission, PhotoPermission) ===';
SELECT TOP 50
    FieldTripPermission,
    CASE WHEN UPPER(LTRIM(RTRIM(FieldTripPermission))) IN ('Y','YES','TRUE','1') THEN 1
         WHEN UPPER(LTRIM(RTRIM(FieldTripPermission))) IN ('N','NO','FALSE','0') THEN 0
         ELSE NULL END AS FieldTripPermission_Bool,
    PhotoPermission,
    CASE WHEN UPPER(LTRIM(RTRIM(PhotoPermission))) IN ('Y','YES','TRUE','1') THEN 1
         WHEN UPPER(LTRIM(RTRIM(PhotoPermission))) IN ('N','NO','FALSE','0') THEN 0
         ELSE NULL END AS PhotoPermission_Bool
FROM dbo.Riders_Staging;
