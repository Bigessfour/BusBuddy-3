using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Driver
{
    /// <summary>
    /// ViewModel for the DriversView - manages driver list display and CRUD operations
    /// Search and filtering for drivers
    /// </summary>
    public class DriversViewModel : BaseViewModel
    {
        private static readonly new ILogger Logger = Log.ForContext<DriversViewModel>();

        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly IDriverService? _driverService;
        private readonly IOperationalReportService? _reportService;
        private readonly IDriverTrainingService? _trainingService;

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
                    NotifySelectionDependentCommands();
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
                    if (ClearSearchCommand is IRelayCommand clear)
                    {
                        clear.NotifyCanExecuteChanged();
                    }
                    LogState("SearchTextChanged");
                }
            }
        }

        /// <summary>
        /// Total drivers in the loaded roster (full collection, not the search filter).
        /// </summary>
        public int TotalDrivers => Drivers.Count;

        /// <summary>
        /// Drivers with Status == Active (source: Drivers collection / DB Status column).
        /// </summary>
        public int ActiveDrivers => Drivers.Count(d => d.IsActive);

        /// <summary>
        /// Drivers where TrainingComplete is false (source: Drivers.TrainingComplete).
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
        /// Constructor for production use — resolves services from App DI when available.
        /// </summary>
        public DriversViewModel()
            : this(
                App.ServiceProvider?.GetService<IBusBuddyDbContextFactory>() ?? new BusBuddyDbContextFactory(),
                App.ServiceProvider?.GetService<IDriverService>(),
                App.ServiceProvider?.GetService<IOperationalReportService>(),
                App.ServiceProvider?.GetService<IDriverTrainingService>())
        {
        }

        /// <summary>
        /// Constructor for testing / DI
        /// </summary>
        public DriversViewModel(
            IBusBuddyDbContextFactory contextFactory,
            IDriverService? driverService = null,
            IOperationalReportService? reportService = null,
            IDriverTrainingService? trainingService = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _driverService = driverService;
            _reportService = reportService;
            _trainingService = trainingService;

            LoadDriversCommand = new AsyncRelayCommand(LoadDriversAsync);
            AddDriverCommand = new RelayCommand(ExecuteAddDriver);
            EditDriverCommand = new RelayCommand(ExecuteEditDriver, () => HasSelectedDriver);
            // Soft-retire first (specs/drivers.md); hard delete only when service allows and user confirms.
            DeleteDriverCommand = new AsyncRelayCommand(ExecuteDeleteDriverAsync, () => HasSelectedDriver);
            RefreshCommand = new AsyncRelayCommand(LoadDriversAsync);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch, () => !string.IsNullOrEmpty(SearchText));
            GenerateReportsCommand = new AsyncRelayCommand(ExecuteGenerateReportsAsync);
            LicenseCheckCommand = new AsyncRelayCommand(ExecuteLicenseCheckAsync);
            TrainingRecordsCommand = new AsyncRelayCommand(ExecuteTrainingRecordsAsync, () => HasSelectedDriver);
            AssignRouteCommand = new AsyncRelayCommand(ExecuteAssignRouteAsync, () => HasSelectedDriver);
            EditDetailsCommand = new RelayCommand(ExecuteEditDetails, () => HasSelectedDriver);
            ViewLicenseCommand = new RelayCommand(ExecuteViewLicense, () => HasSelectedDriver);
            TrainingHistoryCommand = new AsyncRelayCommand(ExecuteTrainingHistoryAsync, () => HasSelectedDriver);

            _ = LoadDriversAsync();
        }

        private void NotifySelectionDependentCommands()
        {
            if (EditDriverCommand is IRelayCommand edit)
            {
                edit.NotifyCanExecuteChanged();
            }

            if (DeleteDriverCommand is IRelayCommand del)
            {
                del.NotifyCanExecuteChanged();
            }

            if (AssignRouteCommand is IRelayCommand assign)
            {
                assign.NotifyCanExecuteChanged();
            }

            if (EditDetailsCommand is IRelayCommand editDetails)
            {
                editDetails.NotifyCanExecuteChanged();
            }

            if (ViewLicenseCommand is IRelayCommand viewLicense)
            {
                viewLicense.NotifyCanExecuteChanged();
            }

            if (TrainingRecordsCommand is IRelayCommand training)
            {
                training.NotifyCanExecuteChanged();
            }

            if (TrainingHistoryCommand is IRelayCommand history)
            {
                history.NotifyCanExecuteChanged();
            }
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Load all drivers from the database (prefer IDriverService; fall back to DbContext).
        /// </summary>
        public async Task LoadDriversAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading drivers from database");

                List<Core.Models.Driver> drivers;
                if (_driverService is not null)
                {
                    drivers = await _driverService.GetAllDriversAsync().ConfigureAwait(true);
                }
                else
                {
                    using var context = _contextFactory.CreateDbContext();
                    drivers = await context.Drivers
                        .OrderBy(d => d.DriverName)
                        .ToListAsync()
                        .ConfigureAwait(true);
                }

                Drivers.Clear();
                foreach (var driver in drivers.OrderBy(d => d.DriverName))
                {
                    Drivers.Add(driver);
                }

                Logger.Information(
                    "Loaded {DriverCount} drivers (Total={Total} Active={Active} TrainingPending={Pending})",
                    Drivers.Count,
                    TotalDrivers,
                    ActiveDrivers,
                    TrainingPendingDrivers);
                base.StatusMessage = $"Loaded {Drivers.Count} drivers";

                LastUpdated = DateTime.Now;
                RefreshRosterStats();
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
                if (driverForm.DataContext is BusBuddy.WPF.ViewModels.Driver.DriverFormViewModel vm)
                {
                    vm.PrepareNewDriver();
                }
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
                if (SelectedDriver is null)
                {
                    base.StatusMessage = "Select a driver before deleting";
                    UserToast.Warning("Select a driver in the grid first, then click Delete Driver.", "No driver selected");
                    return;
                }

                var driver = SelectedDriver;
                var result = System.Windows.MessageBox.Show(
                    $"Retire driver '{driver.DriverName}'?\n\n" +
                    "This marks them Inactive (soft-retire), clears route FKs where needed, " +
                    "and removes them from this list. The record is kept for history.",
                    "Confirm Retire Driver",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    UserToast.Info("Retire cancelled — no changes made.", "Cancelled");
                    return;
                }

                await DeactivateOrDeleteDriverAsync(driver).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing delete driver command");
                base.StatusMessage = $"Error deleting driver: {ex.Message}";
                UserToast.Error($"Delete failed: {ex.Message}", "Delete Driver");
            }
        }

        private void ExecuteClearSearch()
        {
            SearchText = string.Empty;
            ApplyFilters();
            base.StatusMessage = "Search cleared";
        }

        private async Task ExecuteGenerateReportsAsync()
        {
            await GenerateDriverReportAsync(OperationalReportKind.DriverRoster, "Driver roster");
        }

        private async Task ExecuteLicenseCheckAsync()
        {
            SelectedStatusFilter = "License Expiring";
            var needing = _driverService != null
                ? await _driverService.GetDriversNeedingRenewalAsync()
                : Drivers.Where(d =>
                    d.LicenseExpiryDate.HasValue &&
                    d.LicenseExpiryDate.Value.Date <= DateTime.Today.AddDays(30)).ToList();

            Logger.Information("License check: {Count} drivers needing renewal within 30 days", needing.Count);
            base.StatusMessage = needing.Count == 0
                ? "No licenses expiring within 30 days"
                : $"{needing.Count} license(s) due within 30 days — generating report";

            await GenerateDriverReportAsync(OperationalReportKind.LicenseExpiration, "License expiration");
        }

        private async Task ExecuteTrainingRecordsAsync()
        {
            if (SelectedDriver is null)
            {
                base.StatusMessage = "Select a driver to view training";
                return;
            }

            Logger.Information("View Training for DriverId={DriverId}", SelectedDriver.DriverId);
            OpenTrainingChecklist(SelectedDriver);
            await Task.CompletedTask;
        }

        private async Task ExecuteAssignRouteAsync()
        {
            if (SelectedDriver is null)
            {
                return;
            }

            try
            {
                if (_driverService is null)
                {
                    base.StatusMessage = "Driver service unavailable — cannot load route assignments";
                    Logger.Warning("AssignRoute skipped — IDriverService not registered");
                    return;
                }

                var routes = await _driverService.GetDriverRoutesAsync(SelectedDriver.DriverId);
                var names = routes
                    .Select(r => string.IsNullOrWhiteSpace(r.RouteName) ? $"Route {r.RouteId}" : r.RouteName)
                    .Take(5)
                    .ToList();

                Logger.Information(
                    "Driver {DriverId} has {RouteCount} assigned route(s)",
                    SelectedDriver.DriverId,
                    routes.Count);

                base.StatusMessage = routes.Count == 0
                    ? $"{SelectedDriver.DriverName}: no routes assigned (use Route Assignment to assign)"
                    : $"{SelectedDriver.DriverName}: {routes.Count} route(s) — {string.Join(", ", names)}"
                      + (routes.Count > 5 ? "…" : string.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading routes for driver {DriverId}", SelectedDriver.DriverId);
                base.StatusMessage = $"Error loading routes: {ex.Message}";
            }
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

            var d = SelectedDriver;
            var expiry = d.LicenseExpiryDate?.ToString("yyyy-MM-dd") ?? "not set";
            var days = d.LicenseExpiryDate.HasValue
                ? (d.LicenseExpiryDate.Value.Date - DateTime.Today).Days.ToString()
                : "n/a";

            Logger.Information(
                "View license DriverId={DriverId} Number={LicenseNumber} Expiry={Expiry}",
                d.DriverId,
                d.LicenseNumber,
                expiry);

            base.StatusMessage =
                $"{d.DriverName}: license {d.LicenseNumber ?? "(none)"} class {d.LicenseClass ?? "?"} " +
                $"status {d.LicenseStatus ?? "?"} expires {expiry} ({days} days)";
        }

        private async Task ExecuteTrainingHistoryAsync()
        {
            if (SelectedDriver is null)
            {
                return;
            }

            OpenTrainingChecklist(SelectedDriver);
            await Task.CompletedTask;
        }

        private void OpenTrainingChecklist(Core.Models.Driver driver)
        {
            try
            {
                var training = _trainingService
                    ?? App.ServiceProvider?.GetService<IDriverTrainingService>();
                if (training is null)
                {
                    base.StatusMessage = "Training service unavailable";
                    return;
                }

                var vm = new DriverTrainingChecklistViewModel(
                    driver.DriverId,
                    driver.DriverName ?? $"Driver {driver.DriverId}",
                    training);
                var dialog = new BusBuddy.WPF.Views.Driver.DriverTrainingChecklistView(vm);
                try
                {
                    var owner = System.Windows.Application.Current?.Windows
                        .OfType<System.Windows.Window>()
                        .FirstOrDefault(w => w.IsActive)
                        ?? System.Windows.Application.Current?.MainWindow;
                    if (owner != null)
                    {
                        dialog.Owner = owner;
                    }
                }
                catch { /* owner optional */ }

                dialog.ShowDialog();
                _ = LoadDriversAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed opening training checklist DriverId={DriverId}", driver.DriverId);
                base.StatusMessage = $"Training checklist error: {ex.Message}";
            }
        }

        private async Task GenerateDriverReportAsync(OperationalReportKind kind, string label)
        {
            if (_reportService is null)
            {
                base.StatusMessage = $"Report service unavailable — cannot generate {label}";
                Logger.Warning("Driver report {Kind} skipped — IOperationalReportService not registered", kind);
                return;
            }

            try
            {
                IsLoading = true;
                base.StatusMessage = $"Generating {label} report...";
                var result = await _reportService.GenerateAsync(kind);
                base.StatusMessage = result.Status;
                TryOpenReportFile(result.FilePath);
                Logger.Information("Driver report {Kind} written to {Path}", kind, result.FilePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Driver report {Kind} failed", kind);
                base.StatusMessage = $"Error generating {label}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static void TryOpenReportFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.ForContext<DriversViewModel>().Warning(ex, "Could not open report file {Path}", path);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Apply search and status filters to the drivers collection and populate FilteredDrivers
        /// </summary>
        private void ApplyFilters()
        {
            var query = Drivers.AsEnumerable();

            // Status / attention filter
            if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && !SelectedStatusFilter.Equals("All Status", StringComparison.OrdinalIgnoreCase))
            {
                if (SelectedStatusFilter.Equals("License Expiring", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d =>
                        d.LicenseExpiryDate.HasValue &&
                        d.LicenseExpiryDate.Value.Date <= DateTime.Today.AddDays(30));
                }
                else if (SelectedStatusFilter.Equals("Training", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => !d.TrainingComplete);
                }
                else
                {
                    query = query.Where(d => string.Equals(d.Status ?? string.Empty, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
                }
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

            RefreshRosterStats();
            LogState("ApplyFilters:After");
        }

        /// <summary>
        /// Soft-retire via IDriverService (Status = Inactive) and drop from the roster UI.
        /// Never hard-deletes — route/history FKs must stay valid (UTM FK failures 2026-09-11).
        /// </summary>
        private async Task DeactivateOrDeleteDriverAsync(Core.Models.Driver driver)
        {
            Logger.Information("Soft-retiring driver {DriverId} - {DriverName}", driver.DriverId, driver.DriverName);

            if (_driverService is not null)
            {
                try
                {
                    var deactivated = await _driverService.DeleteDriverAsync(driver.DriverId).ConfigureAwait(true);
                    if (deactivated.IsSuccess)
                    {
                        RemoveDriverFromRoster(driver);
                        var sentence = string.IsNullOrWhiteSpace(deactivated.Error)
                            ? $"Driver '{driver.DriverName}' retired."
                            : deactivated.Error;
                        base.StatusMessage = sentence;
                        Logger.Information("Soft-retired and hid driver {DriverId}", driver.DriverId);
                        if (UserToast.IsRetiredSuccess(deactivated))
                        {
                            UserToast.Warning(sentence, "Driver retired");
                        }
                        else
                        {
                            UserToast.Success(sentence, "Driver retired");
                        }

                        return;
                    }

                    base.StatusMessage = deactivated.Error;
                    UserToast.Error(deactivated.Error, "Retire failed");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Cannot retire driver {DriverId}", driver.DriverId);
                    base.StatusMessage = ex.Message;
                    UserToast.Error(ex.Message, "Retire failed");
                    return;
                }
            }

            // No service — soft-retire via DbContext with FindAsync
            using var context = _contextFactory.CreateDbContext();
            var tracked = await context.Drivers.FindAsync(driver.DriverId).ConfigureAwait(true);
            if (tracked is null)
            {
                base.StatusMessage = "Driver not found in database";
                UserToast.Error("Driver not found in the database.", "Retire failed");
                return;
            }

            tracked.Status = "Inactive";
            await context.SaveChangesAsync().ConfigureAwait(true);
            RemoveDriverFromRoster(driver);
            var retired = ClerkWriteMessages.Retired($"Driver '{driver.DriverName}'");
            base.StatusMessage = retired;
            Logger.Information("Retired driver {DriverId} via DbContext", driver.DriverId);
            UserToast.Warning(retired, "Driver retired");
        }

        private void RemoveDriverFromRoster(Core.Models.Driver driver)
        {
            for (var i = Drivers.Count - 1; i >= 0; i--)
            {
                if (Drivers[i].DriverId == driver.DriverId)
                {
                    Drivers.RemoveAt(i);
                }
            }

            for (var i = FilteredDrivers.Count - 1; i >= 0; i--)
            {
                if (FilteredDrivers[i].DriverId == driver.DriverId)
                {
                    FilteredDrivers.RemoveAt(i);
                }
            }

            SelectedDriver = null;
            RefreshRosterStats();
            // Rebuild filtered view without clobbering the caller's StatusMessage.
            var savedStatus = base.StatusMessage;
            ApplyFilters();
            if (!string.IsNullOrWhiteSpace(savedStatus))
            {
                base.StatusMessage = savedStatus;
            }
        }

        private void RefreshRosterStats()
        {
            OnPropertyChanged(nameof(TotalDrivers));
            OnPropertyChanged(nameof(ActiveDrivers));
            OnPropertyChanged(nameof(TrainingPendingDrivers));
            OnPropertyChanged(nameof(ExpiringLicensesCount));
            UpdateDriverStatusData();
            Logger.Debug(
                "Roster stats — Total={Total} Active={Active} TrainingPending={Pending}",
                TotalDrivers,
                ActiveDrivers,
                TrainingPendingDrivers);
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
