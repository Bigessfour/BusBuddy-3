using System.Windows.Input;
using BusBuddy.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Lets the clerk pick a cataloged school before edit or delete.</summary>
public partial class SchoolCatalogPickerDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private Destination? selectedSchool;

    public SchoolCatalogPickerDialogViewModel(IReadOnlyList<Destination> schools, string prompt)
    {
        Schools = schools ?? throw new ArgumentNullException(nameof(schools));
        Prompt = prompt;
        ConfirmCommand = new RelayCommand(Confirm, () => SelectedSchool is not null);
        CancelCommand = new RelayCommand(Cancel);
    }

    public IReadOnlyList<Destination> Schools { get; }

    public string Prompt { get; }

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event EventHandler<bool>? RequestClose;

    partial void OnSelectedSchoolChanged(Destination? value) => ConfirmCommand.NotifyCanExecuteChanged();

    private void Confirm()
    {
        if (SelectedSchool is null)
        {
            return;
        }

        RequestClose?.Invoke(this, true);
    }

    private void Cancel() => RequestClose?.Invoke(this, false);
}
