using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using BusModel = BusBuddy.Core.Models.Bus;
using DriverModel = BusBuddy.Core.Models.Driver;

namespace BusBuddy.WPF.ViewModels.Duty;

/// <summary>Clerk surface for shared driver+bus duty events (#112).</summary>
public class DutyLogViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<DutyLogViewModel>();

    private readonly IDutyEventService _dutyEventService;
    private readonly IBusService _busService;
    private readonly IDriverService _driverService;
    private readonly Func<string, string, bool> _confirmDelete;

    private int _loadGeneration;
    private DateTime _selectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
    private DutyEvent? _selectedEvent;
    private int _draftDriverId;
    private int _draftBusId;
    private DutySession _draftSession = DutySession.AM;
    private DutyEventType _draftEventType = DutyEventType.PreTrip;
    private string _draftNotes = string.Empty;
    private string _draftOdometerText = string.Empty;

    public DutyLogViewModel(
        IDutyEventService dutyEventService,
        IBusService busService,
        IDriverService driverService,
        Func<string, string, bool>? confirmDelete = null)
    {
        _dutyEventService = dutyEventService;
        _busService = busService;
        _driverService = driverService;
        _confirmDelete = confirmDelete ?? DefaultConfirmDelete;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync, CanAdd);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedEvent != null);

        Logger.Information("DutyLogViewModel constructed — loading duty log");
        _ = LoadAsync();
    }

    private static bool DefaultConfirmDelete(string message, string caption) =>
        System.Windows.MessageBox.Show(
            message, caption,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning)
            == System.Windows.MessageBoxResult.Yes;

    public ObservableCollection<DutyEvent> Events { get; } = new();
    public ObservableCollection<MissingPreTripRow> MissingPreTrips { get; } = new();
    public ObservableCollection<BusModel> Buses { get; } = new();
    public ObservableCollection<DriverModel> Drivers { get; } = new();

    public IReadOnlyList<DutySession> SessionOptions { get; } = DutyEventValidator.AllowedSessions;
    public IReadOnlyList<DutyEventType> EventTypeOptions { get; } = DutyEventValidator.AllowedEventTypes;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var day = DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
            if (SetProperty(ref _selectedDate, day))
            {
                _ = LoadAsync();
            }
        }
    }

    public DutyEvent? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (SetProperty(ref _selectedEvent, value))
            {
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int DraftDriverId
    {
        get => _draftDriverId;
        set
        {
            if (SetProperty(ref _draftDriverId, value))
            {
                AddCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int DraftBusId
    {
        get => _draftBusId;
        set
        {
            if (SetProperty(ref _draftBusId, value))
            {
                AddCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DutySession DraftSession
    {
        get => _draftSession;
        set => SetProperty(ref _draftSession, value);
    }

    public DutyEventType DraftEventType
    {
        get => _draftEventType;
        set => SetProperty(ref _draftEventType, value);
    }

    public string DraftNotes
    {
        get => _draftNotes;
        set => SetProperty(ref _draftNotes, value ?? string.Empty);
    }

    /// <summary>Free-text odometer; parsed on Add. Empty means null.</summary>
    public string DraftOdometerText
    {
        get => _draftOdometerText;
        set => SetProperty(ref _draftOdometerText, value ?? string.Empty);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    private bool CanAdd() => DraftDriverId > 0 && DraftBusId > 0;

    /// <summary>Stamp EventedAt onto the clerk-selected calendar day (UTC), keeping clock from UtcNow.</summary>
    internal static DateTime EventedAtForSelectedDate(DateTime selectedDateUtc, DateTime utcNow)
    {
        var day = DateTime.SpecifyKind(selectedDateUtc.Date, DateTimeKind.Utc);
        var now = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : utcNow.ToUniversalTime();
        if (day == now.Date)
        {
            return now;
        }

        return day.Add(now.TimeOfDay);
    }

    private async Task LoadAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StatusMessage = "Loading duty log...";
            Logger.Information("Loading duty events for {Date:yyyy-MM-dd}", SelectedDate);

            var events = await _dutyEventService.GetForDateAsync(SelectedDate);
            var missing = await _dutyEventService.GetMissingPreTripsAsync(SelectedDate);
            var buses = await _busService.GetAllBusesAsync();
            var drivers = await _driverService.GetAllDriversAsync();

            if (generation != _loadGeneration)
            {
                Logger.Debug("Discarding stale duty log load generation={Generation}", generation);
                return;
            }

            Events.Clear();
            foreach (var row in events)
            {
                Events.Add(row);
            }

            MissingPreTrips.Clear();
            foreach (var row in missing)
            {
                MissingPreTrips.Add(row);
            }

            Buses.Clear();
            foreach (var bus in buses)
            {
                Buses.Add(bus);
            }

            Drivers.Clear();
            foreach (var driver in drivers)
            {
                Drivers.Add(driver);
            }

            if (DraftBusId <= 0 && Buses.Count > 0)
            {
                DraftBusId = Buses[0].BusId;
            }

            if (DraftDriverId <= 0 && Drivers.Count > 0)
            {
                DraftDriverId = Drivers[0].DriverId;
            }

            stopwatch.Stop();
            StatusMessage =
                $"{Events.Count} duty events · {MissingPreTrips.Count} missing pre-trips";
            Logger.Information(
                "Duty log loaded Events={EventCount} Missing={MissingCount} ElapsedMs={ElapsedMs}",
                Events.Count, MissingPreTrips.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            stopwatch.Stop();
            DatabaseUserMessage.LogFailure(
                Logger, ex, "Failed to load duty log after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            StatusMessage = DatabaseUserMessage.ForOperation(ex, "load duty log");
        }
    }

    private async Task AddAsync()
    {
        if (!CanAdd())
        {
            StatusMessage = "Select a driver and bus before adding";
            return;
        }

        int? odometer = null;
        if (!string.IsNullOrWhiteSpace(DraftOdometerText))
        {
            if (!int.TryParse(DraftOdometerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                || parsed < 0)
            {
                StatusMessage = "Odometer must be a non-negative whole number.";
                return;
            }

            odometer = parsed;
        }

        try
        {
            var draft = new DutyEvent
            {
                DriverId = DraftDriverId,
                BusId = DraftBusId,
                Session = DraftSession,
                EventType = DraftEventType,
                EventedAt = EventedAtForSelectedDate(SelectedDate, DateTime.UtcNow),
                Notes = string.IsNullOrWhiteSpace(DraftNotes) ? null : DraftNotes.Trim(),
                OdometerReading = odometer
            };

            Logger.Information(
                "Creating duty event Type={EventType} Session={Session} BusId={BusId} DriverId={DriverId} EventedAt={EventedAt:yyyy-MM-dd HH:mm}",
                draft.EventType, draft.Session, draft.BusId, draft.DriverId, draft.EventedAt);

            var created = await _dutyEventService.CreateAsync(draft);
            DraftNotes = string.Empty;
            DraftOdometerText = string.Empty;
            StatusMessage = created.EventType == DutyEventType.Defect
                ? $"Defect {created.DutyEventId} logged — also on Fleet Maintenance"
                : $"Saved duty event {created.DutyEventId}";
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            Logger.Warning(ex, "Duty event validation failed");
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Failed to create duty event");
            StatusMessage = ex is InvalidOperationException
                ? ex.Message
                : DatabaseUserMessage.ForOperation(ex, "save duty event");
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedEvent is null)
        {
            return;
        }

        var id = SelectedEvent.DutyEventId;
        if (!_confirmDelete(
                "Delete this duty event? Linked maintenance SourceDutyEventId will clear.",
                "Confirm Delete"))
        {
            StatusMessage = "Delete cancelled";
            return;
        }

        try
        {
            await _dutyEventService.DeleteAsync(id);
            StatusMessage = $"Deleted duty event {id}";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Failed to delete duty event {DutyEventId}", id);
            StatusMessage = DatabaseUserMessage.ForOperation(ex, "delete duty event");
        }
    }
}
