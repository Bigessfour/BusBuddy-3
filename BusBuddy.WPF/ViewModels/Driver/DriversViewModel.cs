using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Data;
using BusBuddy.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Driver
{
    /// <summary>
    /// ViewModel for the DriversView - manages driver list display and CRUD operations
    /// Implements MVP pattern with enhanced search and filtering capabilities
    /// </summary>
    public class DriversViewModel : BaseViewModel
    {
        private static readonly new ILogger Logger = Log.ForContext<DriversViewModel>();

        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IDriverService? _driverService;

        private Core.Models.Driver? _selectedDriver;
        private string _searchText = string.Empty;

        #region Properties

        /// <summary>
        /// Collection of all drivers for display in the data grid
        /// </summary>
        public ObservableCollection<Core.Models.Driver> Drivers { get; } = new();

        /// <summary>
        /// Currently selected driver in the data grid
        /// </summary>
        public Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (SetProperty(ref _selectedDriver, value))
                {
                    OnPropertyChanged(nameof(HasSelectedDriver));
                    // Update command availability
                    ((RelayCommand)EditDriverCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)DeleteDriverCommand).NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Whether a driver is currently selected
        /// </summary>
        public bool HasSelectedDriver => SelectedDriver != null;

        /// <summary>
        /// Search text for filtering drivers
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplySearchFilter();
                }
            }
        }

        /// <summary>
        /// Total number of drivers
        /// </summary>
        public int TotalDrivers => Drivers.Count;

        /// <summary>
        /// Number of active drivers
        /// </summary>
        public int ActiveDrivers => Drivers.Count(d => d.Status == "Active");

        #endregion

        #region Commands

        public ICommand LoadDriversCommand { get; }
        public ICommand AddDriverCommand { get; }
        public ICommand EditDriverCommand { get; }
        public ICommand DeleteDriverCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor for production use
        /// </summary>
        public DriversViewModel()
        {
            _contextFactory = new BusBuddyDbContextFactory();

            // Initialize commands
            LoadDriversCommand = new AsyncRelayCommand(LoadDriversAsync);
            AddDriverCommand = new RelayCommand(ExecuteAddDriver);
            EditDriverCommand = new RelayCommand(ExecuteEditDriver, () => HasSelectedDriver);
            DeleteDriverCommand = new AsyncRelayCommand(ExecuteDeleteDriverAsync, () => HasSelectedDriver);
            RefreshCommand = new AsyncRelayCommand(LoadDriversAsync);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch, () => !string.IsNullOrEmpty(SearchText));

            // Load initial data
            _ = LoadDriversAsync();
        }

        /// <summary>
        /// Constructor for testing (dependency injection)
        /// </summary>
        public DriversViewModel(IBusBuddyDbContextFactory contextFactory, IDriverService? driverService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _driverService = driverService;

            // Initialize commands (same as above)
            LoadDriversCommand = new AsyncRelayCommand(LoadDriversAsync);
            AddDriverCommand = new RelayCommand(ExecuteAddDriver);
            EditDriverCommand = new RelayCommand(ExecuteEditDriver, () => HasSelectedDriver);
            DeleteDriverCommand = new AsyncRelayCommand(ExecuteDeleteDriverAsync, () => HasSelectedDriver);
            RefreshCommand = new AsyncRelayCommand(LoadDriversAsync);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch, () => !string.IsNullOrEmpty(SearchText));
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Load all drivers from the database
        /// </summary>
        public async Task LoadDriversAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading drivers from database");

                using var context = _contextFactory.CreateDbContext();
                var drivers = await context.Drivers
                    .OrderBy(d => d.DriverName)
                    .ToListAsync();

                Drivers.Clear();
                foreach (var driver in drivers)
                {
                    Drivers.Add(driver);
                }

                Logger.Information("Loaded {DriverCount} drivers", Drivers.Count);
                base.StatusMessage = $"Loaded {Drivers.Count} drivers";

                // Update property notifications
                OnPropertyChanged(nameof(TotalDrivers));
                OnPropertyChanged(nameof(ActiveDrivers));

                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading drivers");
                base.StatusMessage = $"Error loading drivers: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Command Handlers

        private void ExecuteAddDriver()
        {
            try
            {
                Logger.Information("Add driver command executed");

                var driverForm = new BusBuddy.WPF.Views.Driver.DriverForm();
                var result = driverForm.ShowDialog();

                if (result == true)
                {
                    // Refresh the driver list after successful add
                    _ = LoadDriversAsync();
                    base.StatusMessage = "Driver added successfully";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing add driver command");
                base.StatusMessage = $"Error adding driver: {ex.Message}";
            }
        }

        private void ExecuteEditDriver()
        {
            try
            {
                if (SelectedDriver != null)
                {
                    Logger.Information("Edit driver command executed for driver {DriverId}", SelectedDriver.DriverId);

                    var driverForm = new BusBuddy.WPF.Views.Driver.DriverForm();
                    // TODO: Pass driver data to form for editing
                    var result = driverForm.ShowDialog();

                    if (result == true)
                    {
                        // Refresh the driver list after successful edit
                        _ = LoadDriversAsync();
                        base.StatusMessage = "Driver updated successfully";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing edit driver command");
                base.StatusMessage = $"Error editing driver: {ex.Message}";
            }
        }

        private async Task ExecuteDeleteDriverAsync()
        {
            try
            {
                if (SelectedDriver != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Are you sure you want to delete driver '{SelectedDriver.DriverName}'?",
                        "Confirm Delete",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await DeleteDriverAsync(SelectedDriver);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing delete driver command");
                base.StatusMessage = $"Error deleting driver: {ex.Message}";
            }
        }

        private void ExecuteClearSearch()
        {
            SearchText = string.Empty;
            base.StatusMessage = "Search cleared";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Apply search filter to the drivers collection
        /// </summary>
        private void ApplySearchFilter()
        {
            // In a more sophisticated implementation, you might use a CollectionView
            // For now, we'll just update the status message
            if (!string.IsNullOrEmpty(SearchText))
            {
                var filteredCount = Drivers.Count(d =>
                    d.DriverName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (d.DriverPhone?.Contains(SearchText) == true) ||
                    (d.DriverEmail?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true));

                base.StatusMessage = $"Found {filteredCount} drivers matching '{SearchText}'";
            }
            else
            {
                base.StatusMessage = $"Showing {Drivers.Count} drivers";
            }
        }

        /// <summary>
        /// Delete a driver from the database
        /// </summary>
        private async Task DeleteDriverAsync(Core.Models.Driver driver)
        {
            try
            {
                Logger.Information("Deleting driver {DriverId} - {DriverName}", driver.DriverId, driver.DriverName);

                using var context = _contextFactory.CreateDbContext();
                context.Drivers.Remove(driver);
                await context.SaveChangesAsync();

                Drivers.Remove(driver);
                SelectedDriver = null;

                Logger.Information("Successfully deleted driver {DriverId}", driver.DriverId);
                base.StatusMessage = "Driver deleted successfully";

                // Update property notifications
                OnPropertyChanged(nameof(TotalDrivers));
                OnPropertyChanged(nameof(ActiveDrivers));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting driver {DriverId}", driver.DriverId);
                base.StatusMessage = $"Error deleting driver: {ex.Message}";
            }
        }

        #endregion
    }
}
