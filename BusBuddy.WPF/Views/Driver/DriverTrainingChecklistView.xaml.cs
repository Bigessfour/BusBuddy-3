using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Driver;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Driver;

public partial class DriverTrainingChecklistView : ChromelessWindow
{
    public DriverTrainingChecklistView(DriverTrainingChecklistViewModel viewModel)
    {
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = viewModel;
        viewModel.Closed += (_, _) => Close();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
