# BusBuddy Staging Database Setup Script
# Creates staging database for UAT testing

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "BusBuddy-RG",

    [Parameter(Mandatory=$false)]
    [string]$ServerName = "busbuddy-server-sm2",

    [Parameter(Mandatory=$false)]
    [string]$StagingDatabaseName = "BusBuddyDB-Staging",

    [Parameter(Mandatory=$false)]
    [string]$ProductionDatabaseName = "BusBuddyDB"
)

Write-Information "🚀 Setting up BusBuddy Staging Database for UAT" -InformationAction Continue

try {
    # Step 1: Create staging database
    Write-Information "📋 Step 1: Creating staging database: $StagingDatabaseName" -InformationAction Continue

    $stagingDb = az sql db create `
        --resource-group $ResourceGroupName `
        --server $ServerName `
        --name $StagingDatabaseName `
        --service-objective S0 `
        --backup-storage-redundancy Local `
        --query "name" `
        --output tsv

    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Staging database created: $stagingDb" -InformationAction Continue
    } else {
        Write-Warning "⚠️ Database might already exist, continuing..."
    }

    # Step 2: Copy schema from production (without data)
    Write-Information "📋 Step 2: Copying schema from production database" -InformationAction Continue

    # Get connection string for staging database
    $stagingConnectionString = "Server=tcp:$ServerName.database.windows.net,1433;Initial Catalog=$StagingDatabaseName;Persist Security Info=False;User ID=`${AZURE_SQL_USER};Password=`${AZURE_SQL_PASSWORD};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    Write-Information "🔧 Staging connection string configured" -InformationAction Continue

    # Step 3: Apply Entity Framework migrations to staging
    Write-Information "📋 Step 3: Applying EF migrations to staging database" -InformationAction Continue

    # Set environment variables for staging
    $env:ASPNETCORE_ENVIRONMENT = "Staging"
    $env:STAGING_DATABASE_CONNECTION_STRING = $stagingConnectionString

    # Apply migrations
    dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF --connection $stagingConnectionString

    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Migrations applied successfully" -InformationAction Continue
    } else {
        throw "Failed to apply migrations to staging database"
    }

    # Step 4: Seed test data for UAT
    Write-Information "📋 Step 4: Seeding test data for UAT" -InformationAction Continue

    # Create test data seeding script
    $seedScript = @"
-- BusBuddy Staging Test Data
-- Seed basic test data for UAT scenarios

-- Insert test students
INSERT INTO Students (StudentNumber, StudentName, Address, PhoneNumber, EmergencyContact, CreatedDate, IsActive)
VALUES
('STU001', 'Alice Johnson', '123 Main St, Phoenix, AZ', '(555) 123-4567', 'Parent: (555) 123-4568', GETDATE(), 1),
('STU002', 'Bob Smith', '456 Oak Ave, Phoenix, AZ', '(555) 234-5678', 'Parent: (555) 234-5679', GETDATE(), 1),
('STU003', 'Carol Davis', '789 Pine Rd, Phoenix, AZ', '(555) 345-6789', 'Parent: (555) 345-6780', GETDATE(), 1),
('STU004', 'David Wilson', '321 Elm St, Phoenix, AZ', '(555) 456-7890', 'Parent: (555) 456-7891', GETDATE(), 1),
('STU005', 'Emma Brown', '654 Cedar Ln, Phoenix, AZ', '(555) 567-8901', 'Parent: (555) 567-8902', GETDATE(), 1);

-- Insert test routes
INSERT INTO Routes (RouteName, RouteNumber, Description, StartTime, EndTime, IsActive, CreatedDate)
VALUES
('North Route', 'R001', 'Covers northern Phoenix area schools', '07:00:00', '08:30:00', 1, GETDATE()),
('South Route', 'R002', 'Covers southern Phoenix area schools', '07:15:00', '08:45:00', 1, GETDATE()),
('East Route', 'R003', 'Covers eastern Phoenix area schools', '07:30:00', '09:00:00', 1, GETDATE());

-- Insert test vehicles
INSERT INTO Vehicles (BusNumber, Make, Model, Year, Capacity, IsActive, CreatedDate)
VALUES
('BUS001', 'Blue Bird', 'Vision', 2020, 72, 1, GETDATE()),
('BUS002', 'IC Bus', 'CE Series', 2019, 84, 1, GETDATE()),
('BUS003', 'Thomas Built', 'Saf-T-Liner C2', 2021, 78, 1, GETDATE());
"@

    # Save and execute seed script
    $seedScript | Out-File "staging-seed-data.sql" -Encoding UTF8

    Write-Information "📋 Test data seeding script created: staging-seed-data.sql" -InformationAction Continue
    Write-Information "🔧 To apply test data, run: sqlcmd -S $ServerName.database.windows.net -d $StagingDatabaseName -i staging-seed-data.sql" -InformationAction Continue

    # Step 5: Configuration summary
    Write-Information "📊 Step 5: Staging Environment Summary" -InformationAction Continue
    Write-Information "═══════════════════════════════════════" -InformationAction Continue
    Write-Information "🎯 Environment: Staging" -InformationAction Continue
    Write-Information "🗄️ Database: $StagingDatabaseName" -InformationAction Continue
    Write-Information "🖥️ Server: $ServerName.database.windows.net" -InformationAction Continue
    Write-Information "📋 Connection: Set STAGING_DATABASE_CONNECTION_STRING environment variable" -InformationAction Continue
    Write-Information "📁 Seed Data: staging-seed-data.sql created" -InformationAction Continue
    Write-Information "═══════════════════════════════════════" -InformationAction Continue

    Write-Information "🎯 Next Steps:" -InformationAction Continue
    Write-Information "1. Set environment variables for staging deployment" -InformationAction Continue
    Write-Information "2. Deploy BusBuddy application with staging configuration" -InformationAction Continue
    Write-Information "3. Execute seed data script for test scenarios" -InformationAction Continue
    Write-Information "4. Begin UAT testing with transportation coordinators" -InformationAction Continue

    Write-Information "🎉 Staging database setup completed successfully!" -InformationAction Continue

} catch {
    Write-Error "❌ Staging database setup failed: $($_.Exception.Message)"
    Write-Error "📋 Check Azure permissions and retry"
    exit 1
}
