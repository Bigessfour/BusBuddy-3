using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

/// <summary>
/// Clerk must pick Mistake, Moved, or Not attending before a student row is removed.
/// Size is session-memory: drag-resize once and the next open uses that size.
/// </summary>
public partial class StudentDeletionReasonDialog : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<StudentDeletionReasonDialog>();
    private readonly StudentDeletionReasonDialogViewModel _vm;
    private bool _rememberSize;

    public StudentDeletionReasonDialog(StudentDeletionReasonDialogViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;
        if (ClerkSessionLayout.TryGetDeletionSize(out var width, out var height))
        {
            Width = width;
            Height = height;
        }

        _vm.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
        Loaded += (_, _) => _rememberSize = true;
        Logger.Information("Student deletion reason dialog opened StudentId={StudentId}", _vm.StudentId);
    }

    public StudentDeletionRequest? Result => _vm.Result;

    private void DeletionDialog_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_rememberSize || !IsLoaded)
        {
            return;
        }

        ClerkSessionLayout.RememberDeletionSize(ActualWidth, ActualHeight);
    }
}
