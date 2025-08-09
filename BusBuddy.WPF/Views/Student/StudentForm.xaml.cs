using System.Windows;
using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion WPF docs
using Syncfusion.SfSkinManager; // SfSkinManager per official docs
using BusBuddy.WPF.ViewModels.Student;

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// Interaction logic for StudentForm.xaml
    /// </summary>
    /// <summary>
    /// StudentForm — Syncfusion ChromelessWindow for student entry/edit.
    /// Applies FluentDark theme by default, falls back to FluentLight if needed.
    /// All controls are styled for accessibility and data type clarity.
    /// ViewModel handles all data and validation logic.
    /// </summary>
    public partial class StudentForm : ChromelessWindow
    {
        public StudentFormViewModel ViewModel { get; private set; }

    /// <summary>
    /// Default constructor: initializes theming, ViewModel, and event hooks.
    /// </summary>
    public StudentForm()
        {
            InitializeComponent();
            // Apply Syncfusion theme — FluentDark default, FluentLight fallback
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            try
            {
                // Try FluentDark theme first (preferred for accessibility)
                using var dark = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, dark);
            }
            catch
            {
                // Fallback to FluentLight if FluentDark is unavailable
                using var light = new Theme("FluentLight");
                SfSkinManager.SetTheme(this, light);
            }
            ViewModel = new StudentFormViewModel();
            DataContext = ViewModel;

            // Subscribe to ViewModel events for form closure
            // (Allows ViewModel to close dialog on save/cancel)
            ViewModel.RequestClose += OnRequestClose;
        }

    /// <summary>
    /// Overload: initializes with an existing student for editing.
    /// </summary>
    public StudentForm(Core.Models.Student student) : this()
        {
            ViewModel = new StudentFormViewModel(student);
            DataContext = ViewModel;
            ViewModel.RequestClose += OnRequestClose;
        }

    /// <summary>
    /// Handles ViewModel RequestClose event to close dialog with result.
    /// </summary>
    private void OnRequestClose(object? sender, bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

    /// <summary>
    /// Cleanup: Unsubscribes events, disposes ViewModel, and releases SkinManager resources.
    /// </summary>
    protected override void OnClosed(System.EventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks
            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnRequestClose;
                ViewModel.Dispose();
            }
            // Clear SkinManager instances for this window per docs
            try { SfSkinManager.Dispose(this); } catch { }
            base.OnClosed(e);
        }
    }
}
