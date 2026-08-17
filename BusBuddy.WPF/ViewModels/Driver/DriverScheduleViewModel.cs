using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Commands;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Driver;

public class DriverScheduleAppointment
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class DriverScheduleViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<DriverScheduleViewModel>();
    private readonly IScheduleService _scheduleService;

    public DriverScheduleViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        Logger.Information("DriverScheduleViewModel constructed — loading SfScheduler appointments");
        _ = LoadAsync();
    }

    public ObservableCollection<DriverScheduleAppointment> Appointments { get; } = new();

    public ICommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StatusMessage = "Loading driver schedules...";
            Logger.Information("Loading driver schedules for SfScheduler");
            var schedules = await _scheduleService.GetSchedulesAsync();
            Appointments.Clear();
            foreach (var schedule in schedules)
            {
                var start = schedule.DepartureTime == default
                    ? schedule.ScheduleDate.Date.AddHours(7)
                    : schedule.DepartureTime;
                var end = schedule.ArrivalTime == default || schedule.ArrivalTime <= start
                    ? start.AddHours(1)
                    : schedule.ArrivalTime;
                Appointments.Add(new DriverScheduleAppointment
                {
                    StartTime = start,
                    EndTime = end,
                    Subject = schedule.DisplayTitle,
                    Location = schedule.Location ?? string.Empty,
                    Notes = $"{schedule.Status} — driver {schedule.Driver?.DriverName ?? schedule.DriverId.ToString()}"
                });
            }

            stopwatch.Stop();
            StatusMessage = $"{Appointments.Count} scheduled assignments";
            Logger.Information(
                "Driver schedules loaded Appointments={Count} SourceRows={SourceRows} ElapsedMs={ElapsedMs}",
                Appointments.Count, schedules.Count(), stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error(ex, "Failed to load driver schedules after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            StatusMessage = "Load failed";
        }
    }
}
