using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Driver;

/// <summary>Editable CDE training checklist grid for one driver.</summary>
public sealed class DriverTrainingChecklistViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<DriverTrainingChecklistViewModel>();
    private readonly IDriverTrainingService _trainingService;
    private readonly int _driverId;
    private readonly string _driverName;
    private DriverTrainingRecord? _selectedRecord;

    public event EventHandler? Closed;

    public DriverTrainingChecklistViewModel(int driverId, string driverName, IDriverTrainingService trainingService)
    {
        _driverId = driverId;
        _driverName = driverName;
        _trainingService = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
        Records = new ObservableCollection<DriverTrainingRecord>();
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        MarkCompleteCommand = new AsyncRelayCommand(MarkCompleteAsync, () => SelectedRecord is not null);
        SaveRowCommand = new AsyncRelayCommand(SaveSelectedAsync, () => SelectedRecord is not null);
        IncludeOptionalCommand = new AsyncRelayCommand(() => LoadAsync(includeOptional: true));
        CloseCommand = new RelayCommand(() => Closed?.Invoke(this, EventArgs.Empty));
        _ = LoadAsync();
    }

    public string Title => $"CDE Training — {_driverName}";

    public ObservableCollection<DriverTrainingRecord> Records { get; }

    public DriverTrainingRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                (MarkCompleteCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (SaveRowCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand MarkCompleteCommand { get; }
    public ICommand SaveRowCommand { get; }
    public ICommand IncludeOptionalCommand { get; }
    public ICommand CloseCommand { get; }

    private async Task LoadAsync() => await LoadAsync(includeOptional: false);

    private async Task LoadAsync(bool includeOptional)
    {
        try
        {
            IsLoading = true;
            var list = await _trainingService.EnsureMatrixChecklistAsync(_driverId, includeOptional);
            await _trainingService.RefreshTrainingCompleteFlagAsync(_driverId);
            Records.Clear();
            foreach (var row in list)
            {
                Records.Add(row);
            }

            var required = list.Count(r => r.IsRequired && r.IsApplicable);
            var complete = list.Count(r => r.IsRequired && r.IsApplicable && r.IsComplete && !r.IsExpired);
            StatusMessage = $"{complete}/{required} required current";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed loading training checklist DriverId={DriverId}", _driverId);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task MarkCompleteAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        try
        {
            await _trainingService.UpsertCompletionAsync(
                _driverId,
                SelectedRecord.RequirementCode,
                DateTime.Today,
                SelectedRecord.ExpiryDate,
                SelectedRecord.CertificateOrReference,
                SelectedRecord.Notes);
            await LoadAsync();
            StatusMessage = $"Marked {SelectedRecord.RequirementName} complete";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Mark complete failed");
            StatusMessage = ex.Message;
        }
    }

    private async Task SaveSelectedAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        try
        {
            var completed = SelectedRecord.CompletedDate ?? DateTime.Today;
            await _trainingService.UpsertCompletionAsync(
                _driverId,
                SelectedRecord.RequirementCode,
                completed,
                SelectedRecord.ExpiryDate,
                SelectedRecord.CertificateOrReference,
                SelectedRecord.Notes);
            await LoadAsync();
            StatusMessage = "Row saved";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Save training row failed");
            StatusMessage = ex.Message;
        }
    }
}
