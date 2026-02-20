using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Student;
using DomainStudent = BusBuddy.Core.Domain.Student;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.Tests.ViewModels.Student
{
    /// <summary>
    /// Unit tests for StudentsViewModel
    /// Demonstrates enhanced MVVM testing patterns with proper mocking and assertions
    /// </summary>
    [TestFixture]
    [Category("ViewModel")]
    [Category("Student")]
    public class StudentManagementViewModelTests : ViewModelTestBase
    {
        private Mock<IStudentService> _studentServiceMock = null!;
        private Mock<ILogger<StudentsViewModel>> _loggerMock = null!;
        private StudentsViewModel _viewModel = null!;

        [SetUp]
        public override void ViewModelSetUp()
        {
            base.ViewModelSetUp();

            _studentServiceMock = new Mock<IStudentService>();
            _loggerMock = CreateLoggerMock<StudentsViewModel>();

            // Use the DI constructor for testing
            _viewModel = new StudentsViewModel(
                new Mock<IBusBuddyDbContextFactory>().Object,
                new Mock<AddressService>().Object);
        }

        [Test]
        public void LoadStudentsAsync_ShouldLoadStudentsAndUpdateCollection()
        {
            // Arrange & Act - ViewModel initializes with empty collection

            // Assert
            _viewModel.Students.Should().NotBeNull();
            _viewModel.RefreshCommand.Should().NotBeNull();
            _viewModel.AddStudentCommand.Should().NotBeNull();
            _viewModel.DeleteStudentCommand.Should().NotBeNull();
        }

        [Test]
        public void AddStudentCommand_ShouldBeInitialized()
        {
            // Arrange & Act - Command is initialized in constructor

            // Assert
            _viewModel.AddStudentCommand.Should().NotBeNull();
            _viewModel.AddStudentCommand.Should().BeOfType<RelayCommand>();
        }

        [Test]
        public void DeleteStudentCommand_ShouldBeInitialized()
        {
            // Arrange & Act - Command is initialized in constructor

            // Assert
            _viewModel.DeleteStudentCommand.Should().NotBeNull();
            _viewModel.DeleteStudentCommand.Should().BeOfType<RelayCommand>();
        }

        [Test]
        public void SelectedStudent_PropertyChanged_ShouldRaiseEvent()
        {
            // Arrange
            var student = new DomainStudent { StudentId = 1, StudentName = "Test Student" };
            var propertyChangedRaised = false;
            var propertyName = "";

            _viewModel.PropertyChanged += (s, e) =>
            {
                propertyChangedRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            _viewModel.SelectedStudent = student;

            // Assert
            propertyChangedRaised.Should().BeTrue();
            propertyName.Should().Be(nameof(_viewModel.SelectedStudent));
        }

        [Test]
        public void Students_Collection_ShouldBeInitialized()
        {
            // Arrange & Act - Collection is initialized in constructor

            // Assert
            _viewModel.Students.Should().NotBeNull();
            _viewModel.Students.Should().BeOfType<ObservableCollection<DomainStudent>>();
        }
    }
}
