using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Syncfusion.Windows.Shared;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.ViewModels.Driver;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Interaction logic for DriverForm.xaml
    /// MVP-ready driver entry form with ChromelessWindow and SkinManager theming
    /// </summary>
    public partial class DriverForm : ChromelessWindow
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<DriverForm>();
        private DriverFormViewModel? ViewModel => DataContext as DriverFormViewModel;
        public DriverForm()
        {
            Log.Information("Initializing {ViewName}", nameof(DriverForm));
            InitializeComponent();

            // Apply Syncfusion theme — FluentDark default, FluentLight fallback
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            try
            {
                using var dark = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, dark);
                Log.Information("FluentDark theme applied to DriverForm");
            }
            catch (System.Exception ex)
            {
                Log.Error("Failed to apply FluentDark theme to DriverForm: {Error}", ex.Message);
                using var white = new Theme("FluentWhite");
                SfSkinManager.SetTheme(this, white);
                Log.Information("Fallback to FluentLight theme for DriverForm");
            }

            // Set DataContext to DriverFormViewModel using DI
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<DriverFormViewModel>();
            }

            // Hook RequestClose to close dialog with result
            if (ViewModel != null)
            {
                ViewModel.RequestClose += OnRequestClose;
            }

            // Global button click diagnostics (WPF Button and Syncfusion ButtonAdv)
            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                Log.Information("{ViewName}: global button diagnostics attached", nameof(DriverForm));
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "{ViewName}: failed to attach button diagnostics", nameof(DriverForm));
            }

            Log.Information("{ViewName} initialized successfully", nameof(DriverForm));
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try
            {
                SfSkinManager.Dispose(this);
                Log.Information("SfSkinManager resources disposed for DriverForm");
            }
            catch (System.Exception ex)
            {
                Log.Error("Error disposing SfSkinManager for DriverForm: {Error}", ex.Message);
            }

            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnRequestClose;
            }
            base.OnClosed(e);
        }

        // Handles ViewModel RequestClose event to close dialog with result.
        private void OnRequestClose(object? sender, bool? dialogResult)
        {
            try
            {
                Log.Information("DriverForm RequestClose received. DialogResult={DialogResult}", dialogResult);
                DialogResult = dialogResult;
                Close();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "DriverForm: error handling RequestClose");
                Close();
            }
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
                    Log.Information("DriverForm Button: {Type} Name={Name} Label={Label} CanExecute={CanExecute}", type, name, badv.Label, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null;
                    try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    Log.Information("DriverForm Button: {Type} Name={Name} Content={Content} CanExecute={CanExecute}", type, name, btn.Content, canExec);
                }
                else
                {
                    Log.Information("DriverForm Button: {Type} Name={Name}", type, name);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "DriverForm: button click logging failed");
            }
        }
    }
}
