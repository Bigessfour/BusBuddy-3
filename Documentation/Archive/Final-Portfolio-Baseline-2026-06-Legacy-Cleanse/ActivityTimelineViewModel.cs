using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Syncfusion.UI.Xaml.Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels
{
    public class ActivityTimelineViewModel : BaseInDevelopmentViewModel
    {
        private readonly IActivityLogService _logService;
        private ObservableCollection<ActivityTimelineEvent> _timelineEvents = new();
        private DateTime _startDate = DateTime.Now.AddDays(-7);
        private DateTime _endDate = DateTime.Now;
        private bool _isCustomDateRange;
        private DateRangeOption _selectedDateRange = new DateRangeOption { Range = DateRange.LastWeek, DisplayName = "Last 7 Days" };
        private ObservableCollection<EventTypeOption> _selectedEventTypes = new();
        private bool _hasNoData;

        public ObservableCollection<ActivityTimelineEvent> TimelineEvents
        {
            get => _timelineEvents;
            set => SetProperty(ref _timelineEvents, value);
        }

        public ObservableCollection<DateRangeOption> DateRanges { get; } = new();

        public DateRangeOption SelectedDateRange
        {
            get => _selectedDateRange;
            set
            {
                if (SetProperty(ref _selectedDateRange, value))
                {
                    IsCustomDateRange = value.Range == DateRange.Custom;

                    if (value.Range != DateRange.Custom)
                    {
                        UpdateDateRangeFromPreset(value.Range);
                        _ = RefreshTimelineAsync();
                    }
                }
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public bool IsCustomDateRange
        {
            get => _isCustomDateRange;
            set => SetProperty(ref _isCustomDateRange, value);
        }

        public ObservableCollection<EventTypeOption> EventTypes { get; } = new();

        public ObservableCollection<EventTypeOption> SelectedEventTypes
        {
            get => _selectedEventTypes;
            set => SetProperty(ref _selectedEventTypes, value);
        }

        public ObservableCollection<EventLegendItem> EventLegendItems { get; } = new();

        public bool HasNoData
        {
            get => _hasNoData;
            set => SetProperty(ref _hasNoData, value);
        }

        public ICommand RefreshCommand { get; }

        public ActivityTimelineViewModel(IActivityLogService logService)
            : base()
        {
            _logService = logService;

            // Initialize commands
            RefreshCommand = new AsyncRelayCommand(RefreshTimelineAsync);

            // Set up date range options
            InitializeDateRanges();

            // Set up event type options
            InitializeEventTypes();

            // Set up event legend
            InitializeEventLegend();

            // Set initial date range
            SelectedDateRange = DateRanges.First(d => d.Range == DateRange.LastWeek);

            // Populate timeline
            _ = RefreshTimelineAsync();
        }

        private void InitializeDateRanges()
        {
            DateRanges.Clear();
            DateRanges.Add(new DateRangeOption { Range = DateRange.Today, DisplayName = "Today" });
            DateRanges.Add(new DateRangeOption { Range = DateRange.Yesterday, DisplayName = "Yesterday" });
            DateRanges.Add(new DateRangeOption { Range = DateRange.Last24Hours, DisplayName = "Last 24 Hours" });
            DateRanges.Add(new DateRangeOption { Range = DateRange.LastWeek, DisplayName = "Last 7 Days" });
            DateRanges.Add(new DateRangeOption { Range = DateRange.LastMonth, DisplayName = "Last 30 Days" });
            DateRanges.Add(new DateRangeOption { Range = DateRange.Custom, DisplayName = "Custom Range" });
        }

        private void InitializeEventTypes()
        {
            EventTypes.Clear();

            // Create standard event type options with their respective colors
            EventTypes.Add(new EventTypeOption { EventType = "Create", DisplayName = "Create Operations", Color = new SolidColorBrush(Colors.Green) });
            EventTypes.Add(new EventTypeOption { EventType = "Read", DisplayName = "Read Operations", Color = new SolidColorBrush(Colors.Blue) });
            EventTypes.Add(new EventTypeOption { EventType = "Update", DisplayName = "Update Operations", Color = new SolidColorBrush(Colors.Orange) });
            EventTypes.Add(new EventTypeOption { EventType = "Delete", DisplayName = "Delete Operations", Color = new SolidColorBrush(Colors.Red) });
            EventTypes.Add(new EventTypeOption { EventType = "Login", DisplayName = "User Login/Logout", Color = new SolidColorBrush(Colors.Purple) });
            EventTypes.Add(new EventTypeOption { EventType = "Error", DisplayName = "Errors/Exceptions", Color = new SolidColorBrush(Colors.DarkRed) });
            EventTypes.Add(new EventTypeOption { EventType = "System", DisplayName = "System Events", Color = new SolidColorBrush(Colors.Gray) });

            // Select all event types by default
            foreach (var eventType in EventTypes)
            {
                SelectedEventTypes.Add(eventType);
            }
        }

        private void InitializeEventLegend()
        {
            EventLegendItems.Clear();

            // Add legend items for each event type
            foreach (var eventType in EventTypes)
            {
                EventLegendItems.Add(new EventLegendItem
                {
                    Name = eventType.DisplayName,
                    Color = eventType.Color
                });
            }
        }

        private void UpdateDateRangeFromPreset(DateRange range)
        {
            DateTime now = DateTime.Now;

            switch (range)
            {
                case DateRange.Today:
                    StartDate = now.Date;
                    EndDate = now.Date.AddDays(1).AddSeconds(-1);
                    break;
                case DateRange.Yesterday:
                    StartDate = now.Date.AddDays(-1);
                    EndDate = now.Date.AddSeconds(-1);
                    break;
                case DateRange.Last24Hours:
                    StartDate = now.AddDays(-1);
                    EndDate = now;
                    break;
                case DateRange.LastWeek:
                    StartDate = now.Date.AddDays(-7);
                    EndDate = now;
                    break;
                case DateRange.LastMonth:
                    StartDate = now.Date.AddDays(-30);
                    EndDate = now;
                    break;
            }
        }

        private async Task RefreshTimelineAsync()
        {
            try
            {
                IsLoading = true;
                HasNoData = false;

                Logger.Debug("Refreshing activity timeline with filter: Start={StartDate}, End={EndDate}, EventTypes={EventTypes}",
                    StartDate, EndDate, string.Join(",", SelectedEventTypes.Select(e => e.EventType)));

                // Get all logs (with increased limit for timeline) from service
                var allLogs = await _logService.GetLogsAsync(1000);

                // Apply date filter
                var dateFilteredLogs = allLogs
                    .Where(log => log.Timestamp >= StartDate && log.Timestamp <= EndDate)
                    .ToList();

                // Apply event type filter if any types are selected
                var selectedEventTypeStrings = SelectedEventTypes.Select(t => t.EventType).ToList();
                var filteredLogs = dateFilteredLogs;

                if (SelectedEventTypes.Count > 0 && SelectedEventTypes.Count < EventTypes.Count)
                {
                    filteredLogs = dateFilteredLogs
                        .Where(log =>
                        {
                            // Map log action to event type
                            string eventType = DetermineEventType(log.Action);
                            return selectedEventTypeStrings.Contains(eventType);
                        })
                        .ToList();
                }

                // Convert logs to timeline events
                TimelineEvents.Clear();
                foreach (var log in filteredLogs)
                {
                    var eventType = DetermineEventType(log.Action);
                    var eventOption = EventTypes.FirstOrDefault(e => e.EventType == eventType);

                    // Default to gray if no matching event type
                    var color = eventOption?.Color ?? new SolidColorBrush(Colors.Gray);

                    // Create timeline event - give each a minimum duration of 1 minute for visibility
                    var timelineEvent = new ActivityTimelineEvent
                    {
                        Subject = $"{log.Action} by {log.User}",
                        StartTime = log.Timestamp,
                        EndTime = log.Timestamp.AddMinutes(1),
                        Details = log.Details,
                        EventColor = color,
                        TextColor = new SolidColorBrush(Colors.White),
                        LogId = log.Id
                    };

                    TimelineEvents.Add(timelineEvent);
                }

                // Check if we have data to display
                HasNoData = TimelineEvents.Count == 0;

                Logger.Information("Loaded {FilteredCount} activity timeline events after filtering from {TotalCount} total logs",
                    TimelineEvents.Count, allLogs.Count());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading activity timeline");
                // We'll leave error handling to the UI via HasNoData
                HasNoData = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string DetermineEventType(string action)
        {
            // Map common action patterns to event types
            action = action.ToLower();

            if (action.Contains("creat") || action.Contains("add") || action.Contains("new"))
            {

                return "Create";
            }


            if (action.Contains("updat") || action.Contains("edit") || action.Contains("modif") || action.Contains("chang"))
            {

                return "Update";
            }


            if (action.Contains("delet") || action.Contains("remov"))
            {

                return "Delete";
            }


            if (action.Contains("view") || action.Contains("get") || action.Contains("fetch") || action.Contains("load") || action.Contains("read"))
            {

                return "Read";
            }


            if (action.Contains("login") || action.Contains("logout") || action.Contains("auth"))
            {

                return "Login";
            }


            if (action.Contains("error") || action.Contains("except") || action.Contains("fail"))
            {

                return "Error";
            }

            // Default to system events

            return "System";
        }
    }

    public class ActivityTimelineEvent : Syncfusion.UI.Xaml.Scheduler.ScheduleAppointment
    {
        public int LogId { get; set; }
        public Brush EventColor { get; set; } = new SolidColorBrush(Colors.Blue);
        public Brush TextColor { get; set; } = new SolidColorBrush(Colors.White);
        public string? Details { get; set; }
    }

    public class EventTypeOption
    {
        public string EventType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Brush Color { get; set; } = new SolidColorBrush(Colors.Gray);
    }

    public class EventLegendItem
    {
        public string Name { get; set; } = string.Empty;
        public Brush Color { get; set; } = new SolidColorBrush(Colors.Gray);
    }

    public enum DateRange
    {
        Today,
        Yesterday,
        Last24Hours,
        LastWeek,
        LastMonth,
        Custom
    }

    public class DateRangeOption
    {
        public DateRange Range { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
