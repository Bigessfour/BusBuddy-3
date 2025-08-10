using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion docs
using Syncfusion.SfSkinManager; // For Syncfusion theming
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.WPF.Utilities; // SyncfusionThemeManager
using Serilog; // Logging per project standards
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics; // Conditional/DEBUG — https://learn.microsoft.com/dotnet/api/system.diagnostics.conditionalattribute

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// StudentsView — Student management view using Syncfusion ChromelessWindow with theming.
    /// Docs:
    /// - Syncfusion ChromelessWindow: https://help.syncfusion.com/wpf/chromelesswindow/overview
    /// - Syncfusion SfSkinManager: https://help.syncfusion.com/wpf/themes/sfskinmanager
    /// - WPF RenderOptions/TextOptions: https://learn.microsoft.com/dotnet/api/system.windows.media.renderoptions
    /// - WPF DPI handling (OnDpiChanged): https://learn.microsoft.com/dotnet/api/system.windows.window.ondpichanged
    /// </summary>
    public partial class StudentsView : ChromelessWindow
    {
        private static readonly ILogger Logger = Log.ForContext<StudentsView>();
        public StudentsView()
        {
            InitializeComponent();

            // Apply Syncfusion theme via central manager (FluentDark with FluentLight fallback)
            // Docs: SfSkinManager — https://help.syncfusion.com/wpf/themes/sfskinmanager
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);

            // Set the ViewModel for data binding (prefer DI so DbContext/connection align with saves)
            try
            {
                var sp = App.ServiceProvider;
                var vm = sp?.GetService<StudentsViewModel>();
                DataContext = vm ?? new StudentsViewModel();
            }
            catch (System.Exception ex)
            {
                // Serilog exception logging per project standards
                Logger.Warning(ex, "StudentsView: DI resolve failed — falling back to default StudentsViewModel");
                DataContext = new StudentsViewModel();
            }

#if DEBUG
            // Debug-only global button click diagnostics — attach handler in DEBUG builds only
            // Docs: UIElement.AddHandler — https://learn.microsoft.com/dotnet/api/system.windows.uielement.addhandler
            AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
            Logger.Information("StudentsView: global button click diagnostics attached (DEBUG only)");
#endif

            // Ensure crisp images/text on high DPI
            // Docs: RenderOptions/TextOptions — https://learn.microsoft.com/dotnet/api/system.windows.media.renderoptions
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            Logger.Information("StudentsView initialized");
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // Dispose skin manager resources for this window
            try { SfSkinManager.Dispose(this); } catch { /* no-op */ }
            base.OnClosed(e);
        }

#if DEBUG
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
#endif

        // Handle per-monitor DPI changes to keep layout crisp
        protected override void OnDpiChanged(System.Windows.DpiScale oldDpi, System.Windows.DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            try
            {
                var scale = newDpi.DpiScaleX; // assume uniform scaling
                Resources["Dynamic.Font.Size.Base"] = 12.0 * scale; // if bound in XAML, this adjusts
                RenderOptions.SetBitmapScalingMode(this, scale >= 1.0 ? BitmapScalingMode.HighQuality : BitmapScalingMode.Fant);
                Logger.Information("StudentsView DPI changed: {OldX}→{NewX}", oldDpi.DpiScaleX, newDpi.DpiScaleX);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: OnDpiChanged handling failed");
            }
        }
    }
}
