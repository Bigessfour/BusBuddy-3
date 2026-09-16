using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class SchoolCatalogPickerDialog : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<SchoolCatalogPickerDialog>();

    public SchoolCatalogPickerDialog(SchoolCatalogPickerDialogViewModel viewModel)
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
        Logger.Information("School catalog picker opened Count={Count}", viewModel.Schools.Count);
    }
}
