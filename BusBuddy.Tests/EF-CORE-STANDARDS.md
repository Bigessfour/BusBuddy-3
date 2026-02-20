# EF Core Standards and Best Practices - BusBuddy Implementation

## Overview

This document establishes Microsoft Entity Framework Core documentation as the authoritative standard for all database-related development in the BusBuddy project. All EF Core implementations must follow these standards to ensure consistency, performance, and maintainability.

## Authoritative References

### Primary Documentation Sources

- **Microsoft EF Core Documentation**: https://learn.microsoft.com/en-us/ef/core/
- **EF Core Modeling**: https://learn.microsoft.com/en-us/ef/core/modeling/
- **EF Core Performance**: https://learn.microsoft.com/en-us/ef/core/performance/
- **EF Core Testing**: https://learn.microsoft.com/en-us/ef/core/testing/

### Key Standards Documents

- **Entity Configuration**: https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties
- **Relationships**: https://learn.microsoft.com/en-us/ef/core/modeling/relationships
- **Indexes**: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- **Data Types**: https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties

## Entity Design Standards

### 1. Primary Key Conventions

```csharp
// ✅ CORRECT: Follow Microsoft int PK convention
public class Student
{
    [Key]
    public int StudentId { get; set; } // Primary key as int

    // Other properties...
}

// ❌ AVOID: Non-standard primary keys without justification
public class Student
{
    [Key]
    public Guid StudentId { get; set; } // Only use Guid if specifically required
}
```

**Standards**:

- Use `int` for primary keys unless there's a documented business requirement for alternative types
- Primary key property name should match entity name + "Id" (e.g., `StudentId`, `RouteId`)
- Use `[Key]` attribute for explicit primary key declaration

### 2. Property Configuration

```csharp
// ✅ CORRECT: Proper property configuration with validation
public class Student
{
    [Key]
    public int StudentId { get; set; }

    [Required]
    [StringLength(100)]
    public string StudentName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Required]
    public int RouteId { get; set; }

    [ForeignKey("RouteId")]
    public virtual Route Route { get; set; } = null!;
}
```

**Standards**:

- Use data annotations for validation (`[Required]`, `[StringLength]`, `[EmailAddress]`, etc.)
- Configure string properties with `[StringLength]` to optimize database storage
- Use appropriate data types (e.g., `decimal` for monetary values, `DateTime` for dates)
- Navigation properties should be virtual for lazy loading

### 3. Relationship Configuration

```csharp
// ✅ CORRECT: Explicit relationship configuration
public class Student
{
    [Key]
    public int StudentId { get; set; }

    [Required]
    public int RouteId { get; set; }

    [ForeignKey("RouteId")]
    public virtual Route Route { get; set; } = null!;
}

public class Route
{
    [Key]
    public int RouteId { get; set; }

    [StringLength(200)]
    public string RouteName { get; set; } = string.Empty;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
```

**Standards**:

- Use `[ForeignKey]` attribute to explicitly define foreign key relationships
- Configure navigation properties as virtual for EF Core lazy loading
- Use `ICollection<T>` for one-to-many relationships
- Initialize navigation collections to prevent null reference exceptions

## DbContext Configuration Standards

### 1. OnModelCreating Method Structure

```csharp
// ✅ CORRECT: Organized OnModelCreating following Microsoft patterns
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure entities in logical groups
    ConfigureStudents(modelBuilder);
    ConfigureRoutes(modelBuilder);
    ConfigureRelationships(modelBuilder);
    ConfigureIndexes(modelBuilder);
}

private void ConfigureStudents(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Student>(entity =>
    {
        entity.ToTable("Students");

        entity.HasKey(e => e.StudentId);

        entity.Property(e => e.StudentName)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(e => e.Email)
            .HasMaxLength(255);

        // Configure relationships
        entity.HasOne(e => e.Route)
            .WithMany(r => r.Students)
            .HasForeignKey(e => e.RouteId)
            .OnDelete(DeleteBehavior.Restrict);
    });
}
```

**Standards**:

- Organize `OnModelCreating` with private methods for each entity/configuration group
- Use fluent API for complex configurations
- Follow Microsoft's entity configuration patterns
- Group related configurations together

### 2. Index Configuration

```csharp
// ✅ CORRECT: Performance-optimized indexes
private void ConfigureIndexes(ModelBuilder modelBuilder)
{
    // Single column indexes
    modelBuilder.Entity<Student>()
        .HasIndex(e => e.RouteId)
        .HasDatabaseName("IX_Students_RouteId");

    // Composite indexes for common query patterns
    modelBuilder.Entity<RouteStop>()
        .HasIndex(e => new { e.RouteId, e.StopOrder })
        .HasDatabaseName("IX_RouteStops_RouteOrder");

    // Unique indexes where needed
    modelBuilder.Entity<Driver>()
        .HasIndex(e => e.LicenseNumber)
        .HasDatabaseName("IX_Drivers_LicenseNumber")
        .IsUnique();
}
```

**Standards**:

- Create indexes for frequently queried columns
- Use composite indexes for multi-column WHERE clauses
- Name indexes with `IX_[TableName]_[ColumnName]` convention
- Use unique indexes for business key constraints

### 3. Relationship Constraints

```csharp
// ✅ CORRECT: Proper relationship constraints
private void ConfigureRelationships(ModelBuilder modelBuilder)
{
    // One-to-Many: Route -> Students
    modelBuilder.Entity<Student>()
        .HasOne(s => s.Route)
        .WithMany(r => r.Students)
        .HasForeignKey(s => s.RouteId)
        .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

    // One-to-Many: Route -> RouteStops
    modelBuilder.Entity<RouteStop>()
        .HasOne(rs => rs.Route)
        .WithMany(r => r.RouteStops)
        .HasForeignKey(rs => rs.RouteId)
        .OnDelete(DeleteBehavior.Cascade); // Allow cascade delete for dependent data
}
```

**Standards**:

- Use `DeleteBehavior.Restrict` for important relationships to prevent accidental data loss
- Use `DeleteBehavior.Cascade` only for dependent/child entities
- Configure all relationships explicitly in `OnModelCreating`

## Testing Standards

### 1. Entity Configuration Testing

```csharp
[TestFixture]
[Category("EntityFramework")]
public class EntityConfigurationTests
{
    private BusBuddyDbContext _context = null!;
    private IModel _model = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase("EntityConfigurationTest")
            .Options;

        _context = new BusBuddyDbContext(options);
        _model = _context.Model;
    }

    [Test]
    public void Student_ShouldHaveRequiredRouteRelationship()
    {
        // Arrange
        var studentEntity = _model.FindEntityType(typeof(Student));

        // Act
        var routeFk = studentEntity?.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Route));

        // Assert
        routeFk.Should().NotBeNull("Student should have foreign key to Route");
        routeFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict,
            "Student-Route relationship should use Restrict delete behavior");
    }
}
```

**Standards**:

- Test entity configurations using EF Core metadata API
- Validate relationships, indexes, and constraints
- Use FluentAssertions for readable test assertions
- Reference Microsoft testing documentation

### 2. Data Validation Testing

```csharp
[Test]
public void StudentEntity_ShouldHaveRequiredDataAnnotations()
{
    // Arrange
    var studentType = typeof(Student);
    var properties = studentType.GetProperties();

    // Act & Assert
    var studentNameProperty = properties.FirstOrDefault(p => p.Name == "StudentName");
    studentNameProperty.Should().NotBeNull("StudentName property should exist");

    var requiredAttribute = studentNameProperty!.GetCustomAttribute<RequiredAttribute>();
    requiredAttribute.Should().NotBeNull("StudentName should have Required attribute");

    var stringLengthAttribute = studentNameProperty.GetCustomAttribute<StringLengthAttribute>();
    stringLengthAttribute.Should().NotBeNull("StudentName should have StringLength attribute");
}
```

**Standards**:

- Test data annotations using reflection
- Validate validation attributes are properly applied
- Ensure property configurations match entity requirements

## Performance Standards

### 1. Query Optimization

```csharp
// ✅ CORRECT: Optimized queries following Microsoft patterns
public async Task<List<Student>> GetStudentsByRouteAsync(int routeId)
{
    return await _context.Students
        .Where(s => s.RouteId == routeId)
        .Include(s => s.Route) // Explicit include for navigation properties
        .AsNoTracking() // Use AsNoTracking for read-only queries
        .ToListAsync();
}
```

**Standards**:

- Use `AsNoTracking()` for read-only queries
- Use `Include()` judiciously to prevent N+1 queries
- Use `Select()` for projection when only specific properties are needed
- Consider using `IQueryable<T>` for composable queries

### 2. Change Tracking

```csharp
// ✅ CORRECT: Proper change tracking management
public async Task UpdateStudentAsync(Student student)
{
    _context.Students.Update(student);
    await _context.SaveChangesAsync();
}

// For bulk operations, consider disabling change tracking
public async Task BulkUpdateStudentsAsync(IEnumerable<Student> students)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    foreach (var student in students)
    {
        _context.Students.Update(student);
    }

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
```

**Standards**:

- Use appropriate change tracking methods (`Add`, `Update`, `Remove`)
- Consider bulk operations for large datasets
- Use transactions for multiple related operations

## Migration Standards

### 1. Migration Naming

```bash
# ✅ CORRECT: Descriptive migration names
dotnet ef migrations add AddRouteStopEntity
dotnet ef migrations add AddStudentRouteIndex
dotnet ef migrations add UpdateStudentEmailValidation
```

**Standards**:

- Use descriptive names that explain the change
- Follow PascalCase naming convention
- Include entity names when relevant

### 2. Migration Content

```csharp
// ✅ CORRECT: Well-structured migrations
public partial class AddRouteStopEntity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RouteStops",
            columns: table => new
            {
                RouteStopId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RouteId = table.Column<int>(type: "int", nullable: false),
                StopName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StopOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RouteStops", x => x.RouteStopId);
                table.ForeignKey(
                    name: "FK_RouteStops_Routes_RouteId",
                    column: x => x.RouteId,
                    principalTable: "Routes",
                    principalColumn: "RouteId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RouteStops_RouteId",
            table: "RouteStops",
            column: "RouteId");

        migrationBuilder.CreateIndex(
            name: "IX_RouteStops_RouteOrder",
            table: "RouteStops",
            columns: new[] { "RouteId", "StopOrder" });
    }
}
```

**Standards**:

- Use explicit column types and constraints
- Name foreign keys and indexes consistently
- Include appropriate data annotations in migration

## Code Analysis and Enforcement

### 1. Roslyn Analyzers Configuration

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisMode>Recommended</AnalysisMode>
  <AnalysisLevel>latest</AnalysisLevel>
</PropertyGroup>

<ItemGroup>
  <!-- EF Core specific analyzers -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Analyzers" Version="9.0.0" />
</ItemGroup>
```

### 2. EditorConfig Rules

```editorconfig
# EF Core specific rules
dotnet_diagnostic. EF1001.severity = error    # Use of IDbContextFactory
dotnet_diagnostic. EF1002.severity = error    # Constructor injection
dotnet_diagnostic. EF1003.severity = warning  # Query compilation issues
```

### 3. Code Comments Standards

```csharp
/// <summary>
/// Retrieves students for a specific route with optimized query.
/// Reference: https://learn.microsoft.com/en-us/ef/core/performance/
/// </summary>
/// <param name="routeId">The route identifier</param>
/// <returns>List of students for the route</returns>
public async Task<List<Student>> GetStudentsByRouteAsync(int routeId)
{
    // Use AsNoTracking for read-only queries to improve performance
    // Reference: https://learn.microsoft.com/en-us/ef/core/querying/tracking
    return await _context.Students
        .Where(s => s.RouteId == routeId)
        .AsNoTracking()
        .ToListAsync();
}
```

**Standards**:

- Include Microsoft documentation references in code comments
- Explain performance optimizations with documentation links
- Document complex EF Core patterns with references

## Compliance Validation

### 1. Automated Testing

- Run entity configuration tests as part of CI/CD pipeline
- Validate all entities follow established patterns
- Check for compliance with Microsoft guidelines

### 2. Code Review Checklist

- [ ] Entity follows Microsoft naming conventions
- [ ] Primary key uses int type (unless documented exception)
- [ ] Relationships properly configured with foreign keys
- [ ] Data annotations applied for validation
- [ ] Indexes created for frequently queried columns
- [ ] Navigation properties configured as virtual
- [ ] String properties have MaxLength specified
- [ ] Migration follows naming and structure standards

### 3. Documentation Requirements

- All EF Core implementations must reference Microsoft documentation
- Code comments should include relevant documentation links
- Complex configurations should be explained with references

## References

1. **EF Core Documentation**: https://learn.microsoft.com/en-us/ef/core/
2. **Modeling Entities**: https://learn.microsoft.com/en-us/ef/core/modeling/
3. **Performance Best Practices**: https://learn.microsoft.com/en-us/ef/core/performance/
4. **Testing EF Core**: https://learn.microsoft.com/en-us/ef/core/testing/
5. **Migrations**: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/

This document establishes Microsoft EF Core standards as the authoritative source for all database-related development in BusBuddy. All implementations must comply with these standards and reference the official Microsoft documentation.
