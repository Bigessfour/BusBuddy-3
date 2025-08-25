-- BusBuddy-3 Azure AD Service Principal Database User Setup
-- Run this script in BOTH databases: BusBuddyDB and BusBuddyDB-Staging
-- Connect using your Entra ID admin account

-- Create user from Azure AD service principal
CREATE USER [0a93d214-37e7-4147-beaf-8ca8036c614e] FROM EXTERNAL PROVIDER;

-- Grant read access
ALTER ROLE db_datareader ADD MEMBER [0a93d214-37e7-4147-beaf-8ca8036c614e];

-- Grant write access
ALTER ROLE db_datawriter ADD MEMBER [0a93d214-37e7-4147-beaf-8ca8036c614e];

-- Verify the user was created successfully
SELECT 
    dp.name AS principal_name,
    dp.type_desc AS principal_type,
    r.name AS role_name
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members rm ON dp.principal_id = rm.member_principal_id
LEFT JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
WHERE dp.name = '0a93d214-37e7-4147-beaf-8ca8036c614e'
ORDER BY dp.name, r.name;
