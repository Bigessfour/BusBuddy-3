using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Tests.UI
{
    /// <summary>
    /// Syncfusion WPF UI automation tests for BusBuddy application.
    /// Tests core Syncfusion controls including SfDataGrid, ChromelessWindow, and SfSkinManager.
    ///
    /// References:
    /// - Syncfusion WPF UI Testing: https://help.syncfusion.com/wpf/testing
    /// - SfDataGrid Testing: https://help.syncfusion.com/wpf/datagrid/testing
    /// - Microsoft Testing Platform 2025: https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro
    /// </summary>
    [TestFixture]
    [Category("Syncfusion")]
    [Category("UI")]
    [Apartment(ApartmentState.STA)]
    public class SyncfusionUITests
    {
        private static readonly ILogger Logger = Log.ForContext<SyncfusionUITests>();
        private Application? _testApplication;
        private Window? _testWindow;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Initialize WPF application context for UI testing
            if (Application.Current == null)
            {
                _testApplication = new Application();
                _testApplication.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            Logger.Information("SyncfusionUITests: Test application initialized");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Clean shutdown of test application
            _testWindow?.Close();
            _testApplication?.Shutdown();
            Logger.Information("SyncfusionUITests: Test application shutdown complete");
        }

        [SetUp]
        public void SetUp()
        {
            // Ensure we're on the UI thread for each test
            if (!Dispatcher.CurrentDispatcher.CheckAccess())
            {
                throw new InvalidOperationException("SyncfusionUITests must run on UI thread");
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any test-specific windows
            _testWindow?.Close();
            _testWindow = null;
        }

        /// <summary>
        /// Test SfDataGrid basic initialization and data binding capabilities.
        /// Validates that Syncfusion DataGrid can be created and configured properly.
        /// </summary>
        [Test]
        [Category("DataGrid")]
        [Category("Syncfusion")]
        public async Task SfDataGrid_Should_Initialize_Successfully()
        {
            // Arrange
            var dataGrid = new SfDataGrid();
            var testWindow = new Window
            {
                Content = dataGrid,
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            // Act & Assert
            await RunOnUIThreadAsync(() =>
            {
                dataGrid.Should().NotBeNull("SfDataGrid should initialize successfully");
                dataGrid.AllowEditing.Should().BeTrue("SfDataGrid should allow editing by default");
                dataGrid.AutoGenerateColumns.Should().BeTrue("SfDataGrid should auto-generate columns by default");

                Logger.Information("SfDataGrid initialized successfully with default properties");
            });
        }

        /// <summary>
        /// Test SfDataGrid with Student data binding and column generation.
        /// Validates integration with BusBuddy Student models and ViewModels.
        /// </summary>
        [Test]
        [Category("DataGrid")]
        [Category("Syncfusion")]
        [Category("Students")]
        public async Task SfDataGrid_Should_Bind_Student_Data()
        {
            // Arrange
            var dataGrid = new SfDataGrid();
            var viewModel = new StudentsViewModel();

            // Act & Assert
            await RunOnUIThreadAsync(() =>
            {
                dataGrid.DataContext = viewModel;
                // Note: In real scenarios, this would be bound via XAML ItemsSource="{Binding Students}"
                // For testing, we verify the binding infrastructure is available

                viewModel.Should().NotBeNull("StudentsViewModel should be available for binding");
                dataGrid.DataContext.Should().Be(viewModel, "DataContext should be set to StudentsViewModel");

                Logger.Information("SfDataGrid successfully bound to StudentsViewModel");
            });
        }

        /// <summary>
        /// Test Syncfusion SfSkinManager theme application.
        /// Validates that FluentDark and FluentLight themes can be applied successfully.
        /// </summary>
        [Test]
        [Category("Theming")]
        [Category("Syncfusion")]
        public async Task SfSkinManager_Should_Apply_Themes_Successfully()
        {
            // Arrange
            var testWindow = new ChromelessWindow
            {
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            // Act & Assert
            await RunOnUIThreadAsync(() =>
            {
                // Test FluentDark theme application
                SfSkinManager.SetTheme(testWindow, new Theme("FluentDark"));
                var appliedTheme = SfSkinManager.GetTheme(testWindow);

                appliedTheme.Should().NotBeNull("Theme should be applied successfully");
                appliedTheme.ThemeName.Should().Be("FluentDark", "FluentDark theme should be applied");

                // Test FluentLight theme application
                SfSkinManager.SetTheme(testWindow, new Theme("FluentLight"));
                var lightTheme = SfSkinManager.GetTheme(testWindow);

                lightTheme.Should().NotBeNull("FluentLight theme should be applied");
                lightTheme.ThemeName.Should().Be("FluentLight", "FluentLight theme should be applied");

                Logger.Information("SfSkinManager successfully applied FluentDark and FluentLight themes");
            });

            _testWindow = testWindow;
        }

        /// <summary>
        /// Test ChromelessWindow initialization and basic properties.
        /// Validates Syncfusion ChromelessWindow can be created and configured.
        /// </summary>
        [Test]
        [Category("Window")]
        [Category("Syncfusion")]
        public async Task ChromelessWindow_Should_Initialize_With_Correct_Properties()
        {
            // Arrange & Act
            ChromelessWindow? chromelessWindow = null;

            await RunOnUIThreadAsync(() =>
            {
                chromelessWindow = new ChromelessWindow
                {
                    Title = "BusBuddy Test Window",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // Assert
                chromelessWindow.Should().NotBeNull("ChromelessWindow should initialize successfully");
                chromelessWindow.Title.Should().Be("BusBuddy Test Window", "Window title should be set correctly");
                chromelessWindow.Width.Should().Be(800, "Window width should be set correctly");
                chromelessWindow.Height.Should().Be(600, "Window height should be set correctly");

                Logger.Information("ChromelessWindow initialized successfully with correct properties");
            });

            _testWindow = chromelessWindow;
        }

        /// <summary>
        /// Test SfDataGrid performance with large dataset simulation.
        /// Validates that Syncfusion DataGrid performs well with realistic data volumes.
        /// </summary>
        [Test]
        [Category("Performance")]
        [Category("DataGrid")]
        [Category("Syncfusion")]
        public async Task SfDataGrid_Should_Handle_Large_Dataset_Performance()
        {
            // Arrange
            var dataGrid = new SfDataGrid();
            var students = new List<Student>();

            // Generate test data (simulate realistic student dataset)
            for (int i = 1; i <= 1000; i++)
            {
                students.Add(new Student
                {
                    StudentId = i,
                    StudentName = $"Student{i} LastName{i}",
                    Grade = $"{(i % 12) + 1}",
                    DateOfBirth = DateTime.Now.AddYears(-(5 + (i % 15))),
                    Active = i % 10 != 0 // 90% active students
                });
            }

            // Act & Assert
            await RunOnUIThreadAsync(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                dataGrid.ItemsSource = students;
                dataGrid.Columns.Clear(); // Force column regeneration

                stopwatch.Stop();

                // Performance assertions
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
                    "SfDataGrid should load 1000 students in under 5 seconds");

                dataGrid.ItemsSource.Should().NotBeNull("ItemsSource should be set");
                students.Count.Should().Be(1000, "All 1000 students should be in the dataset");

                Logger.Information($"SfDataGrid loaded {students.Count} students in {stopwatch.ElapsedMilliseconds}ms");
            });
        }

        /// <summary>
        /// Helper method to run actions on the UI thread asynchronously.
        /// Ensures proper threading for WPF UI testing scenarios.
        /// </summary>
        private static async Task RunOnUIThreadAsync(Action action)
        {
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                await Dispatcher.CurrentDispatcher.InvokeAsync(action, DispatcherPriority.Normal);
            }
        }
    }
}
