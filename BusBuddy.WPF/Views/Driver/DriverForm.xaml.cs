using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.ViewModels.Driver;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.Windows.Tools.Controls;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Interaction logic for DriverForm.xaml
    /// MVP-ready driver entry form with ChromelessWindow and SkinManager theming
    /// </summary>
    public partial class DriverForm : UserControl, BusBuddy.WPF.Views.Common.IDialogHostable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<DriverForm>();
        public event EventHandler? RequestCloseByHost; // IDialogHostable contract (EventHandler)
        public bool? DialogResult { get; private set; }
        public DriverFormViewModel? ViewModel { get; private set; }

        public DriverForm()
        {
            Log.Information("Initializing {ViewName}", nameof(DriverForm));
            InitializeComponent();
            ApplyTheme();
            InitializeViewModel();
            AttachDiagnostics();
            Log.Information("{ViewName} initialized successfully (Create mode)", nameof(DriverForm));
        }

        private void InitializeViewModel()
        {
            try
            {
                if (App.ServiceProvider != null)
                {
                    ViewModel = App.ServiceProvider.GetRequiredService<DriverFormViewModel>();
                }
                else
                {
                    // Fallback — driver service required, throw if null in ctor
                    Log.Warning("ServiceProvider unavailable; DriverFormViewModel resolution skipped");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Failed to resolve DriverFormViewModel");
            }
            DataContext = ViewModel;
            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnVmRequestClose;
                ViewModel.RequestClose += OnVmRequestClose;
            }
        }

        private void ApplyTheme()
        {
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);
        }

        private void OnVmRequestClose(object? sender, bool? e)
        {
            Log.Information("DriverForm ViewModel requested close. Result={Result}", e);
            DialogResult = e;
            RequestCloseByHost?.Invoke(this, EventArgs.Empty);
        }

        private void AttachDiagnostics()
        {
            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                Log.Information("{ViewName}: global button diagnostics attached", nameof(DriverForm));
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "{ViewName}: failed to attach button diagnostics", nameof(DriverForm));
            }
        }

        public void DisposeResources()
        {
            try { if (ViewModel != null) ViewModel.RequestClose -= OnVmRequestClose; } catch { }
            try { SfSkinManager.Dispose(this); } catch { }
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
