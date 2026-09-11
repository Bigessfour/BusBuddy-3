using System.Collections.ObjectModel;
using System.Diagnostics;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using CommunityToolkit.Mvvm.Input;
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
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        Logger.Information("DriverScheduleViewModel constructed — loading SfScheduler appointments");
        _ = LoadAsync();
    }

    public ObservableCollection<DriverScheduleAppointment> Appointments { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StatusMessage = "Loading driver schedules...";
            Logger.Information("Loading driver schedules for SfScheduler");
            var schedules = (await _scheduleService.GetSchedulesAsync()).ToList();
            Appointments.Clear();
            var skipped = 0;
            foreach (var schedule in schedules)
            {
                if (schedule.DepartureTime == default)
                {
                    skipped++;
                    Logger.Warning(
                        "Skipping schedule {ScheduleId} — DepartureTime is missing (not inventing 07:00)",
                        schedule.ScheduleId);
                    continue;
                }

                if (schedule.ArrivalTime == default || schedule.ArrivalTime <= schedule.DepartureTime)
                {
                    skipped++;
                    Logger.Warning(
                        "Skipping schedule {ScheduleId} — ArrivalTime missing or not after DepartureTime",
                        schedule.ScheduleId);
                    continue;
                }

                var routeLabel = schedule.Route?.RouteName;
                var subject = schedule.IsSportsTrip
                    ? schedule.DisplayTitle
                    : !string.IsNullOrWhiteSpace(routeLabel)
                        ? $"{routeLabel} — {schedule.Bus?.BusNumber ?? "TBD"}"
                        : schedule.DisplayTitle;

                Appointments.Add(new DriverScheduleAppointment
                {
                    StartTime = schedule.DepartureTime,
                    EndTime = schedule.ArrivalTime,
                    Subject = subject,
                    Location = schedule.Location ?? string.Empty,
                    Notes = $"{schedule.Status} — driver {schedule.Driver?.DriverName ?? schedule.DriverId.ToString()}"
                });
            }

            stopwatch.Stop();
            StatusMessage = skipped == 0
                ? $"{Appointments.Count} scheduled assignments"
                : $"{Appointments.Count} scheduled assignments ({skipped} skipped — incomplete times)";
            Logger.Information(
                "Driver schedules loaded Appointments={Count} Skipped={Skipped} SourceRows={SourceRows} ElapsedMs={ElapsedMs}",
                Appointments.Count, skipped, schedules.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DatabaseUserMessage.LogFailure(
                Logger,
                ex,
                "Failed to load driver schedules after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
            StatusMessage = DatabaseUserMessage.ForOperation(ex, "load driver schedules");
        }
    }
}
