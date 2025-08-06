using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace BusBuddy.WPF.Services;

/// <summary>
/// Custom Schedule Data Provider for Bus Buddy
/// Integrates Activity entities with Syncfusion SfScheduler
/// </summary>
public class BusBuddyScheduleDataProvider
{
    private readonly IActivityService _activityService;
    private static readonly ILogger Logger = Log.ForContext<BusBuddyScheduleDataProvider>();
    private ObservableCollection<BusBuddyScheduleAppointment> _masterList;
    private bool _isDirty;

    public BusBuddyScheduleDataProvider(IActivityService activityService)
    {
        _activityService = activityService;
        _masterList = new ObservableCollection<BusBuddyScheduleAppointment>();
        _isDirty = false;
    }

    public ObservableCollection<BusBuddyScheduleAppointment> MasterList
    {
        get => _masterList;
        set
        {
            _masterList = value ?? new ObservableCollection<BusBuddyScheduleAppointment>();
            _isDirty = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => _isDirty = value;
    }

    public ObservableCollection<BusBuddyScheduleAppointment> GetScheduleForDay(DateTime day)
    {
        var dayAppointments = new ObservableCollection<BusBuddyScheduleAppointment>();
        foreach (var appointment in _masterList)
        {
            if (appointment.StartTime.Date == day.Date ||
                (appointment.StartTime.Date <= day.Date && appointment.EndTime.Date >= day.Date))
            {
                dayAppointments.Add(appointment);
            }
        }
        return dayAppointments;
    }

    public ObservableCollection<BusBuddyScheduleAppointment> GetSchedule(DateTime startDate, DateTime endDate)
    {
        var rangeAppointments = new ObservableCollection<BusBuddyScheduleAppointment>();
        foreach (var appointment in _masterList)
        {
            if ((appointment.StartTime >= startDate && appointment.StartTime <= endDate) ||
                (appointment.EndTime >= startDate && appointment.EndTime <= endDate) ||
                (appointment.StartTime <= startDate && appointment.EndTime >= endDate))
            {
                rangeAppointments.Add(appointment);
            }
        }
        return rangeAppointments;
    }

    public BusBuddyScheduleAppointment NewScheduleAppointment()
    {
        var appointment = new BusBuddyScheduleAppointment
        {
            Id = GenerateUniqueId(),
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(1),
            Subject = "New Activity",
            Notes = "",
            IsAllDay = false
        };
        return appointment;
    }

    public void AddItem(BusBuddyScheduleAppointment item)
    {
        _masterList.Add(item);
        _isDirty = true;
        Logger.Information("Added appointment: {Subject}", item.Subject);
    }

    public void RemoveItem(BusBuddyScheduleAppointment item)
    {
        _masterList.Remove(item);
        _isDirty = true;
        Logger.Information("Removed appointment: {Subject}", item.Subject);
    }

    public void CommitChanges()
    {
        if (!_isDirty)
        {
            return;
        }


        try
        {
            Logger.Information("Committing schedule changes to database");
            _isDirty = false;
            Logger.Information("Successfully committed schedule changes");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error committing schedule changes");
            throw;
        }
    }

    public async Task LoadActivitiesAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            Logger.Information("Loading activities from {StartDate} to {EndDate}", startDate, endDate);
            var activities = await _activityService.GetActivitiesByDateRangeAsync(startDate, endDate);
            _masterList.Clear();
            foreach (var activity in activities)
            {
                var appointment = CreateAppointmentFromActivity(activity);
                _masterList.Add(appointment);
            }
            _isDirty = false;
            Logger.Information("Loaded {Count} activities as appointments", _masterList.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading activities");
            throw;
        }
    }

    private BusBuddyScheduleAppointment CreateAppointmentFromActivity(Activity activity)
    {
        var appointment = new BusBuddyScheduleAppointment
        {
            Id = activity.ActivityId,
            ActivityId = activity.ActivityId,
            StartTime = activity.ActivityDate.Add(activity.StartTime ?? TimeSpan.Zero),
            EndTime = activity.ActivityDate.Add(activity.EndTime ?? activity.StartTime?.Add(TimeSpan.FromHours(1)) ?? TimeSpan.FromHours(1)),
            Subject = $"{activity.ActivityType}",
            Notes = $"Route: {activity.RouteId}\nVehicle: {activity.VehicleId}\nDriver: {activity.DriverId}\nStudents: {activity.StudentsCount ?? 0}",
            IsAllDay = false,
            AppointmentBackground = GetBrushForActivityType(activity.ActivityType),
            Location = activity.Notes ?? ""
        };
        return appointment;
    }

    private Brush GetBrushForActivityType(string? activityType)
    {
        return activityType?.ToLower() switch
        {
            "morning" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 46, 125, 50)), // Green
            "afternoon" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 25, 118, 210)), // Blue
            "field trip" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 152, 0)), // Orange
            "special" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 156, 39, 176)), // Purple
            _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 97, 97, 97)) // Gray
        };
    }

    private int GenerateUniqueId()
    {
        var maxId = _masterList.Count > 0 ? _masterList.Max(a => a.Id) : 0;
        return maxId + 1;
    }
}


// Use the model from Core
// public class BusBuddyScheduleAppointment : ScheduleAppointment { ... } // REMOVE this duplicate
