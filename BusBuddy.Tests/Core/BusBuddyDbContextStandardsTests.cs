using NUnit.Framework;
using FluentAssertions;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace BusBuddy.Tests.Core;

/// <summary>
/// Validates BusBuddyDbContext configuration against Microsoft EF Core standards
/// Reference: https://learn.microsoft.com/en-us/ef/core/modeling/
/// </summary>
[TestFixture]
[Category("EntityFramework")]
[Category("DbContextValidation")]
public class BusBuddyDbContextStandardsTests : IDisposable
{
    private BusBuddyDbContext _context = null!;
    private IModel _model = null!;
    private bool _disposed;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Create in-memory context to inspect model configuration
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase("DbContextStandardsTest")
            .Options;

        _context = new BusBuddyDbContext(options);
        _model = _context.Model;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Dispose();
    }

    /// <summary>
    /// Disposes the test context and suppresses finalization
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _context?.Dispose();
            }

            _disposed = true;
        }
    }

    #region Microsoft Standards Compliance

    [Test]
    [Category("StandardsCompliance")]
    public void DbContext_ShouldFollowMicrosoftConfigurationPatterns()
    {
        // Reference: https://learn.microsoft.com/en-us/ef/core/modeling/

        // Act
        var entityTypes = _model.GetEntityTypes().ToList();

        // Assert
        entityTypes.Should().NotBeEmpty("DbContext should contain configured entities");

        foreach (var entityType in entityTypes)
        {
            // 1. All entities should have table names configured
            var tableName = entityType.GetTableName();
            tableName.Should().NotBeNullOrEmpty(
                $"Entity {entityType.ClrType.Name} should have table name configured");

            // 2. All entities should have primary keys
            var primaryKey = entityType.FindPrimaryKey();
            primaryKey.Should().NotBeNull(
                $"Entity {entityType.ClrType.Name} should have primary key configured");

            // 3. Primary keys should follow Microsoft conventions (int type)
            var pkProperty = primaryKey!.Properties[0];
            pkProperty.ClrType.Should().Be<int>(
                $"Entity {entityType.ClrType.Name} should use int for primary key following Microsoft conventions");

            // 4. Primary key should be named EntityName + "Id"
            var expectedPkName = entityType.ClrType.Name + "Id";
            pkProperty.Name.Should().Be(expectedPkName,
                $"Primary key for {entityType.ClrType.Name} should be named {expectedPkName}");
        }
    }

    [Test]
    [Category("StandardsCompliance")]
    public void EntityProperties_ShouldFollowMicrosoftGuidelines()
    {
        // Reference: https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties

        // Act
        var stringProperties = _model.GetEntityTypes()
            .SelectMany(et => et.GetProperties())
            .Where(p => p.ClrType == typeof(string) && !p.IsPrimaryKey())
            .ToList();

        // Assert
        foreach (var property in stringProperties)
        {
            // String properties should have MaxLength configured
            var maxLength = property.GetMaxLength();
            maxLength.Should().NotBeNull(
                $"String property {((IEntityType)property.DeclaringType).ClrType.Name}.{property.Name} should have MaxLength configured");

            maxLength.Should().BeGreaterThan(0,
                $"String property {((IEntityType)property.DeclaringType).ClrType.Name}.{property.Name} should have positive MaxLength");
        }
    }

    [Test]
    [Category("StandardsCompliance")]
    public void Relationships_ShouldFollowMicrosoftPatterns()
    {
        // Reference: https://learn.microsoft.com/en-us/ef/core/modeling/relationships

        // Act
        var foreignKeys = _model.GetEntityTypes()
            .SelectMany(et => et.GetForeignKeys())
            .ToList();

        // Assert
        foreach (var foreignKey in foreignKeys)
        {
            // Foreign keys should have delete behavior configured
            foreignKey.DeleteBehavior.Should().NotBe(DeleteBehavior.ClientSetNull,
                $"Foreign key from {foreignKey.DeclaringEntityType.ClrType.Name} to {foreignKey.PrincipalEntityType.ClrType.Name} should have explicit delete behavior");

            // Foreign key properties should exist
            foreignKey.Properties.Should().NotBeEmpty(
                $"Foreign key from {foreignKey.DeclaringEntityType.ClrType.Name} to {foreignKey.PrincipalEntityType.ClrType.Name} should have properties");
        }
    }

    #endregion

    #region BusBuddy-Specific Standards

    [Test]
    [Category("BusBuddyStandards")]
    public void StudentEntity_ShouldFollowBusBuddyStandards()
    {
        // Arrange
        var studentEntity = _model.FindEntityType(typeof(Student));

        // Assert
        studentEntity.Should().NotBeNull("Student entity should be configured");

        // 1. Primary Key
        var pk = studentEntity!.FindPrimaryKey();
        pk.Should().NotBeNull("Student should have primary key");
        pk!.Properties[0].Name.Should().Be("StudentId");

        // 2. Required Properties
        var studentNameProperty = studentEntity.GetProperties()
            .FirstOrDefault(p => p.Name == "StudentName");
        studentNameProperty.Should().NotBeNull("StudentName property should exist");
        studentNameProperty!.GetMaxLength().Should().BeGreaterThan(0);

        // 3. Foreign Key to Route
        var routeFk = studentEntity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Route));
        routeFk.Should().NotBeNull("Student should have foreign key to Route");
        routeFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict,
            "Student-Route relationship should use Restrict delete behavior");

        // 4. Indexes
        var routeIndex = studentEntity.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Any(p => p.Name == "RouteId"));
        routeIndex.Should().NotBeNull("Student should have index on RouteId");
    }

    [Test]
    [Category("BusBuddyStandards")]
    public void RouteEntity_ShouldFollowBusBuddyStandards()
    {
        // Arrange
        var routeEntity = _model.FindEntityType(typeof(Route));

        // Assert
        routeEntity.Should().NotBeNull("Route entity should be configured");

        // 1. Primary Key
        var pk = routeEntity!.FindPrimaryKey();
        pk.Should().NotBeNull("Route should have primary key");
        pk!.Properties[0].Name.Should().Be("RouteId");

        // 2. Navigation to Students
        // trunk-ignore(spellcheck): False positive - GetNavigations() is correct EF Core method
        var studentsNavigation = routeEntity.GetNavigations()
            .FirstOrDefault(n => n.Name == "Students");
        studentsNavigation.Should().NotBeNull("Route should have Students navigation");
        studentsNavigation!.IsCollection.Should().BeTrue("Students navigation should be collection");
    }

    [Test]
    [Category("BusBuddyStandards")]
    public void RouteStopEntity_ShouldHaveCompositeIndex()
    {
        // Arrange
        var routeStopEntity = _model.FindEntityType(typeof(RouteStop));

        // Assert
        routeStopEntity.Should().NotBeNull("RouteStop entity should be configured");

        // 1. Primary Key
        var pk = routeStopEntity!.FindPrimaryKey();
        pk.Should().NotBeNull("RouteStop should have primary key");
        pk!.Properties[0].Name.Should().Be("RouteStopId");

        // 2. Composite Index
        var compositeIndex = routeStopEntity.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Count > 1);
        compositeIndex.Should().NotBeNull("RouteStop should have composite index");

        // 3. Foreign Key to Route
        var routeFk = routeStopEntity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Route));
        routeFk.Should().NotBeNull("RouteStop should have foreign key to Route");
        routeFk!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade,
            "RouteStop-Route relationship should use Cascade delete behavior");
    }

    #endregion

    #region Data Annotation Validation

    [Test]
    [Category("DataAnnotations")]
    public void StudentEntity_ShouldHaveProperDataAnnotations()
    {
        // Arrange
        var studentType = typeof(Student);
        var properties = studentType.GetProperties();

        // Act & Assert - Required properties
        var studentNameProperty = properties.FirstOrDefault(p => p.Name == "StudentName");
        studentNameProperty.Should().NotBeNull("StudentName property should exist");

        var requiredAttribute = studentNameProperty!.GetCustomAttribute<RequiredAttribute>();
        requiredAttribute.Should().NotBeNull("StudentName should have Required attribute");

        var stringLengthAttribute = studentNameProperty.GetCustomAttribute<StringLengthAttribute>();
        stringLengthAttribute.Should().NotBeNull("StudentName should have StringLength attribute");
        stringLengthAttribute!.MaximumLength.Should().BeGreaterThan(0);
    }

    [Test]
    [Category("DataAnnotations")]
    public void DriverEntity_ShouldHaveValidationAttributes()
    {
        // Arrange
        var driverType = typeof(Driver);
        var properties = driverType.GetProperties();

        // Act & Assert - Email validation
        var emailProperty = properties.FirstOrDefault(p => p.Name == "DriverEmail");
        emailProperty.Should().NotBeNull("DriverEmail property should exist");

        var emailAttribute = emailProperty!.GetCustomAttribute<EmailAddressAttribute>();
        emailAttribute.Should().NotBeNull("DriverEmail should have EmailAddress validation");

        // Phone validation
        var phoneProperty = properties.FirstOrDefault(p => p.Name == "DriverPhone");
        phoneProperty.Should().NotBeNull("DriverPhone property should exist");

        var phoneAttribute = phoneProperty!.GetCustomAttribute<PhoneAttribute>();
        phoneAttribute.Should().NotBeNull("DriverPhone should have Phone validation");
    }

    #endregion

    #region Index Validation

    [Test]
    [Category("Indexes")]
    public void Entities_ShouldHaveAppropriateIndexes()
    {
        // Act
        var entitiesWithIndexes = _model.GetEntityTypes()
            .Where(et => et.GetIndexes().Any())
            .ToList();

        // Assert
        entitiesWithIndexes.Should().NotBeEmpty("Some entities should have indexes configured");

        // Verify specific indexes exist
        var studentEntity = _model.FindEntityType(typeof(Student));
        var studentIndexes = studentEntity?.GetIndexes() ?? Enumerable.Empty<IIndex>();

        var routeIndex = studentIndexes.FirstOrDefault(idx =>
            idx.Properties.Any(p => p.Name == "RouteId"));
        routeIndex.Should().NotBeNull("Student should have index on RouteId");
    }

    #endregion

    #region Model Validation

    [Test]
    [Category("ModelValidation")]
    public void Model_ShouldBeValidAndComplete()
    {
        // Act
        var entityTypes = _model.GetEntityTypes().ToList();
        var errors = new List<string>();

        foreach (var entityType in entityTypes)
        {
            // Check for primary key
            if (entityType.FindPrimaryKey() == null)
            {
                errors.Add($"Entity {entityType.ClrType.Name} missing primary key");
            }

            // Check for table name
            if (string.IsNullOrEmpty(entityType.GetTableName()))
            {
                errors.Add($"Entity {entityType.ClrType.Name} missing table name");
            }

            // Check string properties have max length
            var stringProperties = entityType.GetProperties()
                .Where(p => p.ClrType == typeof(string) && !p.IsPrimaryKey());

            foreach (var property in stringProperties)
            {
                if (property.GetMaxLength() == null)
                {
                    errors.Add($"String property {entityType.ClrType.Name}.{property.Name} missing MaxLength");
                }
            }
        }

        // Assert
        errors.Should().BeEmpty($"Model validation errors: {string.Join(", ", errors)}");
    }

    #endregion
}
