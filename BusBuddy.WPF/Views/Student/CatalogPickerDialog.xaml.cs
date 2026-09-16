using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using Serilog;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class CatalogPickerDialog : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<CatalogPickerDialog>();

    public CatalogPickerDialog(CatalogPickerDialogViewModel viewModel)
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
        Logger.Information("Catalog picker opened Title={Title} Count={Count}", viewModel.Title, viewModel.ItemCount);
    }
}
