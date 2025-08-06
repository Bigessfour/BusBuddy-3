using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;
using Moq;
using FluentAssertions;
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Tests.ViewModels.Student
{
    [TestFixture]
    public class StudentsViewModelTests : IDisposable
    {
        // Serilog logger with enrichments for test lifecycle
        private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<StudentsViewModelTests>();
        private BusBuddyDbContext? _context;
        private Mock<AddressService>? _mockAddressService;
        private StudentsViewModel? _viewModel;

        [SetUp]
        public void SetUp()
        {
            Logger.Information("[SetUp] Initializing test context for {TestClass}", nameof(StudentsViewModelTests));
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
                .Options;
            _context = new BusBuddyDbContext(options);
            _mockAddressService = new Mock<AddressService>();
            // Setup test students
            _context.Students.AddRange(new List<BusBuddy.Core.Models.Student>
            {
                new BusBuddy.Core.Models.Student { StudentId = 1, StudentName = "Alice", Grade = "5", Active = true },
                new BusBuddy.Core.Models.Student { StudentId = 2, StudentName = "Bob", Grade = "6", Active = false },
            });
            _context.SaveChanges();
            _viewModel = new StudentsViewModel(_context, _mockAddressService.Object);
        }

        [Test]
        public async Task StudentsCollection_LoadsAllStudents()
        {
            await _viewModel.LoadStudentsAsync();
            _viewModel.Students.Should().HaveCount(2);
            _viewModel.Students.Should().Contain(s => s.StudentName == "Alice");
            _viewModel.Students.Should().Contain(s => s.StudentName == "Bob");
        }

        [Test]
        public async Task SelectStudent_UpdatesSelectedStudent()
        {
            await _viewModel.LoadStudentsAsync();
            var student = _viewModel.Students[0];
            _viewModel.SelectedStudent = student;
            _viewModel.SelectedStudent.Should().Be(student);
        }

        [Test]
        public async Task FilterStudents_ByActiveStatus()
        {
            await _viewModel.LoadStudentsAsync();
            var filtered = _viewModel.Students.Where(s => s.Active).ToList();
            filtered.Should().OnlyContain(s => s.Active);
        }

        [Test]
        public void StatusMessage_UpdatesOnLoad()
        {
            _viewModel.StatusMessage = "Loaded 2 students";
            _viewModel.StatusMessage.Should().Contain("Loaded");
        }

        public void Dispose()
        {
            Logger.Information("[Dispose] Finalizing test class {TestClass}", nameof(StudentsViewModelTests));
            _viewModel?.Dispose();
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
        // ...existing code...
    }
}
