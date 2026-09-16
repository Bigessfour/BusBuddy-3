using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using MaintenanceModel = BusBuddy.Core.Models.Maintenance;
using BusModel = BusBuddy.Core.Models.Bus;

namespace BusBuddy.WPF.ViewModels.Maintenance;

public class MaintenanceViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<MaintenanceViewModel>();
    private readonly IMaintenanceService _maintenanceService;
    private readonly IBusService _busService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private int _loadVersion;
    private MaintenanceModel? _selectedRecord;

    public MaintenanceViewModel(IMaintenanceService maintenanceService, IBusService busService)
    {
        _maintenanceService = maintenanceService;
        _busService = busService;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => SelectedRecord != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedRecord != null);
        Logger.Information("MaintenanceViewModel constructed — loading records");
        _ = LoadAsync();
    }

    public ObservableCollection<MaintenanceModel> Records { get; } = new();
    public ObservableCollection<BusModel> Vehicles { get; } = new();

    public IReadOnlyList<string> StatusOptions { get; } = MaintenanceRecordValidator.AllowedStatuses;
    public IReadOnlyList<string> PriorityOptions { get; } = MaintenanceRecordValidator.AllowedPriorities;

    public MaintenanceModel? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref _loadVersion);
        await _loadGate.WaitAsync().ConfigureAwait(true);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (version != _loadVersion)
            {
                return;
            }

            StatusMessage = "Loading maintenance records...";
            Logger.Information("Loading maintenance records and vehicles");
            var records = await _maintenanceService.GetAllMaintenanceRecordsAsync();
            if (version != _loadVersion)
            {
                return;
            }

            var buses = await _busService.GetAllBusesAsync();
            if (version != _loadVersion)
            {
                return;
            }

            Records.Clear();
            foreach (var record in records)
            {
                Records.Add(record);
            }

            Vehicles.Clear();
            foreach (var bus in buses)
            {
                Vehicles.Add(bus);
            }

            stopwatch.Stop();
            StatusMessage = $"{Records.Count} maintenance records";
            Logger.Information(
                "Maintenance UI loaded Records={RecordCount} Vehicles={VehicleCount} ElapsedMs={ElapsedMs}",
                Records.Count, Vehicles.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DatabaseUserMessage.LogFailure(Logger, ex, "Failed to load maintenance records after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            StatusMessage = DatabaseUserMessage.ForOperation(ex, "load maintenance records");
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task AddAsync()
    {
        var firstBus = Vehicles.FirstOrDefault();
        if (firstBus is null)
        {
            Logger.Warning("Add maintenance skipped — no vehicles loaded");
            StatusMessage = "Add a bus before creating maintenance records";
            return;
        }

        try
        {
            Logger.Information("Preparing draft maintenance row VehicleId={VehicleId}", firstBus.BusId);
            var draft = new MaintenanceModel
            {
                Date = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                VehicleId = firstBus.BusId,
                OdometerReading = firstBus.CurrentOdometer ?? 0,
                MaintenanceCompleted = string.Empty,
                Vendor = string.Empty,
                RepairCost = 0,
                Status = MaintenanceRecordValidator.AllowedStatuses[0],
                Priority = MaintenanceRecordValidator.AllowedPriorities[1]
            };
            Records.Insert(0, draft);
            SelectedRecord = draft;
            StatusMessage = "Fill in the new row, then click Save";
            Logger.Information("Draft maintenance row added for vehicle {VehicleId}", firstBus.BusId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add maintenance record");
            StatusMessage = "Add failed";
        }

        await Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (SelectedRecord is null)
        {
            Logger.Debug("Save skipped — no maintenance record selected");
            return;
        }

        try
        {
            if (SelectedRecord.MaintenanceId == 0)
            {
                Logger.Information("Creating maintenance record VehicleId={VehicleId}", SelectedRecord.VehicleId);
                var created = await _maintenanceService.CreateMaintenanceRecordAsync(SelectedRecord);
                if (!created.IsSuccess)
                {
                    StatusMessage = created.Error;
                    return;
                }

                StatusMessage = "Saved";
                Logger.Information("Created maintenance record {MaintenanceId}", created.Value.MaintenanceId);
            }
            else
            {
                Logger.Information("Saving maintenance record {MaintenanceId}", SelectedRecord.MaintenanceId);
                var updated = await _maintenanceService.UpdateMaintenanceRecordAsync(SelectedRecord);
                if (!updated.IsSuccess)
                {
                    StatusMessage = updated.Error;
                    return;
                }

                StatusMessage = "Saved";
                Logger.Information("Saved maintenance record {MaintenanceId}", SelectedRecord.MaintenanceId);
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(
                Logger,
                ex,
                "Failed to save maintenance record {MaintenanceId}",
                SelectedRecord.MaintenanceId);
            StatusMessage = DatabaseUserMessage.ForOperation(ex, "save this maintenance record");
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedRecord is null)
        {
            Logger.Debug("Delete skipped — no maintenance record selected");
            return;
        }

        var id = SelectedRecord.MaintenanceId;
        var confirm = MessageBox.Show(
            "Delete this maintenance record? This cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            Logger.Information("Delete cancelled for maintenance record {MaintenanceId}", id);
            StatusMessage = "Delete cancelled";
            return;
        }

        if (id == 0)
        {
            Records.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = "Draft discarded";
            return;
        }

        try
        {
            Logger.Information("Deleting maintenance record {MaintenanceId}", id);
            var deleted = await _maintenanceService.DeleteMaintenanceRecordAsync(id);
            if (!deleted.IsSuccess)
            {
                StatusMessage = deleted.Error;
                return;
            }

            Records.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = ClerkWriteMessages.Deleted($"Maintenance record {id}");
            Logger.Information("Deleted maintenance record {MaintenanceId} from UI", id);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Failed to delete maintenance record {MaintenanceId}", id);
            StatusMessage = DatabaseUserMessage.ForOperation(ex, "delete maintenance record");
        }
    }
}
