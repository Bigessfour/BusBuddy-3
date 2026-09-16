using System.Windows.Input;
using BusBuddy.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Clerk picks Mistake, Moved, or Not attending before a student row is removed.
/// There is no default reason — Confirm stays disabled until one is selected.
/// </summary>
public partial class StudentDeletionReasonDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private StudentDeletionReasonOption? selectedReason;

    [ObservableProperty]
    private string notes = string.Empty;

    public StudentDeletionReasonDialogViewModel(StudentModel student)
    {
        ArgumentNullException.ThrowIfNull(student);
        StudentId = student.StudentId;
        Prompt = $"Delete {student.StudentName} from the roster? This cannot be undone.";
        Warning = student.Active
            ? "This student is currently active. Archive instead if they may return this year."
            : string.Empty;
        ConfirmCommand = new RelayCommand(Confirm, () => SelectedReason is not null);
        CancelCommand = new RelayCommand(Cancel);
    }

    public int StudentId { get; }

    public string Prompt { get; }

    public string Warning { get; }

    public IReadOnlyList<StudentDeletionReasonOption> Reasons { get; } = StudentDeletionReasonOption.All;

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public StudentDeletionRequest? Result { get; private set; }

    public event EventHandler<bool>? RequestClose;

    partial void OnSelectedReasonChanged(StudentDeletionReasonOption? value) =>
        ConfirmCommand.NotifyCanExecuteChanged();

    private void Confirm()
    {
        if (SelectedReason is null)
        {
            return;
        }

        Result = new StudentDeletionRequest(SelectedReason.Reason, Notes);
        RequestClose?.Invoke(this, true);
    }

    private void Cancel()
    {
        Result = null;
        RequestClose?.Invoke(this, false);
    }
}
