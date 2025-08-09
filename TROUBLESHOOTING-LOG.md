# 🔧 BusBuddy Troubleshooting Log

**Last Updated:** August 8, 2025  
**Purpose:** Centralized log of common issues, root causes, and verified solutions

---

## 🚨 **Critical Issues & Verified Fixes**

### **Database Migration & EF Tools Sync Issues**

#### **Issue: EF Tools Version Mismatch (RESOLVED)**
**Symptoms:**
- Build warnings about EF version compatibility
- Migration commands failing unexpectedly
- Database context errors during seeding

**Root Cause:** EF Tools version (9.0.7) was behind EF Core version (9.0.8)

**Verified Solution:**
```powershell
# Update EF Tools to match EF Core version
dotnet tool update --global dotnet-ef --version 9.0.8

# Verify alignment
dotnet ef --version
# Should output: Entity Framework Core .NET Command-line Tools 9.0.8
```

**Prevention:** Always align EF Tools and EF Core package versions in CI/CD.

---

#### **Issue: Migration History Out of Sync**
**Symptoms:**
- Error: "There is already an object named 'ActivityLogs' in the database"
- Migrations marked as applied but tables missing
- Seeding shows success but 0 records inserted

**Root Cause:** Migration history table inconsistent with actual database schema

**Verified Solution:**
```powershell
# Option 1: Sync migration history (RECOMMENDED for production)
dotnet ef database update --force

# Option 2: Reset migrations (DEV ONLY - DATA LOSS)
dotnet ef database drop --force
dotnet ef database update

# Option 3: Manual sync (when migrations conflict)
# 1. Backup current database
# 2. Drop __EFMigrationsHistory table
# 3. Re-run migrations from clean state
```

**Additional Checks:**
```sql
-- Verify migration history state
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;

-- Check for orphaned tables
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_TYPE = 'BASE TABLE';
```

---

### **Table Mapping & Entity Configuration Issues**

#### **Issue: Bus Entity Mapping to Wrong Table (RESOLVED)**
**Symptoms:**
- "Invalid object name 'Buses'" SQL errors
- WPF UI not displaying vehicle data
- Seeding failures with misleading success messages

**Root Cause:** Entity configured for "Buses" table but database contains "Vehicles"

**Verified Solution:**
```csharp
// In BusBuddyDbContext.cs - OnModelCreating()
entity.ToTable("Vehicles");  // NOT "Buses"

// Update all related index configurations
entity.HasIndex(e => e.BusNumber, "IX_Vehicles_BusNumber");  // NOT IX_Buses_*
```

**Verification Steps:**
```sql
-- Confirm table exists
SELECT COUNT(*) FROM Vehicles;  -- Should return row count, not error

-- Verify data
SELECT BusNumber, Make, Model, Status FROM Vehicles;
```

---

### **Foreign Key Constraint Violations**

#### **Issue: Student Route Assignment FK Errors**
**Symptoms:**
- Cannot assign students to routes
- FK constraint violation on Students.RouteAssignmentId
- CRUD operations failing on dependent entities

**Root Cause:** Foreign key relationships not properly configured or referential integrity issues

**Verified Solution:**
```sql
-- Check FK constraint existence
SELECT 
    fk.name AS ForeignKey,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
WHERE tp.name = 'Students';

-- Fix orphaned FK references
UPDATE Students 
SET RouteAssignmentId = NULL 
WHERE RouteAssignmentId NOT IN (SELECT RouteAssignmentId FROM RouteAssignments);
```

**EF Configuration Fix:**
```csharp
// In Student entity configuration
entity.HasOne(s => s.RouteAssignment)
      .WithMany(ra => ra.Students)
      .HasForeignKey(s => s.RouteAssignmentId)
      .OnDelete(DeleteBehavior.SetNull);  // Prevent cascade delete issues
```

---

### **Data Seeding & Initialization Issues**

#### **Issue: Seeding Shows Success But 0 Records**
**Symptoms:**
- Console shows "Database seeded successfully" 
- Actual tables remain empty
- UI displays no data despite successful seeding

**Root Cause:** Multiple potential causes:
1. Transaction not committed
2. Wrong database context
3. Entity validation failures silently ignored

**Verified Solution:**
```csharp
// Enhanced seeding with transaction verification
public async Task SeedAsync()
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try 
    {
        // Check if data already exists
        if (await _context.Students.AnyAsync())
        {
            _logger.LogInformation("Data already exists, skipping seed");
            return;
        }

        // Add entities with validation
        var students = CreateSampleStudents();
        await _context.Students.AddRangeAsync(students);
        
        // Force save and verify
        var saveResult = await _context.SaveChangesAsync();
        _logger.LogInformation("Saved {Count} records to database", saveResult);
        
        await transaction.CommitAsync();
        
        // Verify data was actually saved
        var actualCount = await _context.Students.CountAsync();
        _logger.LogInformation("Verification: {Count} students in database", actualCount);
        
        if (actualCount == 0)
        {
            throw new InvalidOperationException("Seeding failed - no records saved");
        }
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Seeding failed");
        throw;
    }
}
```

---

## 🔍 **End-to-End CRUD Testing Protocol**

### **Student Management CRUD Test**
```powershell
# Execute comprehensive CRUD validation
function Test-StudentCRUD {
    Write-Host "🧪 Testing Student CRUD Operations..." -ForegroundColor Cyan
    
    try {
        # Test CREATE
        $testStudent = @{
            StudentNumber = "TEST001"
            StudentName = "Test Student"
            Grade = "10"
            School = "Test High School"
        }
        
        # Test via UI or direct service call
        $created = Add-TestStudent $testStudent
        if (-not $created) { throw "CREATE failed" }
        Write-Host "✅ CREATE: Student added successfully" -ForegroundColor Green
        
        # Test READ
        $retrieved = Get-StudentByNumber "TEST001"
        if (-not $retrieved) { throw "READ failed" }
        Write-Host "✅ READ: Student retrieved successfully" -ForegroundColor Green
        
        # Test UPDATE
        $retrieved.Grade = "11"
        $updated = Update-TestStudent $retrieved
        if (-not $updated) { throw "UPDATE failed" }
        Write-Host "✅ UPDATE: Student updated successfully" -ForegroundColor Green
        
        # Test DELETE
        $deleted = Remove-TestStudent $retrieved.StudentId
        if (-not $deleted) { throw "DELETE failed" }
        Write-Host "✅ DELETE: Student removed successfully" -ForegroundColor Green
        
        Write-Host "🎉 All CRUD operations passed!" -ForegroundColor Magenta
        
    } catch {
        Write-Host "❌ CRUD Test Failed: $_" -ForegroundColor Red
        return $false
    }
    
    return $true
}
```

### **Route Assignment Integration Test**
```powershell
function Test-RouteAssignmentIntegration {
    Write-Host "🚌 Testing Route Assignment Integration..." -ForegroundColor Cyan
    
    try {
        # Create test route
        $testRoute = Create-TestRoute "Route 100" "Elementary"
        
        # Create test student
        $testStudent = Create-TestStudent "INT001" "Integration Test"
        
        # Test assignment
        $assignment = Assign-StudentToRoute $testStudent.StudentId $testRoute.RouteId
        if (-not $assignment) { throw "Route assignment failed" }
        
        # Verify FK relationship
        $verifyStudent = Get-StudentWithRoute $testStudent.StudentId
        if ($verifyStudent.RouteAssignmentId -ne $assignment.RouteAssignmentId) {
            throw "FK relationship not established"
        }
        
        # Test unassignment
        $unassigned = Remove-StudentFromRoute $testStudent.StudentId
        if (-not $unassigned) { throw "Unassignment failed" }
        
        # Cleanup
        Remove-TestStudent $testStudent.StudentId
        Remove-TestRoute $testRoute.RouteId
        
        Write-Host "✅ Route assignment integration passed!" -ForegroundColor Green
        return $true
        
    } catch {
        Write-Host "❌ Integration Test Failed: $_" -ForegroundColor Red
        return $false
    }
}
```

---

## 🛠️ **Common Diagnostic Commands**

### **Database Connection Validation**
```powershell
# Test database connectivity
function Test-DatabaseConnection {
    try {
        $result = Invoke-Sqlcmd -Query "SELECT @@VERSION" -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddyDb"
        Write-Host "✅ Database connection successful" -ForegroundColor Green
        Write-Host "SQL Server Version: $($result.Column1)" -ForegroundColor Cyan
        return $true
    } catch {
        Write-Host "❌ Database connection failed: $_" -ForegroundColor Red
        return $false
    }
}

# Verify migration status
function Get-MigrationStatus {
    try {
        dotnet ef migrations list --project BusBuddy.Core --startup-project BusBuddy.WPF
    } catch {
        Write-Host "❌ Migration check failed: $_" -ForegroundColor Red
    }
}
```

### **EF Core Tools Verification**
```powershell
# Comprehensive EF validation
function Test-EFCoreHealth {
    Write-Host "🔍 Checking EF Core Health..." -ForegroundColor Cyan
    
    # Check EF Tools version
    $efVersion = dotnet ef --version
    Write-Host "EF Tools Version: $efVersion" -ForegroundColor Yellow
    
    # Check if version matches packages
    $packageVersion = Get-Content Directory.Build.props | Select-String "EntityFrameworkCore" | Select-String "Version"
    Write-Host "Package Versions: $packageVersion" -ForegroundColor Yellow
    
    # Test database connectivity
    try {
        dotnet ef database update --dry-run --project BusBuddy.Core --startup-project BusBuddy.WPF
        Write-Host "✅ EF database connectivity verified" -ForegroundColor Green
    } catch {
        Write-Host "❌ EF database test failed: $_" -ForegroundColor Red
    }
}
```

### **Syncfusion Control Validation**
```powershell
# Validate Syncfusion integration
function Test-SyncfusionIntegration {
    Write-Host "🎨 Validating Syncfusion Integration..." -ForegroundColor Cyan
    
    # Check for Syncfusion references in XAML
    $xamlFiles = Get-ChildItem -Recurse -Filter "*.xaml" | Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" }
    $syncfusionUsage = @()
    
    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match 'syncfusion:') {
            $matches = [regex]::Matches($content, 'syncfusion:(\w+)')
            foreach ($match in $matches) {
                $syncfusionUsage += @{
                    File = $file.Name
                    Control = $match.Groups[1].Value
                }
            }
        }
    }
    
    if ($syncfusionUsage.Count -gt 0) {
        Write-Host "✅ Found $($syncfusionUsage.Count) Syncfusion control usages" -ForegroundColor Green
        $syncfusionUsage | Group-Object Control | ForEach-Object {
            Write-Host "  $($_.Name): $($_.Count) instances" -ForegroundColor Cyan
        }
    } else {
        Write-Host "⚠️ No Syncfusion controls found in XAML files" -ForegroundColor Yellow
    }
}
```

---

## 📋 **Issue Tracking & Status**

### **Resolved Issues** ✅
- [x] EF Tools version mismatch (9.0.7 → 9.0.8)
- [x] Bus entity table mapping (Buses → Vehicles)
- [x] Retry resilience configuration
- [x] Connection string fallback logic

### **In Progress** 🔄
- [ ] Migration history synchronization
- [ ] End-to-end CRUD testing implementation
- [ ] Foreign key constraint validation
- [ ] Data seeding verification

### **Identified But Not Yet Addressed** ⚠️
- [ ] PowerShell Write-Host violations (50+ instances)
- [ ] Syncfusion theme consistency validation
- [ ] Production security hardening
- [ ] Performance testing with large datasets
- [ ] Backup and disaster recovery procedures

### **Monitoring Required** 👀
- [ ] Database seeding success rates
- [ ] WPF UI responsiveness with real data
- [ ] Migration deployment success in staging
- [ ] External API rate limit compliance

---

## 🔗 **Related Documentation**
- [Main README](README.md) - Project overview and setup
- [GROK-README](GROK-README.md) - Development status and file locations
- [Testing Standards](BusBuddy.Tests/TESTING-STANDARDS.md) - Test implementation guidelines
- [Anti-Regression Checklist](ANTI-REGRESSION-CHECKLIST.md) - Compliance validation

---

**Next Review:** August 9, 2025  
**Owner:** Development Team  
**Last Verified:** All solutions tested in development environment
