using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Windows.Media;
using FluentAssertions;

namespace BusBuddy.Tests.ViewModels.Student
{
    /// <summary>
    /// Unit tests for StudentFormViewModel
    /// Tests validation, commands, and AI integration
    /// </summary>
    [TestFixture]
    public class StudentFormViewModelTests : IDisposable
    {
        // Serilog logger with enrichments for test lifecycle
        private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<StudentFormViewModelTests>();
        private Mock<BusBuddyDbContext>? _mockContext;
        private Mock<AddressService>? _mockAddressService;
        private StudentFormViewModel? _viewModel;


        [SetUp]
        public void SetUp()
        {
            Logger.Information("[SetUp] Initializing test context for {TestClass}", nameof(StudentFormViewModelTests));
            _mockContext = new Mock<BusBuddyDbContext>();
            _mockAddressService = new Mock<AddressService>();
            var testStudent = new BusBuddy.Core.Models.Student
            {
                StudentId = 1,
                StudentName = "Test Student",
                Grade = "5",
                HomeAddress = "123 Test St",
                City = "TestCity",
                State = "IL",
                Zip = "12345",
                Active = true
            };
            _viewModel = new StudentFormViewModel(testStudent, enableValidation: true);
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Information("[TearDown] Disposing test context for {TestClass}", nameof(StudentFormViewModelTests));
            _viewModel?.Dispose();
        }

        [Test]
        public void Constructor_WithStudent_SetsEditMode()
        {
            var student = new BusBuddy.Core.Models.Student { StudentId = 1, StudentName = "Test" };
            var viewModel = new StudentFormViewModel(student, enableValidation: true);
            viewModel.IsEditMode.Should().BeTrue();
            viewModel.FormTitle.Should().Be("Edit Student");
            viewModel.Dispose();
        }

        [Test]
        public void Constructor_WithoutStudent_SetsAddMode()
        {
            var viewModel = new StudentFormViewModel(enableValidation: true);
            viewModel.IsEditMode.Should().BeFalse();
            viewModel.FormTitle.Should().Be("Add New Student");
            viewModel.Dispose();
        }

        [Test]
        public void ValidateAddress_WithValidAddress_SetsSuccessMessage()
        {
            _viewModel.Student.HomeAddress = "123 Main St";
            _viewModel.Student.City = "TestCity";
            // Simulate address validation
            _viewModel.ValidateAddressCommand.Execute(null);
            _viewModel.AddressValidationMessage.Should().Contain("Address format is valid");
            _viewModel.AddressValidationColor.Should().Be(Brushes.Green);
        }

        [Test]
        public void ValidateAddress_WithInvalidAddress_SetsErrorMessage()
        {
            _viewModel.Student.HomeAddress = "Invalid"; // too short, lacks number/comma
            _viewModel.Student.City = string.Empty;
            _viewModel.Student.State = string.Empty;
            _viewModel.Student.Zip = string.Empty;
            _viewModel.ValidateAddressCommand.Execute(null);
            _viewModel.AddressValidationMessage.Should().Contain("Address validation failed");
            _viewModel.AddressValidationColor.Should().Be(Brushes.Red);
        }

        [Test]
        public void SuggestRoutes_WithValidAddress_UpdatesRoutes()
        {
            _viewModel.Student.HomeAddress = "123 Main St";
            _viewModel.Student.City = "North Chicago";
            _viewModel.SuggestRoutesCommand.Execute(null);
            _viewModel.Student.AMRoute.Should().NotBeNull();
            _viewModel.Student.AMRoute.Should().Contain("Route N");
            _viewModel.ValidationStatus.Should().Contain("AI suggested");
            _viewModel.ValidationStatusBrush.Should().Be(Brushes.Green);
        }

        [Test]
        public void SuggestRoutes_WithoutAddress_SetsGlobalError()
        {
            _viewModel.Student.HomeAddress = "";
            _viewModel.SuggestRoutesCommand.Execute(null);
            _viewModel.HasGlobalError.Should().BeTrue();
            _viewModel.GlobalErrorMessage.Should().Contain("Please enter a home address");
        }

        [Test]
        public void ViewOnMap_WithValidAddress_SetsSuccessStatus()
        {
            _viewModel.Student.HomeAddress = "123 Main St";
            _viewModel.Student.City = "TestCity";
            _viewModel.ViewOnMapCommand.Execute(null);
            _viewModel.ValidationStatus.Should().Contain("✓ Map opened successfully");
            _viewModel.ValidationStatusBrush.Should().Be(Brushes.Green);
        }

        [Test]
        public void ValidateAllData_WithMissingRequiredFields_SetsErrors()
        {
            _viewModel.Student.StudentName = "";
            _viewModel.Student.Grade = "";
            _viewModel.ValidateDataCommand.Execute(null);
            _viewModel.ValidationStatus.Should().Contain("validation errors");
            _viewModel.ValidationStatusBrush.Should().Be(Brushes.Red);
            _viewModel.HasGlobalError.Should().BeTrue();
            _viewModel.CanSave.Should().BeFalse();
        }

        [Test]
        public void ValidateAllData_WithValidData_EnablesSave()
        {
            _viewModel.Student.StudentName = "Test Student";
            _viewModel.Student.Grade = "5";
            _viewModel.Student.HomeAddress = "123 Main St";
            _viewModel.Student.City = "TestCity";
            _viewModel.Student.State = "IL";
            _viewModel.ValidateDataCommand.Execute(null);
            _viewModel.ValidationStatus.Should().Contain("All data validated successfully");
            _viewModel.ValidationStatusBrush.Should().Be(Brushes.Green);
            _viewModel.CanSave.Should().BeTrue();
        }

        [Test]
        public void ClearGlobalError_RemovesErrorState()
        {
            _viewModel.GlobalErrorMessage = "Test error";
            _viewModel.HasGlobalError = true;
            _viewModel.ClearGlobalErrorCommand.Execute(null);
            _viewModel.HasGlobalError.Should().BeFalse();
            _viewModel.GlobalErrorMessage.Should().BeEmpty();
        }

        [Theory]
        [TestCase("North Chicago", "Route N1")]
        [TestCase("South Bend", "Route S1")]
        [TestCase("Central City", "Route Central-1")]
        public void GetAISuggestedRoutes_ReturnsCorrectRoutesByLocation(string city, string expectedRoute)
        {
            // Arrange
            _viewModel.Student.City = city;
            _viewModel.Student.HomeAddress = "123 Test St";

            // Act
            _viewModel.SuggestRoutesCommand.Execute(null);

            // Assert
            _viewModel.Student.AMRoute.Should().Be(expectedRoute);
        }

        [Test]
        public void ImportCsv_SimulatesFileImport()
        {
            _viewModel.ImportCsvCommand.Execute(null);
            _viewModel.ValidationStatus.Should().Contain("✓ CSV import completed");
            _viewModel.ValidationStatusBrush.Should().Be(Brushes.Green);
        }

        [Test]
        public void AvailableRoutes_LoadsTestData()
        {
            // Assert
            _viewModel.AvailableRoutes.Should().NotBeEmpty();
            _viewModel.AvailableRoutes.Should().Contain("Route A");
        }

        [Test]
        public void AvailableBusStops_LoadsTestData()
        {
            _viewModel.AvailableBusStops.Should().NotBeEmpty();
            _viewModel.AvailableBusStops.Should().Contain("Oak & 1st");
        }

        public void Dispose()
        {
            Logger.Information("[Dispose] Finalizing test class {TestClass}", nameof(StudentFormViewModelTests));
            _viewModel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Mock classes for testing
    /// </summary>
    public class AddressValidationResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
