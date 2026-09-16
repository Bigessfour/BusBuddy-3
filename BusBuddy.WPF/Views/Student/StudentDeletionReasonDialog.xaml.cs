using BusBuddy.Core.Models;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

/// <summary>
/// Clerk must pick Mistake, Moved, or Not attending before a student row is removed.
/// </summary>
public partial class StudentDeletionReasonDialog : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<StudentDeletionReasonDialog>();
    private readonly StudentDeletionReasonDialogViewModel _vm;

    public StudentDeletionReasonDialog(StudentDeletionReasonDialogViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;
        _vm.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
        Logger.Information("Student deletion reason dialog opened StudentId={StudentId}", _vm.StudentId);
    }

    public StudentDeletionRequest? Result => _vm.Result;
}
