using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Driver;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Driver;

public partial class DriverTrainingChecklistView : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<DriverTrainingChecklistView>();

    public DriverTrainingChecklistView(DriverTrainingChecklistViewModel viewModel)
    {
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = viewModel;
        Logger.Information("DriverTrainingChecklistView opened Title={Title}", viewModel.Title);
        viewModel.Closed += (_, _) => Close();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
