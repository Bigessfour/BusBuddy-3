using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using BusBuddy.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Activity
{
    public partial class ActivityTimelineViewModel : ObservableObject, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<ActivityTimelineViewModel>();
        private readonly IActivityLogService? _logService;
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private int _refreshVersion;

        public ActivityTimelineViewModel(IActivityLogService? logService)
        {
            _logService = logService;
            RefreshCommand = new AsyncRelayCommand(RefreshTimelineAsync);
            InitializeDateRanges();
            InitializeEventTypes();
            InitializeEventLegend();
            SelectedDateRange = DateRanges.First(d => d.Range == DateRange.LastWeek);
            if (_logService is null)
            {
                Logger.Warning("ActivityTimeline opened without IActivityLogService — timeline cannot load");
            }

            _ = RefreshTimelineAsync();
        }

        [ObservableProperty]
        private ObservableCollection<ActivityTimelineRow> timelineEvents = new();

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasNoData;

        [ObservableProperty]
        private bool isCustomDateRange;

        [ObservableProperty]
        private DateTime startDate = DateTime.Now.AddDays(-7);

        [ObservableProperty]
        private DateTime endDate = DateTime.Now;

        partial void OnStartDateChanged(DateTime value)
        {
            if (IsCustomDateRange)
            {
                _ = RefreshTimelineAsync();
            }
        }

        partial void OnEndDateChanged(DateTime value)
        {
            if (IsCustomDateRange)
            {
                _ = RefreshTimelineAsync();
            }
        }

        private DateRangeOption _selectedDateRange = new() { Range = DateRange.LastWeek, DisplayName = "Last 7 Days" };

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

        public ObservableCollection<DateRangeOption> DateRanges { get; } = new();
        public ObservableCollection<EventTypeOption> EventTypes { get; } = new();
        public ObservableCollection<EventLegendItem> EventLegendItems { get; } = new();

        public ICommand RefreshCommand { get; }

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
            AddEventType("Create", "Create Operations", Colors.Green);
            AddEventType("Read", "Read Operations", Colors.Blue);
            AddEventType("Update", "Update Operations", Colors.Orange);
            AddEventType("Delete", "Delete Operations", Colors.Red);
            AddEventType("Login", "User Login/Logout", Colors.Purple);
            AddEventType("Error", "Errors/Exceptions", Colors.DarkRed);
            AddEventType("System", "System Events", Colors.Gray);
        }

        private void AddEventType(string type, string display, Color color)
        {
            var option = new EventTypeOption(type, display, new SolidColorBrush(color));
            option.FilterChanged += (_, _) => _ = RefreshTimelineAsync();
            EventTypes.Add(option);
        }

        private void InitializeEventLegend()
        {
            EventLegendItems.Clear();
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
            var now = DateTime.Now;
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
            if (_logService is null)
            {
                HasNoData = true;
                Logger.Warning("Activity timeline refresh skipped — IActivityLogService is not registered");
                return;
            }

            var version = Interlocked.Increment(ref _refreshVersion);
            await _refreshGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (version != _refreshVersion)
                {
                    return;
                }

                IsLoading = true;
                HasNoData = false;

                var allLogs = await _logService.GetLogsAsync(1000).ConfigureAwait(true);
                if (version != _refreshVersion)
                {
                    return;
                }

                var dateFiltered = allLogs
                    .Where(log => log.Timestamp >= StartDate && log.Timestamp <= EndDate)
                    .ToList();

                var selectedTypes = EventTypes.Where(t => t.IsSelected).Select(t => t.EventType).ToHashSet();
                var filtered = selectedTypes.Count == 0 || selectedTypes.Count == EventTypes.Count
                    ? dateFiltered
                    : dateFiltered.Where(log => selectedTypes.Contains(DetermineEventType(log.Action))).ToList();

                TimelineEvents.Clear();
                foreach (var log in filtered)
                {
                    TimelineEvents.Add(new ActivityTimelineRow
                    {
                        Subject = $"{log.Action} by {log.User}",
                        StartTime = log.Timestamp,
                        EndTime = log.Timestamp.AddMinutes(1),
                        Details = log.Details ?? string.Empty,
                        LogId = log.Id
                    });
                }

                HasNoData = TimelineEvents.Count == 0;
                Logger.Information(
                    "Activity timeline refreshed Rows={Count} HasNoData={HasNoData} Start={Start} End={End}",
                    TimelineEvents.Count,
                    HasNoData,
                    StartDate,
                    EndDate);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Activity timeline refresh failed");
                HasNoData = true;
            }
            finally
            {
                if (version == _refreshVersion)
                {
                    IsLoading = false;
                }

                _refreshGate.Release();
            }
        }

        private static string DetermineEventType(string action)
        {
            var lower = action.ToLowerInvariant();
            if (lower.Contains("creat") || lower.Contains("add") || lower.Contains("new")) return "Create";
            if (lower.Contains("updat") || lower.Contains("edit") || lower.Contains("modif") || lower.Contains("chang")) return "Update";
            if (lower.Contains("delet") || lower.Contains("remov")) return "Delete";
            if (lower.Contains("view") || lower.Contains("get") || lower.Contains("fetch") || lower.Contains("load") || lower.Contains("read")) return "Read";
            if (lower.Contains("login") || lower.Contains("logout") || lower.Contains("auth")) return "Login";
            if (lower.Contains("error") || lower.Contains("except") || lower.Contains("fail")) return "Error";
            return "System";
        }

        public void Dispose()
        {
            _refreshGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class ActivityTimelineRow
    {
        public string Subject { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Details { get; set; } = string.Empty;
        public int LogId { get; set; }
    }

    public partial class EventTypeOption : ObservableObject
    {
        public EventTypeOption(string eventType, string displayName, Brush color)
        {
            EventType = eventType;
            DisplayName = displayName;
            Color = color;
        }

        public string EventType { get; }
        public string DisplayName { get; }
        public Brush Color { get; }
        public event EventHandler? FilterChanged;

        [ObservableProperty]
        private bool isSelected = true;

        partial void OnIsSelectedChanged(bool value) => FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    public class EventLegendItem
    {
        public string Name { get; set; } = string.Empty;
        public Brush Color { get; set; } = Brushes.Gray;
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
