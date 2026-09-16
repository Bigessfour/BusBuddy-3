using System.Collections.ObjectModel;
using System.Diagnostics;
using BusBuddy.Core.Models;
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
    private readonly IScheduleService? _scheduleService;
    private readonly bool _publishedStopsOnly;

    public DriverScheduleViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        Logger.Information("DriverScheduleViewModel constructed — loading SfScheduler appointments");
        _ = LoadAsync();
    }

    /// <summary>
    /// Published stop times for one route row. Refresh does not swap in the district-wide driver calendar.
    /// </summary>
    public DriverScheduleViewModel(IReadOnlyList<DriverScheduleAppointment> publishedStops, string statusMessage)
    {
        _publishedStopsOnly = true;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        foreach (var appointment in publishedStops)
        {
            Appointments.Add(appointment);
        }

        StatusMessage = statusMessage;
    }

    public static List<DriverScheduleAppointment> FromPublishedStops(
        BusBuddy.Core.Models.Route route,
        IEnumerable<RouteStop> stops)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(stops);

        var day = route.Date.Date;
        return stops
            .OrderBy(s => s.StopOrder)
            .Select(stop =>
            {
                var start = DateTime.SpecifyKind(day.Add(stop.ScheduledArrival), DateTimeKind.Unspecified);
                var end = DateTime.SpecifyKind(day.Add(stop.ScheduledDeparture), DateTimeKind.Unspecified);
                if (end <= start)
                {
                    end = start.AddMinutes(Math.Max(1, stop.StopDuration));
                }

                return new DriverScheduleAppointment
                {
                    StartTime = start,
                    EndTime = end,
                    Subject = $"{stop.StopOrder}. {stop.StopName}",
                    Location = stop.StopAddress,
                    Notes = route.RouteName
                };
            })
            .ToList();
    }

    public ObservableCollection<DriverScheduleAppointment> Appointments { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        if (_publishedStopsOnly)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (_scheduleService is null)
            {
                StatusMessage = "Schedule service is not available.";
                return;
            }

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
