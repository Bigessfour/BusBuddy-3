using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Syncfusion.UI.Xaml.Scheduler;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Unified scheduler for Sports + Activities. Documentation-first patterns:
    /// https://help.syncfusion.com/wpf/scheduler/getting-started
    /// https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Scheduler.SfScheduler.html
    /// </summary>
    public class UnifiedSchedulerViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<UnifiedSchedulerViewModel>();
        private readonly BusBuddyDbContext _context;

        public ObservableCollection<ScheduleAppointment> Appointments { get; } = new();

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; OnPropertyChanged(); }
        }

        private SchedulerViewType _selectedViewType = SchedulerViewType.Month;
        public SchedulerViewType SelectedViewType
        {
            get => _selectedViewType;
            set { _selectedViewType = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public UnifiedSchedulerViewModel()
        {
            _context = new BusBuddyDbContext();
            _ = LoadAppointmentsAsync();
        }

        public async Task LoadAppointmentsAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading unified scheduler appointments...");

                // Load ActivitySchedule entries
                var activities = await _context.ActivitySchedules
                    .Include("ScheduledDriver")
                    .Include("ScheduledVehicle")
                    .ToListAsync();

                // Load Schedules (sports/general) — currently used for sports trips metadata
                var schedules = await _context.Schedules
                    .Include(s => s.Driver)
                    .Include(s => s.Bus)
                    .ToListAsync();

                Appointments.Clear();

                // Map ActivitySchedule to ScheduleAppointment
                foreach (var a in activities)
                {
                    var appt = new ScheduleAppointment
                    {
                        StartTime = a.StartDateTime,
                        EndTime = a.EndDateTime,
                        Subject = a.Subject,
                        IsAllDay = a.IsAllDay,
                        Location = a.ScheduledDestination,
                    };
                    Appointments.Add(appt);
                }

                // Map sports schedules (where SportsCategory is set)
                foreach (var s in schedules.Where(s => !string.IsNullOrEmpty(s.SportsCategory)))
                {
                    var start = s.DepartureTime != default ? s.DepartureTime : s.ScheduleDate;
                    var end = s.ArrivalTime != default ? s.ArrivalTime : s.ScheduleDate.AddHours(2);

                    var appt = new ScheduleAppointment
                    {
                        StartTime = start,
                        EndTime = end,
                        Subject = s.DisplayTitle,
                        IsAllDay = false,
                        Location = s.Location ?? s.DestinationTown,
                    };
                    Appointments.Add(appt);
                }

                Logger.Information("Unified scheduler loaded {Count} appointments", Appointments.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading unified scheduler appointments");
                MessageBox.Show($"Error loading scheduler: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
