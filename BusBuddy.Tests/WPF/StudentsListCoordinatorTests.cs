using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Inline grid save must write only the rows a clerk actually edited, and archiving must replace the
/// old hard delete. All fixtures use synthetic tokens — no student PII.
/// </summary>
[TestFixture]
[Category("Unit")]
public class StudentsListCoordinatorTests
{
    private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
    private BusBuddyDbContext _dbContext = null!;

    private sealed class TestDbContextFactory : IBusBuddyDbContextFactory
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;

        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options) => _options = options;

        public BusBuddyDbContext CreateDbContext() => new(_options);

        public BusBuddyDbContext CreateWriteDbContext() => new(_options);
    }

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"StudentsListDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new BusBuddyDbContext(_dbOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private async Task SeedAsync(params string[] names)
    {
        var id = 1;
        foreach (var name in names)
        {
            _dbContext.Students.Add(new Student
            {
                StudentName = name,
                StudentNumber = $"TEST-{id:0000}",
                Grade = "3",
                HomeAddress = $"{id}00 Test St",
                City = "Testville",
                State = "CO",
                Zip = "81000",
                SchoolYear = "2026-2027",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
            });
            id++;
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private (StudentsListCoordinator Coordinator, Mock<IStudentService> Service) CreateCoordinator()
    {
        var service = new Mock<IStudentService>();
        service
            .Setup(s => s.GetAllStudentsAsync())
            .Returns(async () =>
            {
                await using var ctx = new BusBuddyDbContext(_dbOptions);
                return await ctx.Students.AsNoTracking().OrderBy(s => s.StudentName).ToListAsync();
            });
        service
            .Setup(s => s.UpdateStudentAsync(It.IsAny<Student>()))
            .ReturnsAsync(true);

        return (new StudentsListCoordinator(new TestDbContextFactory(_dbOptions), service.Object), service);
    }

    [Test]
    public async Task SaveInlineGridEditsAsync_WritesOnlyDirtyRows()
    {
        await SeedAsync("TEST_STUDENT_01", "TEST_STUDENT_02", "TEST_STUDENT_03");
        var (coordinator, service) = CreateCoordinator();

        var loaded = await coordinator.LoadStudentsAsync();
        loaded.Should().HaveCount(3);

        var edited = loaded.Single(s => s.StudentName == "TEST_STUDENT_02");
        edited.Grade = "4";

        var (saved, errors) = await coordinator.SaveInlineGridEditsAsync(loaded, Array.Empty<Destination>());

        errors.Should().BeEmpty();
        saved.Should().Be(1, "only the edited row should be written");
        service.Verify(s => s.UpdateStudentAsync(It.IsAny<Student>()), Times.Once);
        service.Verify(s => s.UpdateStudentAsync(It.Is<Student>(s2 => s2.StudentId == edited.StudentId)), Times.Once);
    }

    [Test]
    public async Task SaveInlineGridEditsAsync_WithNoEdits_WritesNothing()
    {
        await SeedAsync("TEST_STUDENT_01", "TEST_STUDENT_02");
        var (coordinator, service) = CreateCoordinator();

        var loaded = await coordinator.LoadStudentsAsync();
        var (saved, errors) = await coordinator.SaveInlineGridEditsAsync(loaded, Array.Empty<Destination>());

        saved.Should().Be(0);
        errors.Should().BeEmpty();
        service.Verify(s => s.UpdateStudentAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public async Task SaveInlineGridEditsAsync_CosmeticPhoneReformatIsNotDirty()
    {
        await SeedAsync("TEST_STUDENT_01");
        var (coordinator, service) = CreateCoordinator();

        var loaded = await coordinator.LoadStudentsAsync();
        // Normalize once so the stored value is already canonical, then re-type it with punctuation.
        loaded[0].HomePhone = "5550000001";
        await coordinator.SaveInlineGridEditsAsync(loaded, Array.Empty<Destination>());
        service.Invocations.Clear();

        loaded[0].HomePhone = "(555) 000-0001";
        var (saved, _) = await coordinator.SaveInlineGridEditsAsync(loaded, Array.Empty<Destination>());

        saved.Should().Be(0, "normalization collapses the edit back to the stored value");
        service.Verify(s => s.UpdateStudentAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public async Task SaveInlineGridEditsAsync_AddressEditClearsCoordinates()
    {
        await SeedAsync("TEST_STUDENT_01");
        var (coordinator, _) = CreateCoordinator();

        var loaded = await coordinator.LoadStudentsAsync();
        var student = loaded[0];
        student.HasValidatedHomeCoordinates.Should().BeTrue();

        student.HomeAddress = "999 Test St";
        var (saved, errors) = await coordinator.SaveInlineGridEditsAsync(loaded, Array.Empty<Destination>());

        errors.Should().BeEmpty();
        saved.Should().Be(1);
        student.Latitude.Should().BeNull("a hand-typed address has not been validated");
        student.Longitude.Should().BeNull();
        student.HasUnvalidatedHomeAddress.Should().BeTrue();
        student.IntakeStatus.Should().Be("Address unvalidated");
    }

    [Test]
    public async Task ArchiveStudentAsync_UsesTheArchivePrimitive_NotADelete()
    {
        await SeedAsync("TEST_STUDENT_01");
        var (coordinator, service) = CreateCoordinator();
        service.Setup(s => s.ArchiveStudentAsync(It.IsAny<int>())).ReturnsAsync(true);

        var loaded = await coordinator.LoadStudentsAsync();
        var archived = await coordinator.ArchiveStudentAsync(loaded[0]);

        archived.Should().BeTrue();
        loaded[0].Active.Should().BeFalse();
        service.Verify(s => s.ArchiveStudentAsync(loaded[0].StudentId), Times.Once);
    }

}
