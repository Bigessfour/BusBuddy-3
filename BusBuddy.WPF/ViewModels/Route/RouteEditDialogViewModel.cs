using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.WPF.Commands;

namespace BusBuddy.WPF.ViewModels.Route
{
public class RouteEditDialogViewModel : INotifyPropertyChanged
{
    private string _startLocation = string.Empty;
    public string StartLocation
    {
        get => _startLocation;
        set { if (_startLocation != value) { _startLocation = value; OnPropertyChanged(); } }
    }

    private string _endLocation = string.Empty;
    public string EndLocation
    {
        get => _endLocation;
        set { if (_endLocation != value) { _endLocation = value; OnPropertyChanged(); } }
    }

    private string _stops = string.Empty; // comma-separated
    public string Stops
    {
        get => _stops;
        set { if (_stops != value) { _stops = value; OnPropertyChanged(); } }
    }

    public ICommand SaveCommand { get; }

    public RouteEditDialogViewModel()
    {
        // Use parameterless RelayCommand; dialog closed externally after binding
        SaveCommand = new RelayCommand(() => { /* Validation stub */ });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
}
