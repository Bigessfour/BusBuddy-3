using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

/// <summary>
/// Clerk checkbox list over the Students SfDataGrid columns (Syncfusion IsHidden).
/// Official drag-drop ColumnChooser is the same IsHidden seam:
/// https://help.syncfusion.com/wpf/datagrid/interactive-features
/// </summary>
public partial class StudentGridColumnChooserDialog : ChromelessWindow
{
    public StudentGridColumnChooserDialog(StudentGridColumnChooserViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = viewModel;
        viewModel.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
    }
}
