using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using Serilog;
using System;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Phase 2 Enhanced Activity Schedule ViewModel
    /// Features: Statistics, filtering, commands, real-time updates
    /// </summary>
    public class ActivityScheduleViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly BusBuddyDbContext _context;
        private static readonly ILogger Logger = Log.ForContext<ActivityScheduleViewModel>();

        // Collections
        public ObservableCollection<ActivitySchedule> ActivitySchedules { get; set; } = new();
        public ObservableCollection<ActivitySchedule> FilteredActivitySchedules { get; set; } = new();

        // Selected items
        private ActivitySchedule? _selectedActivity;
        public ActivitySchedule? SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                _selectedActivity = value;
                OnPropertyChanged();
            }
        }

        // Statistics properties
        private int _todayActivitiesCount;
        public int TodayActivitiesCount
        {
            get => _todayActivitiesCount;
            set
            {
                _todayActivitiesCount = value;
                OnPropertyChanged();
            }
        }

        private int _scheduledTripsCount;
        public int ScheduledTripsCount
        {
            get => _scheduledTripsCount;
            set
            {
                _scheduledTripsCount = value;
                OnPropertyChanged();
            }
        }

        private int _inProgressCount;
        public int InProgressCount
        {
            get => _inProgressCount;
            set
            {
                _inProgressCount = value;
                OnPropertyChanged();
            }
        }

        private int _needsAttentionCount;
        public int NeedsAttentionCount
        {
            get => _needsAttentionCount;
            set
            {
                _needsAttentionCount = value;
                OnPropertyChanged();
            }
        }

        // Filter properties
        private string _statusFilter = "All";
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                _statusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _startDateFilter = DateTime.Today.ToString("yyyy-MM-dd");
        public string StartDateFilter
        {
            get => _startDateFilter;
            set
            {
                _startDateFilter = value;
                OnPropertyChanged();
            }
        }

        private string _endDateFilter = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        public string EndDateFilter
        {
            get => _endDateFilter;
            set
            {
                _endDateFilter = value;
                OnPropertyChanged();
            }
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value;
                OnPropertyChanged();
            }
        }

        private string _selectedViewType = "Month";
        public string SelectedViewType
        {
            get => _selectedViewType;
            set
            {
                _selectedViewType = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand AddActivityCommand { get; }
        public ICommand EditActivityCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand ConfirmActivityCommand { get; }
        public ICommand StartActivityCommand { get; }
        public ICommand CompleteActivityCommand { get; }
        public ICommand CancelActivityCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand GoToTodayCommand { get; }

        public ActivityScheduleViewModel()
        {
            _context = new BusBuddyDbContext();

            // Initialize commands - using parameter-based RelayCommand
            RefreshCommand = new AsyncRelayCommand(LoadActivitySchedulesAsync);
            AddActivityCommand = new RelayCommand(AddNewActivity);
            EditActivityCommand = new RelayCommand(EditActivity, () => SelectedActivity != null);
            ViewDetailsCommand = new RelayCommand(ViewDetails, () => SelectedActivity != null);
            ConfirmActivityCommand = new AsyncRelayCommand(async () => await UpdateActivityStatus("Confirmed"), () => SelectedActivity != null);
            StartActivityCommand = new AsyncRelayCommand(async () => await UpdateActivityStatus("In Progress"), () => SelectedActivity != null);
            CompleteActivityCommand = new AsyncRelayCommand(async () => await UpdateActivityStatus("Completed"), () => SelectedActivity != null);
            CancelActivityCommand = new AsyncRelayCommand(async () => await UpdateActivityStatus("Cancelled"), () => SelectedActivity != null);
            ApplyFilterCommand = new RelayCommand(ApplyFilters);
            GoToTodayCommand = new RelayCommand(() => { SelectedDate = DateTime.Today; });

            // Load initial data
            _ = LoadActivitySchedulesAsync();
        }

        public async Task LoadActivitySchedulesAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("📅 Loading activity schedules...");

                var schedules = await _context.ActivitySchedules
                    .Include("ScheduledDriver")
                    .Include("ScheduledVehicle")
                    .OrderBy(a => a.ScheduledDate)
                    .ThenBy(a => a.ScheduledLeaveTime)
                    .ToListAsync();

                ActivitySchedules.Clear();
                foreach (var schedule in schedules)
                {
                    ActivitySchedules.Add(schedule);
                }

                ApplyFilters();
                UpdateStatistics();

                Logger.Information($"✅ Loaded {schedules.Count} activity schedules");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error loading activity schedules");
                MessageBox.Show($"Error loading activity schedules: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            try
            {
                var filtered = ActivitySchedules.AsEnumerable();

                // Status filter
                if (StatusFilter != "All")
                {
                    filtered = filtered.Where(a => a.Status == StatusFilter);
                }

                // Date range filter
                if (DateTime.TryParse(StartDateFilter, out var startDate) &&
                    DateTime.TryParse(EndDateFilter, out var endDate))
                {
                    filtered = filtered.Where(a => a.ScheduledDate >= startDate && a.ScheduledDate <= endDate);
                }

                FilteredActivitySchedules.Clear();
                foreach (var item in filtered.OrderBy(a => a.ScheduledDate).ThenBy(a => a.ScheduledLeaveTime))
                {
                    FilteredActivitySchedules.Add(item);
                }

                Logger.Debug($"🔍 Applied filters: {FilteredActivitySchedules.Count} items shown");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error applying filters");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                var today = DateTime.Today;

                TodayActivitiesCount = ActivitySchedules.Count(a => a.ScheduledDate.Date == today);
                ScheduledTripsCount = ActivitySchedules.Count(a => a.Status == "Scheduled");
                InProgressCount = ActivitySchedules.Count(a => a.Status == "In Progress");
                NeedsAttentionCount = ActivitySchedules.Count(a =>
                    a.Status == "Cancelled" ||
                    (a.ScheduledDate < today && a.Status != "Completed"));

                Logger.Debug($"📊 Statistics updated: Today={TodayActivitiesCount}, Scheduled={ScheduledTripsCount}, InProgress={InProgressCount}, NeedsAttention={NeedsAttentionCount}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error updating statistics");
            }
        }

        private void AddNewActivity()
        {
            try
            {
                Logger.Information("➕ Adding new activity");
                // TODO: Open Add Activity dialog/window
                MessageBox.Show("Add Activity feature will be implemented in Phase 2.1", "Coming Soon",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error adding new activity");
                MessageBox.Show($"Error adding activity: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditActivity()
        {
            try
            {
                if (SelectedActivity == null)
                {
                    return;
                }

                Logger.Information($"✏️ Editing activity: {SelectedActivity.Subject}");
                // TODO: Open Edit Activity dialog/window
                MessageBox.Show($"Edit Activity feature will be implemented in Phase 2.1\n\nSelected: {SelectedActivity.Subject}",
                              "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error editing activity");
                MessageBox.Show($"Error editing activity: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewDetails()
        {
            try
            {
                if (SelectedActivity == null)
                {
                    return;
                }

                Logger.Information($"👁️ Viewing activity details: {SelectedActivity.Subject}");
                var details = $"Activity Details:\n\n" +
                             $"Date: {SelectedActivity.ScheduledDate:yyyy-MM-dd}\n" +
                             $"Type: {SelectedActivity.TripType}\n" +
                             $"Destination: {SelectedActivity.ScheduledDestination}\n" +
                             $"Leave Time: {SelectedActivity.ScheduledLeaveTime}\n" +
                             $"Event Time: {SelectedActivity.ScheduledEventTime}\n" +
                             $"Driver: {SelectedActivity.ScheduledDriver?.DriverName ?? "Not assigned"}\n" +
                             $"Vehicle: {SelectedActivity.ScheduledVehicle?.BusNumber ?? "Not assigned"}\n" +
                             $"Status: {SelectedActivity.Status}\n" +
                             $"Requested By: {SelectedActivity.RequestedBy}\n" +
                             $"Notes: {SelectedActivity.Notes ?? "None"}";

                MessageBox.Show(details, "Activity Details", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "❌ Error viewing activity details");
                MessageBox.Show($"Error viewing details: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateActivityStatus(string newStatus)
        {
            try
            {
                if (SelectedActivity == null)
                {
                    return;
                }

                Logger.Information($"🔄 Updating activity status to: {newStatus}");

                SelectedActivity.Status = newStatus;
                SelectedActivity.UpdatedDate = DateTime.Now;
                SelectedActivity.UpdatedBy = Environment.UserName;

                await _context.SaveChangesAsync();

                UpdateStatistics();
                ApplyFilters();

                Logger.Information($"✅ Status updated successfully to: {newStatus}");
                MessageBox.Show($"Activity status updated to: {newStatus}", "Success",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"❌ Error updating activity status to {newStatus}");
                MessageBox.Show($"Error updating status: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // IDisposable implementation
        public void Dispose()
        {
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
