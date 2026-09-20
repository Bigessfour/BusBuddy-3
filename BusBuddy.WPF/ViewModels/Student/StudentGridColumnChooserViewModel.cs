using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Clerk add/hide list for Students SfDataGrid columns (Syncfusion GridColumn.IsHidden).
/// </summary>
public sealed class StudentGridColumnChooserViewModel : ObservableObject
{
    public StudentGridColumnChooserViewModel(IEnumerable<StudentGridColumnChoice> columns)
    {
        Columns = new ObservableCollection<StudentGridColumnChoice>(
            columns ?? throw new ArgumentNullException(nameof(columns)));
        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
    }

    public ObservableCollection<StudentGridColumnChoice> Columns { get; }

    public IRelayCommand ApplyCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public bool Applied { get; private set; }

    public event EventHandler<bool>? RequestClose;

    private void Apply()
    {
        Applied = true;
        RequestClose?.Invoke(this, true);
    }

    private void Cancel()
    {
        Applied = false;
        RequestClose?.Invoke(this, false);
    }
}

public sealed class StudentGridColumnChoice : ObservableObject
{
    private bool _isVisible;

    public StudentGridColumnChoice(string key, string header, bool isVisible, bool canHide)
    {
        Key = key;
        Header = header;
        CanHide = canHide;
        _isVisible = isVisible;
    }

    public string Key { get; }

    public string Header { get; }

    public bool CanHide { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
