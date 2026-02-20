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
using System.Diagnostics;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Comprehensive EF Core entity configuration tests following Microsoft documentation standards
    /// Validates entity relationships, constraints, indexes, and data annotations
    /// Reference: https://learn.microsoft.com/en-us/ef/core/modeling/
    /// </summary>
    [TestFixture]
    [Category("EntityFramework")]
    [Category("ModelConfiguration")]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class EntityConfigurationTests : IDisposable
{
    private BusBuddyDbContext _context = null!;
    private IModel _model = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Create in-memory context for model inspection
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase("EntityConfigurationTest")
            .Options;

        _context = new BusBuddyDbContext(options);
        _model = _context.Model;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _context?.Dispose();
    }

    #region Entity Discovery and Basic Configuration

    [Test]
    [Category("EntityDiscovery")]
    public void AllExpectedEntities_ShouldBeConfigured()
    {
        // Arrange - Expected entities based on domain model
        var expectedEntities = new[]
        {
            typeof(Student),
            typeof(Driver),
            typeof(Bus),
            typeof(Route),
            typeof(RouteStop),
            typeof(BusBuddy.Core.Domain.Activity),
            typeof(FuelRecord)
        };

        // Act
        var configuredEntities = _model.GetEntityTypes()
            .Select(et => et.ClrType)
            .ToList();

        // Assert
        foreach (var expectedEntity in expectedEntities)
        {
            configuredEntities.Should().Contain(expectedEntity,
                $"Entity {expectedEntity.Name} should be configured in DbContext");
        }
    }

    [Test]
    [Category("TableNaming")]
    public void EntityTableNames_ShouldFollowConvention()
    {
        // Act
        var entityTableMappings = _model.GetEntityTypes()
            .ToDictionary(
                et => et.ClrType.Name,
                et => et.GetTableName()
            );

        // Assert - Verify table names follow EF Core conventions
        foreach (var mapping in entityTableMappings)
        {
            mapping.Value.Should().NotBeNullOrEmpty($"Table name for {mapping.Key} should not be null");
            mapping.Value.Should().Be(mapping.Key, $"Table name should match entity name by default: {mapping.Key}");
        }
    }

    #endregion

    #region Primary Key Configuration

    [Test]
    [Category("PrimaryKeys")]
    public void AllEntities_ShouldHavePrimaryKey()
    {
        // Act
        var entitiesWithoutPrimaryKey = _model.GetEntityTypes()
            .Where(et => et.FindPrimaryKey() == null)
            .Select(et => et.ClrType.Name)
            .ToList();

        // Assert
        entitiesWithoutPrimaryKey.Should().BeEmpty(
            $"All entities must have a primary key. Missing for: {string.Join(", ", entitiesWithoutPrimaryKey)}");
    }

    [Test]
    [Category("PrimaryKeys")]
    public void PrimaryKeys_ShouldHaveAppropriateDataTypes()
    {
        // Act & Assert
        var studentEntity = _model.FindEntityType(typeof(Student));
        var studentPk = studentEntity?.FindPrimaryKey();

        studentPk.Should().NotBeNull("Student should have a primary key");
        studentPk!.Properties.Should().HaveCount(1, "Primary key should be single property");

        var studentPrimaryKeyProperty = studentPk.Properties[0];
        studentPrimaryKeyProperty.ClrType.Should().Be<int>("Primary key should be int for Student");
        studentPrimaryKeyProperty.Name.Should().Be("StudentId", "Primary key property should be named StudentId");
    }

    #endregion

    #region Relationship Configuration

    [Test]
    [Category("Relationships")]
    public void Student_ShouldHaveRequiredRouteRelationship()
    {
        // Arrange
        var studentEntity = _model.FindEntityType(typeof(Student));

        // Act
        // trunk-ignore(spellcheck/Navigations): False positive - GetNavigations() is correct EF Core method
        var routeNavigation = studentEntity?.GetNavigations()
            .FirstOrDefault(n => n.Name == "Route");

        // Assert
        routeNavigation.Should().NotBeNull("Student should have Route navigation property");

        var routeFk = studentEntity?.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Route));

        routeFk.Should().NotBeNull("Student should have foreign key to Route");
        routeFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict,
            "Student-Route relationship should use Restrict delete behavior");
    }

    [Test]
    [Category("Relationships")]
    public void Route_ShouldHaveManyStudentsRelationship()
    {
        // Arrange
        var routeEntity = _model.FindEntityType(typeof(Route));

        // Act
        var studentsNavigation = routeEntity?.GetNavigations()
            .FirstOrDefault(n => n.Name == "Students");

        // Assert
        studentsNavigation.Should().NotBeNull("Route should have Students navigation property");
        studentsNavigation!.IsCollection.Should().BeTrue("Students navigation should be a collection");
    }

    [Test]
    [Category("Relationships")]
    public void RouteStop_ShouldHaveRequiredRouteRelationship()
    {
        // Arrange
        var routeStopEntity = _model.FindEntityType(typeof(RouteStop));

        // Act
        var routeFk = routeStopEntity?.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Route));

        // Assert
        routeFk.Should().NotBeNull("RouteStop should have foreign key to Route");
        routeFk!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade,
            "RouteStop-Route relationship should use Cascade delete behavior");
    }

    #endregion

    #region Index Configuration

    [Test]
    [Category("Indexes")]
    public void RouteStops_ShouldHaveCompositeIndex()
    {
        // Arrange
        var routeStopEntity = _model.FindEntityType(typeof(RouteStop));

        // Act
        var compositeIndex = routeStopEntity?.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Count > 1);

        // Assert
        compositeIndex.Should().NotBeNull("RouteStop should have a composite index");
        compositeIndex!.Properties.Should().HaveCountGreaterThan(1,
            "Composite index should include multiple properties");
    }

    [Test]
    [Category("Indexes")]
    public void Student_ShouldHaveRouteIndex()
    {
        // Arrange
        var studentEntity = _model.FindEntityType(typeof(Student));

        // Act
        var routeIndex = studentEntity?.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Any(p => p.Name == "RouteId"));

        // Assert
        routeIndex.Should().NotBeNull("Student should have an index on RouteId for query performance");
    }

    #endregion

    #region Data Annotation Validation

    [Test]
    [Category("DataAnnotations")]
    public void StudentEntity_ShouldHaveRequiredDataAnnotations()
    {
        // Arrange
        var studentType = typeof(Student);
        var properties = studentType.GetProperties();

        // Act & Assert - Required properties should have Required attribute
        var studentNameProperty = properties.FirstOrDefault(p => p.Name == "StudentName");
        studentNameProperty.Should().NotBeNull("StudentName property should exist");

        var requiredAttribute = studentNameProperty!.GetCustomAttribute<RequiredAttribute>();
        requiredAttribute.Should().NotBeNull("StudentName should have Required attribute");

        // String length validation
        var stringLengthAttribute = studentNameProperty.GetCustomAttribute<StringLengthAttribute>();
        stringLengthAttribute.Should().NotBeNull("StudentName should have StringLength attribute");
        stringLengthAttribute!.MaximumLength.Should().BeGreaterThan(0,
            "StudentName should have maximum length specified");
    }

    [Test]
    [Category("DataAnnotations")]
    public void DriverEntity_ShouldHaveValidationAttributes()
    {
        // Arrange
        var driverType = typeof(Driver);
        var properties = driverType.GetProperties();

        // Act & Assert - Email property validation
        var emailProperty = properties.FirstOrDefault(p => p.Name == "DriverEmail");
        emailProperty.Should().NotBeNull("DriverEmail property should exist");

        var emailAttribute = emailProperty!.GetCustomAttribute<EmailAddressAttribute>();
        emailAttribute.Should().NotBeNull("DriverEmail should have EmailAddress validation");

        // Phone property validation
        var phoneProperty = properties.FirstOrDefault(p => p.Name == "DriverPhone");
        phoneProperty.Should().NotBeNull("DriverPhone property should exist");

        var phoneAttribute = phoneProperty!.GetCustomAttribute<PhoneAttribute>();
        phoneAttribute.Should().NotBeNull("DriverPhone should have Phone validation");
    }

    #endregion

    #region Property Configuration

    [Test]
    [Category("PropertyConfiguration")]
    public void StringProperties_ShouldHaveMaxLength()
    {
        // Act
        var stringPropertiesWithoutMaxLength = _model.GetEntityTypes()
            .SelectMany(et => et.GetProperties())
            .Where(p => p.ClrType == typeof(string) &&
                       p.GetMaxLength() == null &&
                       !p.IsPrimaryKey())
            .Select(p => $"{((IEntityType)p.DeclaringType).ClrType.Name}.{p.Name}")
            .ToList();

        // Assert
        stringPropertiesWithoutMaxLength.Should().BeEmpty(
            $"String properties should have MaxLength configured: {string.Join(", ", stringPropertiesWithoutMaxLength)}");
    }

    [Test]
    [Category("PropertyConfiguration")]
    public void DecimalProperties_ShouldHavePrecisionConfigured()
    {
        // Act
        var decimalProperties = _model.GetEntityTypes()
            .SelectMany(et => et.GetProperties())
            .Where(p => p.ClrType == typeof(decimal))
            .ToList();

        // Assert - All decimal properties should have precision/scale configured
        foreach (var property in decimalProperties)
        {
            var precision = property.GetPrecision();
            var scale = property.GetScale();

            precision.Should().NotBeNull($"Decimal property {((IEntityType)property.DeclaringType).ClrType.Name}.{property.Name} should have precision configured");
            scale.Should().NotBeNull($"Decimal property {((IEntityType)property.DeclaringType).ClrType.Name}.{property.Name} should have scale configured");
        }
    }

    #endregion

    #region Model Builder Configuration Validation

    [Test]
    [Category("ModelBuilder")]
    public void ModelBuilderConfigurations_ShouldBeApplied()
    {
        // This test validates that OnModelCreating configurations are properly applied
        // Act
        var entityTypes = _model.GetEntityTypes().ToList();

        // Assert
        entityTypes.Should().NotBeEmpty("Model should contain configured entity types");

        foreach (var entityType in entityTypes)
        {
            // Each entity should have a table name configured
            entityType.GetTableName().Should().NotBeNullOrEmpty(
                $"Entity {entityType.ClrType.Name} should have table name configured");

            // Each entity should have a primary key
            entityType.FindPrimaryKey().Should().NotBeNull(
                $"Entity {entityType.ClrType.Name} should have primary key configured");
        }
    }

    #endregion

    #region Microsoft Standards Compliance

    [Test]
    [Category("StandardsCompliance")]
    public void EntityConfiguration_ShouldFollowMicrosoftGuidelines()
    {
        // Reference: https://learn.microsoft.com/en-us/ef/core/modeling/

        // Act & Assert - Validate common Microsoft EF Core patterns

        // 1. All entities should use int primary keys (unless specified otherwise)
        var nonIntPrimaryKeys = _model.GetEntityTypes()
            .Where(et => et.FindPrimaryKey()?.Properties[0].ClrType != typeof(int))
            .Select(et => $"{et.ClrType.Name} ({et.FindPrimaryKey()?.Properties[0].ClrType.Name})")
            .ToList();

        // Allow Guid for some entities if documented
        var allowedNonIntKeys = new[] { "ActivityId" }; // Add any Guid keys here

        foreach (var entity in nonIntPrimaryKeys)
        {
            var keyName = entity.Split(' ')[0];
            allowedNonIntKeys.Should().Contain(keyName,
                $"Entity {entity} uses non-int primary key. Should follow Microsoft int PK guideline unless documented exception.");
        }

        // 2. Navigation properties should be properly configured
        var entitiesWithNavigationIssues = _model.GetEntityTypes()
            .Where(et => et.GetNavigations().Any(n => n.ForeignKey == null))
            .Select(et => et.ClrType.Name)
            .ToList();

        entitiesWithNavigationIssues.Should().BeEmpty(
            $"Navigation properties should have foreign keys configured: {string.Join(", ", entitiesWithNavigationIssues)}");
    }

    #endregion

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    public void Dispose()
    {
        // Suppress finalization to prevent derived types from needing to re-implement IDisposable
        GC.SuppressFinalize(this);
    }
}
}
