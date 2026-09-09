using System.Windows;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class StudentSchoolTransferForm : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<StudentSchoolTransferForm>();

    public StudentSchoolTransferForm(StudentSchoolTransferViewModel viewModel)
    {
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = viewModel;
        Logger.Information("StudentSchoolTransferForm opened Title={Title}", viewModel.Title);
        viewModel.RequestClose += (_, result) =>
        {
            Logger.Information("StudentSchoolTransferForm closing DialogResult={Result}", result);
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
