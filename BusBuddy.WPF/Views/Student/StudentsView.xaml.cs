using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion docs
using Syncfusion.SfSkinManager; // For Syncfusion theming
// ...existing code...
using BusBuddy.WPF.ViewModels.Student;

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// Interaction logic for StudentsView.xaml
    /// Student management view with Syncfusion SfDataGrid for listing and managing students
    /// Enhanced for route building with AI optimization and mapping integration
    /// </summary>
    /// <summary>
    /// StudentsView — Student management view with Syncfusion theming (FluentDark/FluentLight fallback)
    /// </summary>
    public partial class StudentsView : ChromelessWindow
    {
        public StudentsView()
        {
            InitializeComponent();

            // Apply Syncfusion theme — FluentDark default, FluentWhite fallback
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            try
            {
                // Try FluentDark theme first
                using var dark = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, dark);
            }
            catch
            {
                // Fallback to FluentWhite if FluentDark is unavailable
                using var white = new Theme("FluentWhite");
                SfSkinManager.SetTheme(this, white);
            }

            // Set the ViewModel for data binding
            DataContext = new StudentsViewModel();
        }
    }
}
