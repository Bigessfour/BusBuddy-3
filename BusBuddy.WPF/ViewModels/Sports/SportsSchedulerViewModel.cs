using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using ActivityModel = BusBuddy.Core.Models.Activity;
using BusModel = BusBuddy.Core.Models.Bus;
using DriverModel = BusBuddy.Core.Models.Driver;
using DestinationModel = BusBuddy.Core.Models.Destination;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using Serilog;
using Serilog.Context;
using CommunityToolkit.Mvvm.Input;
using System;
using BusBuddy.WPF.ViewModels;
using static BusBuddy.Core.Models.DestinationTypes;

namespace BusBuddy.WPF.ViewModels.Sports
{
    /// <summary>
    /// Phase 2 Sports Scheduler ViewModel
    /// Manages sports activities with vehicle and driver dependencies
    /// Features: Real-time scheduling, conflict detection, resource optimization
    /// </summary>
    [Obsolete("Use UnifiedSchedulerViewModel (Sports + Activities)")]
    public class SportsSchedulerViewModel : BaseViewModel, IDisposable
    {
        private static new readonly ILogger Logger = Log.ForContext<SportsSchedulerViewModel>();
        private readonly BusBuddyDbContext _context;

        #region Observable Collections

        /// <summary>
        /// All sports activities/events
        /// </summary>
        public ObservableCollection<ActivityModel> SportsActivities { get; set; } = new();


        /// <summary>
        /// Available vehicles for assignment
        /// </summary>
        public ObservableCollection<BusModel> AvailableBuses { get; set; } = new();

        /// <summary>
        /// Available drivers for assignment
        /// </summary>
        public ObservableCollection<DriverModel> AvailableDrivers { get; set; } = new();

        /// <summary>
        /// Sports destinations (venues, stadiums, etc.)
        /// </summary>
        public ObservableCollection<DestinationModel> SportsDestinations { get; set; } = new();

        /// <summary>
        /// Filtered activities based on current criteria
        /// </summary>
        public ObservableCollection<ActivityModel> FilteredActivities { get; set; } = new();

        #endregion

        #region Selected Items

        private ActivityModel? _selectedActivity;
        public ActivityModel? SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                if (SetProperty(ref _selectedActivity, value))
                {
                    OnSelectedActivityChanged();
                }
            }
        }

        private BusModel? _selectedBus;
        public BusModel? SelectedBus
        {
            get => _selectedBus;
            set => SetProperty(ref _selectedBus, value);
        }

        private DriverModel? _selectedDriver;
        public DriverModel? SelectedDriver
        {
            get => _selectedDriver;
            set => SetProperty(ref _selectedDriver, value);
        }

        private DestinationModel? _selectedDestination;
        public DestinationModel? SelectedDestination
        {
            get => _selectedDestination;
            set => SetProperty(ref _selectedDestination, value);
        }

        #endregion

        #region Filter Properties

        private DateTime _filterStartDate = DateTime.Today;
        public DateTime FilterStartDate
        {
            get => _filterStartDate;
            set
            {
                if (SetProperty(ref _filterStartDate, value))
                {
                    _ = ApplyFiltersAsync();
                }
            }
        }

        private DateTime _filterEndDate = DateTime.Today.AddDays(30);
        public DateTime FilterEndDate
        {
            get => _filterEndDate;
            set
            {
                if (SetProperty(ref _filterEndDate, value))
                {
                    _ = ApplyFiltersAsync();
                }
            }
        }

        private string _filterSport = "All";
        public string FilterSport
        {
            get => _filterSport;
            set
            {
                if (SetProperty(ref _filterSport, value))
                {
                    _ = ApplyFiltersAsync();
                }
            }
        }

        #endregion

        #region Statistics

        private int _totalSportsEvents;
        public int TotalSportsEvents
        {
            get => _totalSportsEvents;
            set => SetProperty(ref _totalSportsEvents, value);
        }

        private int _awayGames;
        public int AwayGames
        {
            get => _awayGames;
            set => SetProperty(ref _awayGames, value);
        }

        private int _homeGames;
        public int HomeGames
        {
            get => _homeGames;
            set => SetProperty(ref _homeGames, value);
        }

        private int _unassignedActivities;
        public int UnassignedActivities
        {
            get => _unassignedActivities;
            set => SetProperty(ref _unassignedActivities, value);
        }

        #endregion

        #region Commands

        public ICommand LoadDataCommand { get; }
        public ICommand AddSportsEventCommand { get; }
        public ICommand EditActivityCommand { get; }
        public ICommand DeleteActivityCommand { get; }
        public ICommand AssignVehicleCommand { get; }
        public ICommand AssignDriverCommand { get; }
        public ICommand CheckConflictsCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Constructor

        public SportsSchedulerViewModel()
        {
            _context = new BusBuddyDbContext();

            // Initialize commands
            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            AddSportsEventCommand = new AsyncRelayCommand(AddSportsEventAsync);
            EditActivityCommand = new AsyncRelayCommand(EditActivityAsync, () => SelectedActivity != null);
            DeleteActivityCommand = new AsyncRelayCommand(DeleteActivityAsync, () => SelectedActivity != null);
            AssignVehicleCommand = new AsyncRelayCommand(AssignVehicleAsync, () => SelectedActivity != null && SelectedBus != null);
            AssignDriverCommand = new AsyncRelayCommand(AssignDriverAsync, () => SelectedActivity != null && SelectedDriver != null);
            CheckConflictsCommand = new AsyncRelayCommand(CheckConflictsAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);

            Logger.Information("SportsSchedulerViewModel initialized");
        }

        #endregion

        #region Data Loading Methods

        /// <summary>
        /// Load all sports scheduler data
        /// </summary>
        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                using (LogContext.PushProperty("Operation", "LoadSportsSchedulerData"))
                {
                    Logger.Information("Loading sports scheduler data");

                    // Load sports activities (only sports-related)
                    var sportsActivities = await _context.Activities
                        .Include(a => a.AssignedVehicle)
                        .Include(a => a.Driver)
                        .Include(a => a.DestinationEntity)
                        .Where(a => a.ActivityType.Contains("sports", StringComparison.OrdinalIgnoreCase) ||
                                   a.ActivityType.Contains("game", StringComparison.OrdinalIgnoreCase) ||
                                   a.ActivityType.Contains("match", StringComparison.OrdinalIgnoreCase) ||
                                   a.ActivityType.Contains("tournament", StringComparison.OrdinalIgnoreCase) ||
                                   a.ActivityType.Contains("competition", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(a => a.Date)
                        .ThenBy(a => a.LeaveTime)
                        .ToListAsync();

                    // Load available buses (active and available)
                    var vehicles = await _context.Buses
                        .Where(v => v.IsAvailable)
                        .OrderBy(v => v.BusNumber)
                        .ToListAsync();

                    // Load available drivers (active)
                    var drivers = await _context.Drivers
                        .Where(d => d.IsActive)
                        .OrderBy(d => d.LastName)
                        .ThenBy(d => d.FirstName)
                        .ToListAsync();

                    // Load sports destinations
                    var destinations = await _context.Destinations
                        .Where(d => d.IsActive && d.DestinationType == BusBuddy.Core.Models.DestinationTypes.SportsEvent)
                        .OrderBy(d => d.Name)
                        .ToListAsync();

                    // Update UI collections
                    SportsActivities.Clear();
                    foreach (var activity in sportsActivities)
                    {
                        SportsActivities.Add(activity);
                    }

                    AvailableBuses.Clear();
                    foreach (var vehicle in vehicles)
                    {
                        AvailableBuses.Add(vehicle);
                    }

                    AvailableDrivers.Clear();
                    foreach (var driver in drivers)
                    {
                        AvailableDrivers.Add(driver);
                    }

                    SportsDestinations.Clear();
                    foreach (var destination in destinations)
                    {
                        SportsDestinations.Add(destination);
                    }

                    // Apply initial filters
                    await ApplyFiltersAsync();

                    // Update statistics
                    UpdateStatistics();

                    Logger.Information("Loaded {ActivityCount} sports activities, {VehicleCount} vehicles, {DriverCount} drivers, {DestinationCount} destinations",
                        sportsActivities.Count, vehicles.Count, drivers.Count, destinations.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading sports scheduler data");
                MessageBox.Show($"Error loading sports scheduler data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Apply current filters to activities
        /// </summary>
        private async Task ApplyFiltersAsync()
        {
            await Task.Run(() =>
            {
                var filtered = SportsActivities.Where(a =>
                    a.Date >= FilterStartDate &&
                    a.Date <= FilterEndDate &&
                    (FilterSport == "All" || a.ActivityType.Contains(FilterSport, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredActivities.Clear();
                    foreach (var activity in filtered)
                    {
                        FilteredActivities.Add(activity);
                    }
                });
            });
        }

        /// <summary>
        /// Update statistics display
        /// </summary>
        private void UpdateStatistics()
        {
            TotalSportsEvents = SportsActivities.Count;
            AwayGames = SportsActivities.Count(a => a.DestinationEntity != null);
            HomeGames = SportsActivities.Count(a => a.DestinationEntity == null || a.Destination.Contains("home", StringComparison.OrdinalIgnoreCase));
            UnassignedActivities = SportsActivities.Count(a => a.AssignedVehicleId == 0 || a.DriverId == 0);
        }

        #endregion

        #region Assignment Methods

        /// <summary>
        /// Assign selected vehicle to selected activity
        /// </summary>
        private async Task AssignVehicleAsync()
        {
            if (SelectedActivity == null || SelectedBus == null)
            {
                return;
            }

            try
            {
                using (LogContext.PushProperty("Operation", "AssignVehicle"))
                {
                    Logger.Information("Assigning vehicle {BusNumber} to activity {ActivityId}",
                        SelectedBus.BusNumber, SelectedActivity.ActivityId);

                    // Check for conflicts
                    var hasConflict = await CheckVehicleConflictAsync(SelectedBus, SelectedActivity);
                    if (hasConflict)
                    {
                        var result = MessageBox.Show(
                            "This vehicle has a scheduling conflict. Do you want to proceed anyway?",
                            "Scheduling Conflict",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                        {
                            return;
                        }
                    }

                    // Update assignment
                    SelectedActivity.AssignedVehicleId = SelectedBus.VehicleId;
                    SelectedActivity.AssignedVehicle = SelectedBus;

                    await _context.SaveChangesAsync();

                    UpdateStatistics();
                    Logger.Information("Vehicle assignment successful");

                    MessageBox.Show("Vehicle assigned successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error assigning vehicle");
                MessageBox.Show($"Error assigning vehicle: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Assign selected driver to selected activity
        /// </summary>
        private async Task AssignDriverAsync()
        {
            if (SelectedActivity == null || SelectedDriver == null)
            {
                return;
            }

            try
            {
                using (LogContext.PushProperty("Operation", "AssignDriver"))
                {
                    Logger.Information("Assigning driver {DriverName} to activity {ActivityId}",
                        $"{SelectedDriver.FirstName} {SelectedDriver.LastName}", SelectedActivity.ActivityId);

                    // Check for conflicts
                    var hasConflict = await CheckDriverConflictAsync(SelectedDriver, SelectedActivity);
                    if (hasConflict)
                    {
                        var result = MessageBox.Show(
                            "This driver has a scheduling conflict. Do you want to proceed anyway?",
                            "Scheduling Conflict",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                        {
                            return;
                        }
                    }

                    // Update assignment
                    SelectedActivity.DriverId = SelectedDriver.DriverId;
                    SelectedActivity.Driver = SelectedDriver;

                    await _context.SaveChangesAsync();

                    UpdateStatistics();
                    Logger.Information("Driver assignment successful");

                    MessageBox.Show("Driver assigned successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error assigning driver");
                MessageBox.Show($"Error assigning driver: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Conflict Detection

        /// <summary>
        /// Check for vehicle scheduling conflicts
        /// </summary>
        private async Task<bool> CheckVehicleConflictAsync(BusModel vehicle, ActivityModel activity)
        {
            var conflicts = await _context.Activities
                .Where(a => a.AssignedVehicleId == vehicle.VehicleId &&
                           a.Date == activity.Date &&
                           a.ActivityId != activity.ActivityId &&
                           ((a.LeaveTime <= activity.EventTime && a.EventTime >= activity.LeaveTime)))
                .CountAsync();

            return conflicts > 0;
        }

        /// <summary>
        /// Check for driver scheduling conflicts
        /// </summary>
        private async Task<bool> CheckDriverConflictAsync(DriverModel driver, ActivityModel activity)
        {
            var conflicts = await _context.Activities
                .Where(a => a.DriverId == driver.DriverId &&
                           a.Date == activity.Date &&
                           a.ActivityId != activity.ActivityId &&
                           ((a.LeaveTime <= activity.EventTime && a.EventTime >= activity.LeaveTime)))
                .CountAsync();

            return conflicts > 0;
        }

        /// <summary>
        /// Check all conflicts for current schedule
        /// </summary>
        private async Task CheckConflictsAsync()
        {
            try
            {
                IsLoading = true;
                var conflicts = new List<string>();

                foreach (var activity in SportsActivities)
                {
                    if (activity.AssignedVehicle != null)
                    {
                        var vehicleConflict = await CheckVehicleConflictAsync(activity.AssignedVehicle, activity);
                        if (vehicleConflict)
                        {
                            conflicts.Add($"Vehicle {activity.AssignedVehicle.BusNumber} conflict on {activity.Date:MM/dd} - {activity.ActivityType}");
                        }
                    }

                    if (activity.Driver != null)
                    {
                        var driverConflict = await CheckDriverConflictAsync(activity.Driver, activity);
                        if (driverConflict)
                        {
                            conflicts.Add($"Driver {activity.Driver.FirstName} {activity.Driver.LastName} conflict on {activity.Date:MM/dd} - {activity.ActivityType}");
                        }
                    }
                }

                if (conflicts.Count > 0)
                {
                    var message = "Scheduling conflicts detected:\n\n" + string.Join("\n", conflicts);
                    MessageBox.Show(message, "Scheduling Conflicts", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("No scheduling conflicts detected!", "All Clear", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking conflicts");
                MessageBox.Show($"Error checking conflicts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Activity Management

        private async Task AddSportsEventAsync()
        {
            // Implementation for adding new sports event
            // This would open a dialog or navigate to an edit form
            MessageBox.Show("Add Sports Event functionality - to be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task EditActivityAsync()
        {
            if (SelectedActivity == null)
            {
                return;
            }
            // Implementation for editing activity
            MessageBox.Show($"Edit activity: {SelectedActivity.ActivityType} - to be implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task DeleteActivityAsync()
        {
            if (SelectedActivity == null)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete the activity '{SelectedActivity.ActivityType}' on {SelectedActivity.Date:MM/dd/yyyy}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Activities.Remove(SelectedActivity);
                    await _context.SaveChangesAsync();

                    SportsActivities.Remove(SelectedActivity);
                    await ApplyFiltersAsync();
                    UpdateStatistics();

                    Logger.Information("Deleted sports activity {ActivityId}", SelectedActivity.ActivityId);
                    MessageBox.Show("Activity deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error deleting activity");
                    MessageBox.Show($"Error deleting activity: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadDataAsync();
        }

        #endregion

        #region Event Handlers

        private void OnSelectedActivityChanged()
        {
            // Update command availability
            ((AsyncRelayCommand)EditActivityCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)DeleteActivityCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)AssignVehicleCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)AssignDriverCommand).NotifyCanExecuteChanged();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context?.Dispose();
            }
        }

        #endregion
    }
}
