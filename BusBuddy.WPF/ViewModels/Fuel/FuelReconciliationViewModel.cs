using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog.Context;

namespace BusBuddy.WPF.ViewModels.Fuel;

public partial class FuelReconciliationViewModel : BaseViewModel, IDisposable
{
    private static readonly CultureInfo ParseCulture = CultureInfo.CurrentCulture;

    private readonly IFuelService _fuelService;
    private readonly IBusService _busService;
    private readonly IFuelLocationCatalog? _locationCatalog;
    private readonly IUserSettingsService? _settings;

    private CancellationTokenSource? _loadCts;
    private int _loadVersion;
    private bool _suppressReload;
    private bool _updatingBulkText;

    public ObservableCollection<string> FuelLocations { get; } = new() { "All Locations" };
    public ObservableCollection<DailyReconciliationItem> DailyReconciliation { get; } = new();
    public ObservableCollection<DiscrepancyDetailItem> DiscrepancyDetails { get; } = new();

    public IAsyncRelayCommand ExportCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand RememberBulkCommand { get; }

    public event EventHandler? CloseRequested;

    public FuelReconciliationViewModel(
        IFuelService fuelService,
        IBusService busService,
        IFuelLocationCatalog? locationCatalog = null,
        IUserSettingsService? settings = null)
    {
        _fuelService = fuelService ?? throw new ArgumentNullException(nameof(fuelService));
        _busService = busService ?? throw new ArgumentNullException(nameof(busService));
        _locationCatalog = locationCatalog;
        _settings = settings;

        ExportCommand = new AsyncRelayCommand(ExportAsync);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        RememberBulkCommand = new AsyncRelayCommand(RememberBulkReadingAsync, CanRememberBulk);

        ApplyNeutralDiscrepancyColors();
        _ = InitializeAsync();
    }

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private string _selectedLocation = "All Locations";

    [ObservableProperty]
    private string _bulkStationGallonsText = string.Empty;

    [ObservableProperty]
    private double _bulkStationGallons;

    [ObservableProperty]
    private double _vehicleUsageGallons;

    [ObservableProperty]
    private double _discrepancyGallons;

    [ObservableProperty]
    private double _discrepancyPercentage;

    [ObservableProperty]
    private bool _hasBulkReading;

    [ObservableProperty]
    private bool _hasDailyChartData;

    [ObservableProperty]
    private string _reconciliationSummary = "Loading reconciliation data...";

    [ObservableProperty]
    private DateTime? _lastReconciliationDate;

    [ObservableProperty]
    private Brush _discrepancyBackground = Brushes.Transparent;

    [ObservableProperty]
    private Brush _discrepancyBorder = Brushes.Gray;

    [ObservableProperty]
    private Brush _discrepancyForeground = Brushes.Gray;

    partial void OnStartDateChanged(DateTime value) => ScheduleReload();

    partial void OnEndDateChanged(DateTime value) => ScheduleReload();

    partial void OnSelectedLocationChanged(string value) => ScheduleReload();

    partial void OnBulkStationGallonsTextChanged(string value)
    {
        if (_updatingBulkText)
        {
            return;
        }

        RememberBulkCommand.NotifyCanExecuteChanged();
        ScheduleReload();
    }

    private async Task InitializeAsync()
    {
        await LoadPersistedStateAsync();
        await LoadFuelLocationsAsync();
        ScheduleReload();
    }

    private async Task LoadPersistedStateAsync()
    {
        if (_settings is null)
        {
            return;
        }

        try
        {
            await _settings.LoadSettingsAsync();
            var stamp = await _settings.GetSettingAsync(UserSettingsKeys.FuelLastReconciliationUtc, string.Empty);
            if (DateTime.TryParse(
                    stamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var last))
            {
                LastReconciliationDate = last.Kind == DateTimeKind.Utc ? last.ToLocalTime() : last;
            }

            var bulk = await _settings.GetSettingAsync(UserSettingsKeys.FuelLastBulkStationGallons, string.Empty);
            if (!string.IsNullOrWhiteSpace(bulk))
            {
                _updatingBulkText = true;
                BulkStationGallonsText = bulk;
                _updatingBulkText = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not load persisted fuel reconciliation settings");
        }
    }

    private async Task LoadFuelLocationsAsync()
    {
        try
        {
            var selected = SelectedLocation;
            _suppressReload = true;
            FuelLocations.Clear();
            FuelLocations.Add("All Locations");

            if (_locationCatalog != null)
            {
                foreach (var location in await _locationCatalog.GetLocationsAsync())
                {
                    FuelLocations.Add(location);
                }
            }
            else
            {
                foreach (var seed in FuelLocationCatalog.SeedDefaults)
                {
                    FuelLocations.Add(seed);
                }

                foreach (var fromDb in await _fuelService.GetDistinctFuelLocationsAsync())
                {
                    if (!FuelLocations.Any(l => string.Equals(l, fromDb, StringComparison.OrdinalIgnoreCase)))
                    {
                        FuelLocations.Add(fromDb);
                    }
                }
            }

            SelectedLocation = FuelLocations.Contains(selected) ? selected : "All Locations";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load fuel locations for reconciliation filter");
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private void ScheduleReload()
    {
        if (_suppressReload)
        {
            return;
        }

        _ = ReloadDebouncedAsync();
    }

    private async Task ReloadDebouncedAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        var version = Interlocked.Increment(ref _loadVersion);

        try
        {
            await Task.Delay(250, token);
            await LoadReconciliationDataAsync(version, token);
        }
        catch (OperationCanceledException)
        {
            // newer filter change
        }
    }

    private async Task LoadReconciliationDataAsync(int version, CancellationToken token)
    {
        try
        {
            IsLoading = true;

            if (EndDate.Date < StartDate.Date)
            {
                ReconciliationSummary = "End date must be on or after the start date.";
                return;
            }

            var records = (await _fuelService.GetFuelRecordsByDateRangeAsync(StartDate, EndDate)).ToList();
            token.ThrowIfCancellationRequested();

            if (!string.Equals(SelectedLocation, "All Locations", StringComparison.Ordinal))
            {
                records = records
                    .Where(r => string.Equals(r.FuelLocation, SelectedLocation, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var buses = await _busService.GetAllBusesAsync();
            token.ThrowIfCancellationRequested();
            if (version != _loadVersion)
            {
                return;
            }

            var busLookup = buses.ToDictionary(b => b.BusId, b => b.BusNumber);
            var bulk = TryParseBulk(BulkStationGallonsText, out var gallons) ? gallons : (decimal?)null;
            var snapshot = FuelReconciliationCalculator.Build(records, bulk, busLookup);
            ApplySnapshot(snapshot, records.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading reconciliation data");
            UserToast.Error($"Could not load reconciliation data: {ex.Message}", "Reconciliation");
            ReconciliationSummary = "Error loading reconciliation data. Please try again.";
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private void ApplySnapshot(FuelReconciliationSnapshot snapshot, int recordCount)
    {
        DailyReconciliation.Clear();
        foreach (var day in snapshot.Daily)
        {
            DailyReconciliation.Add(new DailyReconciliationItem
            {
                Date = day.Date,
                VehicleUsageGallons = (double)day.VehicleGallons,
                FillCount = day.FillCount
            });
        }

        DiscrepancyDetails.Clear();
        foreach (var detail in snapshot.Details)
        {
            DiscrepancyDetails.Add(new DiscrepancyDetailItem
            {
                Date = detail.Date,
                BusNumber = detail.BusNumber,
                Driver = "—",
                GallonsReported = detail.GallonsReported,
                OdometerReading = detail.OdometerReading,
                DiscrepancyType = detail.DiscrepancyType,
                PotentialIssue = detail.PotentialIssue,
                RecommendedAction = detail.RecommendedAction
            });
        }

        HasBulkReading = snapshot.HasBulkReading;
        BulkStationGallons = snapshot.HasBulkReading ? (double)(snapshot.BulkStationGallons ?? 0m) : 0;
        VehicleUsageGallons = (double)snapshot.VehicleUsageGallons;
        DiscrepancyGallons = (double)snapshot.DiscrepancyGallons;
        DiscrepancyPercentage = snapshot.DiscrepancyRatio;
        HasDailyChartData = DailyReconciliation.Count > 0;
        UpdateDiscrepancyColors();
        RememberBulkCommand.NotifyCanExecuteChanged();

        if (recordCount == 0)
        {
            ReconciliationSummary = "No fuel data available for the selected period and location.";
        }
        else if (!snapshot.HasBulkReading)
        {
            ReconciliationSummary =
                $"{recordCount} vehicle fill-up(s) from {StartDate:d} to {EndDate:d} total {VehicleUsageGallons:N2} gallons. " +
                "Enter the bulk station meter gallons for this period to compute a real discrepancy.";
        }
        else
        {
            var tone = Math.Abs(DiscrepancyPercentage) > 0.05
                ? "This discrepancy requires investigation."
                : "This is within a typical 5% tolerance.";
            ReconciliationSummary =
                $"Analysis of {recordCount} fuel records from {StartDate:d} to {EndDate:d} shows a " +
                $"{Math.Abs(DiscrepancyPercentage):P2} {(DiscrepancyGallons >= 0 ? "bulk surplus" : "vehicle surplus")} " +
                $"versus the station meter. {tone}";
        }
    }

    private void UpdateDiscrepancyColors()
    {
        if (!HasBulkReading)
        {
            ApplyNeutralDiscrepancyColors();
            return;
        }

        var abs = Math.Abs(DiscrepancyPercentage);
        if (abs <= 0.02)
        {
            DiscrepancyBackground = ResolveBrush("BusBuddy.Brush.Panel.Header", Brushes.Transparent);
            DiscrepancyBorder = ResolveBrush("BusBuddy.Brush.Semantic.Success", Brushes.Green);
            DiscrepancyForeground = ResolveBrush("BusBuddy.Brush.Semantic.Success", Brushes.Green);
        }
        else if (abs <= 0.05)
        {
            DiscrepancyBackground = ResolveBrush("BusBuddy.Brush.Panel.Content", Brushes.Transparent);
            DiscrepancyBorder = ResolveBrush("BusBuddy.Brush.Semantic.Warning", Brushes.Orange);
            DiscrepancyForeground = ResolveBrush("BusBuddy.Brush.Semantic.Warning", Brushes.DarkOrange);
        }
        else
        {
            DiscrepancyBackground = ResolveBrush("BusBuddy.Brush.Panel.Content", Brushes.Transparent);
            DiscrepancyBorder = ResolveBrush("BusBuddy.Brush.Semantic.Error", Brushes.Red);
            DiscrepancyForeground = ResolveBrush("BusBuddy.Brush.Semantic.Error", Brushes.Red);
        }
    }

    private void ApplyNeutralDiscrepancyColors()
    {
        DiscrepancyBackground = ResolveBrush("BusBuddy.Brush.Panel.Header", Brushes.Transparent);
        DiscrepancyBorder = ResolveBrush("BusBuddy.Brush.Panel.Border", Brushes.Gray);
        DiscrepancyForeground = ResolveBrush("BusBuddy.Brush.Text.Secondary", Brushes.Gray);
    }

    private static Brush ResolveBrush(string key, Brush fallback)
    {
        try
        {
            if (Application.Current?.TryFindResource(key) is Brush brush)
            {
                return brush;
            }
        }
        catch
        {
            // design-time / headless
        }

        return fallback;
    }

    private static bool TryParseBulk(string? text, out decimal gallons)
    {
        gallons = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, ParseCulture, out gallons) &&
            !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out gallons))
        {
            return false;
        }

        return gallons >= 0;
    }

    private bool CanRememberBulk() => TryParseBulk(BulkStationGallonsText, out _);

    private async Task RememberBulkReadingAsync()
    {
        if (_settings is null || !TryParseBulk(BulkStationGallonsText, out var gallons))
        {
            UserToast.Warning("Enter a valid bulk station gallon reading first.", "Bulk meter");
            return;
        }

        await _settings.SetSettingAsync(
            UserSettingsKeys.FuelLastBulkStationGallons,
            gallons.ToString("0.###", CultureInfo.InvariantCulture));
        await _settings.SetSettingAsync(
            UserSettingsKeys.FuelLastReconciliationUtc,
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await _settings.SaveSettingsAsync();

        LastReconciliationDate = DateTime.Now;
        UserToast.Success("Bulk meter reading saved for the next reconciliation session.", "Saved");
    }

    private async Task ExportAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Fuel_Reconciliation_{StartDate:yyyyMMdd}_to_{EndDate:yyyyMMdd}",
                DefaultExt = ".csv",
                Filter = "CSV Files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                StatusMessage = "Export cancelled";
                return;
            }

            await using var writer = new System.IO.StreamWriter(dialog.FileName);
            await writer.WriteLineAsync("Fuel Reconciliation Report");
            await writer.WriteLineAsync($"Period: {StartDate:d} to {EndDate:d}");
            await writer.WriteLineAsync($"Location: {SelectedLocation}");
            await writer.WriteLineAsync($"Generated: {DateTime.Now}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync(
                HasBulkReading
                    ? $"Bulk Station Total: {BulkStationGallons:N2} gallons (clerk-entered)"
                    : "Bulk Station Total: (not entered)");
            await writer.WriteLineAsync($"Vehicle Usage Total: {VehicleUsageGallons:N2} gallons");
            if (HasBulkReading)
            {
                await writer.WriteLineAsync($"Discrepancy: {DiscrepancyGallons:N2} gallons ({DiscrepancyPercentage:P2})");
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("Daily Vehicle Usage");
            await writer.WriteLineAsync("Date,Vehicle Usage Gallons,Fill Count");
            foreach (var day in DailyReconciliation)
            {
                await writer.WriteLineAsync($"{day.Date:yyyy-MM-dd},{day.VehicleUsageGallons:N2},{day.FillCount}");
            }

            if (DiscrepancyDetails.Count > 0)
            {
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Fill-ups to verify");
                await writer.WriteLineAsync(
                    "Date,Bus Number,Gallons Reported,Odometer,Discrepancy Type,Potential Issue,Recommended Action");
                foreach (var detail in DiscrepancyDetails)
                {
                    await writer.WriteLineAsync(
                        $"{detail.Date:yyyy-MM-dd}," +
                        $"\"{detail.BusNumber}\"," +
                        $"{detail.GallonsReported:N2}," +
                        $"{detail.OdometerReading}," +
                        $"\"{detail.DiscrepancyType}\"," +
                        $"\"{detail.PotentialIssue}\"," +
                        $"\"{detail.RecommendedAction}\"");
                }
            }

            if (_settings != null)
            {
                if (TryParseBulk(BulkStationGallonsText, out var gallons))
                {
                    await _settings.SetSettingAsync(
                        UserSettingsKeys.FuelLastBulkStationGallons,
                        gallons.ToString("0.###", CultureInfo.InvariantCulture));
                }

                await _settings.SetSettingAsync(
                    UserSettingsKeys.FuelLastReconciliationUtc,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await _settings.SaveSettingsAsync();
                LastReconciliationDate = DateTime.Now;
            }

            StatusMessage = $"Exported reconciliation to {dialog.FileName}";
            UserToast.Success("Reconciliation report exported.", "Export successful");

            using (LogContext.PushProperty("ViewModelType", nameof(FuelReconciliationViewModel)))
            {
                Logger.Information("Exported fuel reconciliation report");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting reconciliation report");
            UserToast.Error($"Error exporting report: {ex.Message}", "Export Error");
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        GC.SuppressFinalize(this);
    }
}

public class DailyReconciliationItem
{
    public DateTime Date { get; set; }
    public double VehicleUsageGallons { get; set; }
    public int FillCount { get; set; }
}

public class DiscrepancyDetailItem
{
    public DateTime Date { get; set; }
    public string BusNumber { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public double GallonsReported { get; set; }
    public int OdometerReading { get; set; }
    public string DiscrepancyType { get; set; } = string.Empty;
    public string PotentialIssue { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
}
