using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion docs
using Syncfusion.SfSkinManager; // For Syncfusion theming
// ...existing code...
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.WPF.Utilities; // SyncfusionThemeManager
using Serilog; // Logging per project standards

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
        private static readonly ILogger Logger = Log.ForContext<StudentsView>();
        public StudentsView()
        {
            InitializeComponent();

            // Apply Syncfusion theme via central manager (FluentDark with FluentLight fallback)
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);

            // Set the ViewModel for data binding
            DataContext = new StudentsViewModel();

            // Global button click diagnostics for this window
            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                Logger.Information("StudentsView: global button click diagnostics attached");
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: failed to attach button diagnostics");
            }

            Logger.Information("StudentsView initialized");
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // Dispose skin manager resources for this window
            try { SfSkinManager.Dispose(this); } catch { /* no-op */ }
            base.OnClosed(e);
        }

        private void OnAnyButtonClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                string name = fe?.Name ?? "(unnamed)";
                string type = src?.GetType().Name ?? "(unknown)";

                if (src is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    bool? canExec = null;
                    try { if (badv.Command != null) canExec = badv.Command.CanExecute(badv.CommandParameter); } catch { }
                    Logger.Information("StudentsView Button: {Type} Name={Name} Label={Label} CanExecute={CanExecute}", type, name, badv.Label, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null;
                    try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    Logger.Information("StudentsView Button: {Type} Name={Name} Content={Content} CanExecute={CanExecute}", type, name, btn.Content, canExec);
                }
                else
                {
                    Logger.Information("StudentsView Button: {Type} Name={Name}", type, name);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: button click logging failed");
            }
        }
    }
}
