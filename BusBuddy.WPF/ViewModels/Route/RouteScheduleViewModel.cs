using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.Core.Services;
using CoreModels = BusBuddy.Core.Models;
using BusBuddy.WPF.Commands;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Interactive clerk view of published stop clocks for one route row.
/// Times come from <see cref="RouteSummarySheetBuilder"/> — not
    /// <see cref="CoreModels.Route.EstimatedDuration"/> and not the district driver calendar.
/// </summary>
public sealed class RouteScheduleViewModel : INotifyPropertyChanged
{
    public const string EmptyStopsHint = "Time or add stops first";
    public const string EmDash = "—";

    private readonly Func<Task<RouteSummarySheet?>>? _reTimeAsync;
    private readonly Action? _print;
    private readonly Func<bool>? _confirmOverwriteClocks;
    private RouteSummarySheet _sheet;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public RouteScheduleViewModel(
        RouteSummarySheet sheet,
        Func<Task<RouteSummarySheet?>>? reTimeAsync = null,
        Action? print = null,
        Func<bool>? confirmOverwriteClocks = null)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _reTimeAsync = reTimeAsync;
        _print = print;
        _confirmOverwriteClocks = confirmOverwriteClocks;
        ReTimeCommand = new RelayCommand(async () => await ReTimeAsync(), () => CanReTime);
        PrintCommand = new RelayCommand(Print, () => CanPrint);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        if (HasEmptyStops)
        {
            StatusMessage = EmptyStopsHint;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CloseRequested;

    public ICommand ReTimeCommand { get; }

    public ICommand PrintCommand { get; }

    public ICommand CloseCommand { get; }

    public RouteSummarySheet Sheet
    {
        get => _sheet;
        private set
        {
            _sheet = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEmptyStops));
            OnPropertyChanged(nameof(HasGenerateStopsOnlyNote));
            OnPropertyChanged(nameof(HasPublishedClocks));
            OnPropertyChanged(nameof(FirstLastClockText));
            (ReTimeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            (ReTimeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PrintCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool HasEmptyStops => Sheet.Stops.Count == 0;

    public bool HasGenerateStopsOnlyNote => !string.IsNullOrWhiteSpace(Sheet.GenerateStopsOnlyNote);

    public bool HasPublishedClocks =>
        Sheet.Stops.Any(s => s.Arrival != EmDash || s.Departure != EmDash);

    public string FirstLastClockText =>
        $"{Sheet.DepartureText} → {Sheet.ArrivalText}";

    public bool CanReTime => !IsBusy && !HasEmptyStops && _reTimeAsync != null;

    public bool CanPrint => !IsBusy && _print != null;

    /// <summary>
    /// Builds the window VM from the selected assignment row. Returns false when
    /// no route is selected so the caller can show an info dialog and skip the window.
    /// </summary>
    public static bool TryCreate(
        CoreModels.Route? route,
        IEnumerable<CoreModels.RouteStop>? stops,
        IEnumerable<CoreModels.Student>? students,
        CoreModels.Bus? bus,
        CoreModels.Driver? driver,
        CoreModels.RouteTimeSlot timeSlot,
        out RouteScheduleViewModel? viewModel,
        Func<Task<RouteSummarySheet?>>? reTimeAsync = null,
        Action? print = null,
        Func<bool>? confirmOverwriteClocks = null,
        IReadOnlySet<int>? notRidingStudentIds = null)
    {
        if (route is null)
        {
            viewModel = null;
            return false;
        }

        var sheet = RouteSummarySheetBuilder.Build(
            route,
            stops,
            students,
            bus,
            driver,
            timeSlot,
            notRidingStudentIds: notRidingStudentIds);
        viewModel = new RouteScheduleViewModel(sheet, reTimeAsync, print, confirmOverwriteClocks);
        return true;
    }

    private async Task ReTimeAsync()
    {
        if (!CanReTime || _reTimeAsync is null)
        {
            return;
        }

        if (HasPublishedClocks)
        {
            var overwrite = _confirmOverwriteClocks?.Invoke() ?? false;
            if (!overwrite)
            {
                StatusMessage = "Published times left unchanged";
                return;
            }
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Calculating stop times...";
            var next = await _reTimeAsync();
            if (next is null)
            {
                StatusMessage = "Re-time did not update clocks";
                return;
            }

            Sheet = next;
            StatusMessage = HasEmptyStops
                ? EmptyStopsHint
                : $"Times {Sheet.DepartureText} → {Sheet.ArrivalText}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Print()
    {
        if (!CanPrint)
        {
            return;
        }

        _print?.Invoke();
        StatusMessage = "Printed PdfGrid route sheet";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
