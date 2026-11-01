using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.WPF.Commands;
using CoreModels = BusBuddy.Core.Models;

namespace BusBuddy.WPF.ViewModels.Route;

/// <summary>
/// Clerk picks a dated spare bus / substitute driver for one published route session (#113).
/// Does not write <c>Route.AM*</c>/<c>PM*</c>.
/// </summary>
public sealed class RouteDatedSubstituteViewModel : INotifyPropertyChanged
{
    private CoreModels.Bus? _selectedBus;
    private CoreModels.Driver? _selectedDriver;
    private string _statusMessage = string.Empty;
    private bool _dialogResult;

    public RouteDatedSubstituteViewModel(
        string routeDisplayName,
        DateTime serviceDate,
        IEnumerable<CoreModels.Bus> buses,
        IEnumerable<CoreModels.Driver> drivers,
        CoreModels.Bus? currentBus,
        CoreModels.Driver? currentDriver)
    {
        RouteDisplayName = routeDisplayName;
        ServiceDateText = serviceDate.ToString("yyyy-MM-dd");
        Buses = new ObservableCollection<CoreModels.Bus>(buses);
        Drivers = new ObservableCollection<CoreModels.Driver>(drivers);
        SelectedBus = currentBus ?? Buses.FirstOrDefault();
        SelectedDriver = currentDriver ?? Drivers.FirstOrDefault();
        SaveCommand = new RelayCommand(Save, () => CanSave);
        CancelCommand = new RelayCommand(() =>
        {
            _dialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CloseRequested;

    public string RouteDisplayName { get; }

    public string ServiceDateText { get; }

    public ObservableCollection<CoreModels.Bus> Buses { get; }

    public ObservableCollection<CoreModels.Driver> Drivers { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public bool DialogResult => _dialogResult;

    public CoreModels.Bus? SelectedBus
    {
        get => _selectedBus;
        set
        {
            _selectedBus = value;
            OnPropertyChanged();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public CoreModels.Driver? SelectedDriver
    {
        get => _selectedDriver;
        set
        {
            _selectedDriver = value;
            OnPropertyChanged();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    public bool CanSave => SelectedBus is not null && SelectedDriver is not null;

    private void Save()
    {
        if (!CanSave)
        {
            StatusMessage = "Pick both a bus and a driver.";
            return;
        }

        _dialogResult = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
