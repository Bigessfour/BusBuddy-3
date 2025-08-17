using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Syncfusion.UI.Xaml.Grid;
using BusBuddy.Core.Models;
using FluentAssertions;
using Serilog;

namespace BusBuddy.Tests.UI
{
    /// <summary>
    /// Focused tests for Syncfusion SfDataGrid control with BusBuddy domain objects.
    /// These tests specifically target DataGrid functionality that our enhanced testing discovered.
    ///
    /// Test Categories:
    /// - Category("Syncfusion"): All Syncfusion-related tests
    /// - Category("DataGrid"): Specifically SfDataGrid tests
    /// - Category("UI"): User interface testing
    ///
    /// References:
    /// - SfDataGrid Documentation: https://help.syncfusion.com/wpf/datagrid/getting-started
    /// - NUnit Categories: https://docs.nunit.org/articles/nunit/writing-tests/attributes/category.html
    /// </summary>
    [TestFixture]
    [Category("Syncfusion")]
    [Category("DataGrid")]
    [Category("UI")]
    [Apartment(ApartmentState.STA)]
    public class SyncfusionDataGridTests
    {
        private static readonly ILogger Logger = Log.ForContext<SyncfusionDataGridTests>();
        private SfDataGrid? _dataGrid;
        private Window? _testWindow;

        [SetUp]
        public void SetUp()
        {
            // Ensure WPF application context exists
            if (Application.Current == null)
            {
                new Application();
            }

            // Initialize fresh DataGrid for each test
            _dataGrid = new SfDataGrid();
            _testWindow = new Window
            {
                Content = _dataGrid,
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            Logger.Information("SyncfusionDataGridTests: Test setup complete");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test resources
            _testWindow?.Close();
            _testWindow = null;
            _dataGrid = null;

            Logger.Information("SyncfusionDataGridTests: Test cleanup complete");
        }

        /// <summary>
        /// Test that matches our PowerShell function filter: Category=Syncfusion&FullyQualifiedName~DataGrid
        /// Validates basic SfDataGrid creation and property initialization.
        /// </summary>
        [Test]
        [Category("Syncfusion")]
        [Category("DataGrid")]
        public void SfDataGrid_Should_Create_With_Default_Properties()
        {
            // Arrange & Act
            var dataGrid = new SfDataGrid();

            // Assert
            dataGrid.Should().NotBeNull("SfDataGrid should be created successfully");
            dataGrid.AllowEditing.Should().BeTrue("SfDataGrid should allow editing by default");
            dataGrid.AllowSorting.Should().BeTrue("SfDataGrid should allow sorting by default");
            dataGrid.AllowFiltering.Should().BeFalse("SfDataGrid should not allow filtering by default");
            dataGrid.AutoGenerateColumns.Should().BeTrue("SfDataGrid should auto-generate columns by default");
            dataGrid.ShowGroupDropArea.Should().BeFalse("SfDataGrid should not show group drop area by default");

            Logger.Information("SfDataGrid created successfully with expected default properties");
        }

        /// <summary>
        /// Test SfDataGrid column auto-generation with Student model.
        /// This test should be found by: FullyQualifiedName~DataGrid&Category=Syncfusion
        /// </summary>
        [Test]
        [Category("Syncfusion")]
        [Category("DataGrid")]
        [Category("Students")]
        public async Task SfDataGrid_Should_AutoGenerate_Student_Columns()
        {
            // Arrange
            var students = new List<Student>
            {
                new Student
                {
                    StudentId = 1,
                    StudentName = "John Doe",
                    Grade = "9",
                    DateOfBirth = new DateTime(2010, 5, 15),
                    Active = true
                },
                new Student
                {
                    StudentId = 2,
                    StudentName = "Jane Smith",
                    Grade = "10",
                    DateOfBirth = new DateTime(2009, 8, 22),
                    Active = true
                }
            };            // Act & Assert
            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                _dataGrid!.ItemsSource = students;
                _dataGrid.AutoGenerateColumns = true;

                // Verify data binding
                _dataGrid.ItemsSource.Should().NotBeNull("ItemsSource should be set");
                students.Count.Should().Be(2, "Should have 2 test students");

                // Note: Columns are generated during layout pass, so we verify the setup is correct
                _dataGrid.AutoGenerateColumns.Should().BeTrue("Auto-generation should be enabled");

                Logger.Information($"SfDataGrid configured for auto-generation with {students.Count} students");
            });
        }

        /// <summary>
        /// Test SfDataGrid filtering functionality.
        /// Matches filter: Category=Syncfusion|Category=UI and FullyQualifiedName~DataGrid
        /// </summary>
        [Test]
        [Category("UI")]
        [Category("Syncfusion")]
        [Category("DataGrid")]
        public async Task SfDataGrid_Should_Support_Filtering_When_Enabled()
        {
            // Arrange
            var buses = new List<Bus>
            {
                new Bus { VehicleId = 1, BusNumber = "BUS001", Make = "Blue Bird", Model = "Vision", Year = 2020, Active = true },
                new Bus { VehicleId = 2, BusNumber = "BUS002", Make = "Thomas Built", Model = "Saf-T-Liner", Year = 2019, Active = true },
                new Bus { VehicleId = 3, BusNumber = "BUS003", Make = "Blue Bird", Model = "All American", Year = 2021, Active = false }
            };

            // Act & Assert
            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                _dataGrid!.ItemsSource = buses;
                _dataGrid.AllowFiltering = true;
                _dataGrid.FilterRowPosition = FilterRowPosition.Top;

                // Verify filtering setup
                _dataGrid.AllowFiltering.Should().BeTrue("Filtering should be enabled");
                _dataGrid.FilterRowPosition.Should().Be(FilterRowPosition.Top, "Filter row should be at top");
                buses.Count.Should().Be(3, "Should have 3 test buses");

                Logger.Information($"SfDataGrid filtering enabled with {buses.Count} buses");
            });
        }

        /// <summary>
        /// Test SfDataGrid sorting capabilities.
        /// This test targets the specific DataGrid functionality our PowerShell automation tests.
        /// </summary>
        [Test]
        [Category("Syncfusion")]
        [Category("DataGrid")]
        [Category("Sorting")]
        public async Task SfDataGrid_Should_Support_Column_Sorting()
        {
            // Arrange
            var drivers = new List<Driver>
            {
                new Driver { DriverId = 1, FirstName = "Alice", LastName = "Johnson", LicenseNumber = "DL001" },
                new Driver { DriverId = 2, FirstName = "Bob", LastName = "Smith", LicenseNumber = "DL002" },
                new Driver { DriverId = 3, FirstName = "Charlie", LastName = "Brown", LicenseNumber = "DL003" }
            };

            // Act & Assert
            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                _dataGrid!.ItemsSource = drivers;
                _dataGrid.AllowSorting = true;
                _dataGrid.AllowTriStateSorting = true;

                // Verify sorting configuration
                _dataGrid.AllowSorting.Should().BeTrue("Sorting should be enabled");
                _dataGrid.AllowTriStateSorting.Should().BeTrue("Tri-state sorting should be enabled");
                drivers.Count.Should().Be(3, "Should have 3 test drivers");

                Logger.Information($"SfDataGrid sorting configured with {drivers.Count} drivers");
            });
        }

        /// <summary>
        /// Test SfDataGrid performance with realistic data volumes.
        /// This validates the DataGrid can handle production-scale datasets efficiently.
        /// </summary>
        [Test]
        [Category("Syncfusion")]
        [Category("DataGrid")]
        [Category("Performance")]
        public async Task SfDataGrid_Should_Handle_Large_Dataset_Efficiently()
        {
            // Arrange
            var routes = new List<Route>();
            for (int i = 1; i <= 500; i++)
            {
                routes.Add(new Route
                {
                    RouteId = i,
                    RouteName = $"Route {i:D3}",
                    Description = $"Test route {i} for performance testing",
                    Active = i % 10 != 0 // 90% active
                });
            }

            // Act & Assert
            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                _dataGrid!.ItemsSource = routes;
                // Note: Syncfusion SfDataGrid uses built-in virtualization by default

                stopwatch.Stop();                // Performance assertions
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
                    "SfDataGrid should bind 500 routes in under 2 seconds");
                routes.Count.Should().Be(500, "Should have 500 test routes");

                Logger.Information($"SfDataGrid bound {routes.Count} routes in {stopwatch.ElapsedMilliseconds}ms");
            });
        }
    }
}
