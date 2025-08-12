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
    private string _selectedStatusFilter = "All Status";
    private DateTime _lastUpdated = DateTime.Now;
        private readonly ObservableCollection<StatusCount> _driverStatusData = new();

        #region Properties

    /// <summary>
    /// Collection of all drivers loaded from the database
    /// </summary>
    public ObservableCollection<Core.Models.Driver> Drivers { get; } = new();

    /// <summary>
    /// Filtered view of drivers for binding to the UI grid
    /// </summary>
    public ObservableCollection<Core.Models.Driver> FilteredDrivers { get; } = new();

    /// <summary>
    /// Chart source for driver status distribution (Status/Count)
    /// </summary>
    public ObservableCollection<StatusCount> DriverStatusData => _driverStatusData;

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
                    Logger.Debug("SelectedDriver changed -> Id={DriverId} Name={DriverName}", value?.DriverId, value?.DriverName);
                    OnPropertyChanged(nameof(HasSelectedDriver));
                    // Update command availability using MVVM Toolkit interfaces to support both RelayCommand and AsyncRelayCommand
                    if (EditDriverCommand is CommunityToolkit.Mvvm.Input.IRelayCommand edit)
                    {
                        edit.NotifyCanExecuteChanged();
                    }
                    if (DeleteDriverCommand is CommunityToolkit.Mvvm.Input.IRelayCommand del)
                    {
                        del.NotifyCanExecuteChanged();
                    }
                    if (AssignRouteCommand is CommunityToolkit.Mvvm.Input.IRelayCommand assign)
                    {
                        assign.NotifyCanExecuteChanged();
                    }
                    if (EditDetailsCommand is CommunityToolkit.Mvvm.Input.IRelayCommand editDetails)
                    {
                        editDetails.NotifyCanExecuteChanged();
                    }
                    LogState("SelectionChanged");
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
                    Logger.Debug("SearchText updated -> '{SearchText}'", _searchText);
                    ApplyFilters();
                    if (ClearSearchCommand is CommunityToolkit.Mvvm.Input.IRelayCommand clear)
                    {
                        clear.NotifyCanExecuteChanged();
                    }
                    LogState("SearchTextChanged");
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

    /// <summary>
    /// Number of drivers with training pending (not complete)
    /// </summary>
    public int TrainingPendingDrivers => Drivers.Count(d => !d.TrainingComplete);

    /// <summary>
    /// Number of drivers with licenses expiring within 30 days
    /// </summary>
    public int ExpiringLicensesCount => Drivers.Count(d => d.LicenseExpiryDate.HasValue && d.LicenseExpiryDate.Value.Date > DateTime.Today && d.LicenseExpiryDate.Value.Date <= DateTime.Today.AddDays(30));

        /// <summary>
        /// Selected status filter from the UI (e.g., All Status, Active, Inactive, Training, License Expiring)
        /// </summary>
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (SetProperty(ref _selectedStatusFilter, value))
                {
                    Logger.Debug("StatusFilter changed -> {StatusFilter}", _selectedStatusFilter);
                    ApplyFilters();
                    LogState("StatusFilterChanged");
                }
            }
        }

        /// <summary>
        /// Last time the driver list was refreshed
        /// </summary>
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            private set => SetProperty(ref _lastUpdated, value);
        }

        #endregion

        #region Commands

        public ICommand LoadDriversCommand { get; }
        public ICommand AddDriverCommand { get; }
        public ICommand EditDriverCommand { get; }
        public ICommand DeleteDriverCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand GenerateReportsCommand { get; }
        public ICommand LicenseCheckCommand { get; }
        public ICommand TrainingRecordsCommand { get; }
        public ICommand AssignRouteCommand { get; }
        public ICommand EditDetailsCommand { get; }
        public ICommand ViewLicenseCommand { get; }
        public ICommand TrainingHistoryCommand { get; }

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
            GenerateReportsCommand = new RelayCommand(ExecuteGenerateReports);
            LicenseCheckCommand = new RelayCommand(ExecuteLicenseCheck);
            TrainingRecordsCommand = new RelayCommand(ExecuteTrainingRecords);
            AssignRouteCommand = new RelayCommand(ExecuteAssignRoute, () => HasSelectedDriver);
            EditDetailsCommand = new RelayCommand(ExecuteEditDetails, () => HasSelectedDriver);
            ViewLicenseCommand = new RelayCommand(ExecuteViewLicense, () => HasSelectedDriver);
            TrainingHistoryCommand = new RelayCommand(ExecuteTrainingHistory, () => HasSelectedDriver);

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
            GenerateReportsCommand = new RelayCommand(ExecuteGenerateReports);
            LicenseCheckCommand = new RelayCommand(ExecuteLicenseCheck);
            TrainingRecordsCommand = new RelayCommand(ExecuteTrainingRecords);
            AssignRouteCommand = new RelayCommand(ExecuteAssignRoute, () => HasSelectedDriver);
            EditDetailsCommand = new RelayCommand(ExecuteEditDetails, () => HasSelectedDriver);
            ViewLicenseCommand = new RelayCommand(ExecuteViewLicense, () => HasSelectedDriver);
            TrainingHistoryCommand = new RelayCommand(ExecuteTrainingHistory, () => HasSelectedDriver);
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

                LastUpdated = DateTime.Now;

                // Update property notifications
                OnPropertyChanged(nameof(TotalDrivers));
                OnPropertyChanged(nameof(ActiveDrivers));
                OnPropertyChanged(nameof(TrainingPendingDrivers));
                OnPropertyChanged(nameof(ExpiringLicensesCount));
                UpdateDriverStatusData();

                ApplyFilters();
                LogState("LoadDriversAsync:AfterLoad");
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
                driverForm.Owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
                var result = driverForm.ShowDialog();

                if (result == true)
                {
                    // Refresh the driver list after successful add
                    _ = LoadDriversAsync();
                    base.StatusMessage = "Driver added successfully";
                    LogState("AddDriver:DialogResultTrue");
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
                    driverForm.Owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
                    // Pass driver data to form for editing via ViewModel
                    if (driverForm.DataContext is BusBuddy.WPF.ViewModels.Driver.DriverFormViewModel vm)
                    {
                        vm.SelectedDriver = SelectedDriver; // VM will load into editable Driver
                    }
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
            ApplyFilters();
            base.StatusMessage = "Search cleared";
        }

        private void ExecuteGenerateReports()
        {
            Logger.Information("Generate reports command executed");
            base.StatusMessage = "Generating driver reports (MVP placeholder)";
        }

        private void ExecuteLicenseCheck()
        {
            Logger.Information("License check command executed");
            base.StatusMessage = "Checking license expirations (MVP placeholder)";
        }

        private void ExecuteTrainingRecords()
        {
            Logger.Information("Training records command executed");
            base.StatusMessage = "Opening training records (MVP placeholder)";
        }

        private void ExecuteAssignRoute()
        {
            if (SelectedDriver is null)
            {
                return;
            }

            Logger.Information("Assign route command executed for driver {DriverId}", SelectedDriver.DriverId);
            base.StatusMessage = $"Assign route to {SelectedDriver.DriverName} (MVP placeholder)";
        }

        private void ExecuteEditDetails()
        {
            if (SelectedDriver is null)
            {
                return;
            }

            Logger.Information("Edit details command executed for driver {DriverId}", SelectedDriver.DriverId);
            ExecuteEditDriver();
        }

        private void ExecuteViewLicense()
        {
            if (SelectedDriver is null)
            {
                return;
            }

            Logger.Information("View license command executed for driver {DriverId}", SelectedDriver.DriverId);
            base.StatusMessage = $"Viewing license for {SelectedDriver.DriverName} (MVP placeholder)";
        }

        private void ExecuteTrainingHistory()
        {
            if (SelectedDriver is null)
            {
                return;
            }

            Logger.Information("Training history command executed for driver {DriverId}", SelectedDriver.DriverId);
            base.StatusMessage = $"Viewing training history for {SelectedDriver.DriverName} (MVP placeholder)";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Apply search and status filters to the drivers collection and populate FilteredDrivers
        /// </summary>
        private void ApplyFilters()
        {
            var query = Drivers.AsEnumerable();

            // Status filter
            if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && !SelectedStatusFilter.Equals("All Status", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(d => string.Equals(d.Status ?? string.Empty, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim();
                query = query.Where(d =>
                    (d.DriverName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                    (d.DriverPhone?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                    (d.DriverEmail?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                    (d.LicenseNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) == true));
            }

            // Update filtered collection efficiently
            var results = query.ToList();
            FilteredDrivers.Clear();
            foreach (var driver in results)
            {
                FilteredDrivers.Add(driver);
            }
            Logger.Debug("Filters applied -> Search='{Search}' Status='{Status}' ResultCount={Count}", SearchText, SelectedStatusFilter, FilteredDrivers.Count);

            base.StatusMessage = !string.IsNullOrWhiteSpace(SearchText)
                ? $"Found {FilteredDrivers.Count} drivers matching '{SearchText}'"
                : $"Showing {FilteredDrivers.Count} drivers";

            // Update computed stats if needed
            OnPropertyChanged(nameof(TotalDrivers));
            OnPropertyChanged(nameof(ActiveDrivers));
            OnPropertyChanged(nameof(TrainingPendingDrivers));
            OnPropertyChanged(nameof(ExpiringLicensesCount));
            UpdateDriverStatusData();
            LogState("ApplyFilters:After");
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
                OnPropertyChanged(nameof(TrainingPendingDrivers));
                OnPropertyChanged(nameof(ExpiringLicensesCount));
                UpdateDriverStatusData();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting driver {DriverId}", driver.DriverId);
                base.StatusMessage = $"Error deleting driver: {ex.Message}";
            }
        }

        private void UpdateDriverStatusData()
        {
            // Rebuild status counts: group by Status across full Drivers collection
            var groups = Drivers
                .GroupBy(d => string.IsNullOrWhiteSpace(d.Status) ? "Unknown" : d.Status)
                .Select(g => new StatusCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList();

            _driverStatusData.Clear();
            foreach (var item in groups)
            {
                _driverStatusData.Add(item);
            }
            OnPropertyChanged(nameof(DriverStatusData));
            Logger.Debug("StatusData rebuilt -> {Items}", string.Join(", ", _driverStatusData.Select(s => $"{s.Status}:{s.Count}")));
        }

        #region Debug Helpers
        private void LogState(string context)
        {
            try
            {
                Logger.Debug("State[{Context}] Total={Total} Filtered={Filtered} SelectedId={SelectedId} Search='{Search}' Status='{Status}'", context, Drivers.Count, FilteredDrivers.Count, SelectedDriver?.DriverId, SearchText, SelectedStatusFilter);
            }
            catch { /* swallow logging issues */ }
        }
        #endregion

        public record StatusCount(string Status, int Count);

        #endregion
    }
}
