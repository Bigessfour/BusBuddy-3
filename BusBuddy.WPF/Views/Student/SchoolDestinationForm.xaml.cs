using System.Windows;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class SchoolDestinationForm : ChromelessWindow
{
    public SchoolDestinationForm(SchoolDestinationFormViewModel viewModel)
    {
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = viewModel;
        viewModel.RequestClose += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
    }

    protected override void OnClosed(System.EventArgs e)
    {
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
