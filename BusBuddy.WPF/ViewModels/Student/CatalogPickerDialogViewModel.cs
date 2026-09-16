using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Non-generic host for the picker window. XAML cannot close over <c>T</c>.</summary>
public abstract class CatalogPickerDialogViewModel : ObservableObject
{
    protected CatalogPickerDialogViewModel(string prompt, string title)
    {
        Prompt = prompt ?? string.Empty;
        Title = title ?? string.Empty;
        ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
    }

    public string Prompt { get; }

    public string Title { get; }

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event EventHandler<bool>? RequestClose;

    public abstract int ItemCount { get; }

    protected abstract bool CanConfirm();

    protected abstract void Confirm();

    protected void Close(bool result) => RequestClose?.Invoke(this, result);
}

/// <summary>Picks one catalog row (school or pickup stop) before edit or retire. Binds <c>Name</c>.</summary>
public sealed partial class CatalogPickerDialogViewModel<T> : CatalogPickerDialogViewModel
    where T : class
{
    [ObservableProperty]
    private T? selectedItem;

    public CatalogPickerDialogViewModel(IReadOnlyList<T> items, string prompt, string title)
        : base(prompt, title)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<T> Items { get; }

    public override int ItemCount => Items.Count;

    protected override bool CanConfirm() => SelectedItem is not null;

    protected override void Confirm()
    {
        if (SelectedItem is null)
        {
            return;
        }

        Close(true);
    }

    partial void OnSelectedItemChanged(T? value) => ConfirmCommand.NotifyCanExecuteChanged();
}
