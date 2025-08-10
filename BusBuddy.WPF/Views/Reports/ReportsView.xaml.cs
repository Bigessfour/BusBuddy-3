using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Tools.Controls;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Reports;

namespace BusBuddy.WPF.Views.Reports
{
    /// <summary>
    /// Interaction logic for ReportsView.xaml
    /// </summary>
    public partial class ReportsView : UserControl
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ReportsView>();
        public ReportsView()
        {
            Log.Information("Initializing {ViewName}", nameof(ReportsView));

            InitializeComponent();

            // Ensure DataContext is set so command bindings on buttons work
            try
            {
                if (this.DataContext is not ReportsViewModel)
                {
                    this.DataContext = new ReportsViewModel();
                    Log.Information("{ViewName}: DataContext set to ReportsViewModel", nameof(ReportsView));
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "{ViewName}: failed to set DataContext to ReportsViewModel", nameof(ReportsView));
            }

            // Apply Syncfusion theme with fallback (centralized manager logs details)
            try
            {
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                SyncfusionThemeManager.ApplyTheme(this);
                Log.Information("Theme applied for {ViewName}", nameof(ReportsView));
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "Failed to apply theme for {ViewName}", nameof(ReportsView));
            }

            // Global button diagnostics (WPF Button and Syncfusion ButtonAdv)
            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                Log.Information("{ViewName}: global button diagnostics attached", nameof(ReportsView));
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "{ViewName}: failed to attach button diagnostics", nameof(ReportsView));
            }

            Loaded += OnLoaded;

            Log.Information("{ViewName} initialized successfully", nameof(ReportsView));
        }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                Log.Information("Loaded {ViewName} with theme resource {ResourceKey}", GetType().Name, "BusBuddy.Brush.Primary");
            }
            catch { }
        }

        private void OnAnyButtonClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                string name = fe?.Name ?? "(unnamed)";
                string type = src?.GetType().Name ?? "(unknown)";

                if (src is ButtonAdv badv)
                {
                    bool? canExec = null;
                    try { if (badv.Command != null) canExec = badv.Command.CanExecute(badv.CommandParameter); } catch { }
                    Log.Information("ReportsView Button: {Type} Name={Name} Label={Label} CanExecute={CanExecute}", type, name, badv.Label, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null;
                    try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    Log.Information("ReportsView Button: {Type} Name={Name} Content={Content} CanExecute={CanExecute}", type, name, btn.Content, canExec);
                }
                else
                {
                    Log.Information("ReportsView Button: {Type} Name={Name}", type, name);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "ReportsView: button click logging failed");
            }
        }
    }
}
