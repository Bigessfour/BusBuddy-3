using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.ViewModels.Bus;
using BusBuddy.WPF.Utilities;
using Serilog;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Interaction logic for BusForm.xaml
    /// MVP-ready bus entry form with Syncfusion ChromelessWindow and SkinManager theming
    /// </summary>
    public partial class BusForm : UserControl, BusBuddy.WPF.Views.Common.IDialogHostable
    {
        private static readonly ILogger Logger = Log.ForContext<BusForm>();
        public event EventHandler? RequestCloseByHost;
        public bool? DialogResult { get; private set; }
        public BusFormViewModel? ViewModel { get; private set; }

        public BusForm()
        {
            InitializeComponent();
            InitializeViewModel();
            ApplyTheme();
            Logger.Information("BusForm (UserControl) initialized (Create mode)");
        }

        public BusForm(BusBuddy.Core.Models.Bus bus) : this()
        {
            // Reinitialize for edit mode with provided bus
            try
            {
                var sp = App.ServiceProvider;
                var svc = sp?.GetService<BusBuddy.Core.Services.Interfaces.IBusService>();
                ViewModel = new BusFormViewModel(svc, bus);
            }
            catch
            {
                ViewModel = new BusFormViewModel(null, bus);
            }
            DataContext = ViewModel;
            HookVm();
            Logger.Information("BusForm (UserControl) initialized (Edit mode) for BusNumber={BusNumber}", ViewModel?.BusNumber);
        }

        private void InitializeViewModel()
        {
            try
            {
                var sp = App.ServiceProvider;
                var vm = sp?.GetService<BusFormViewModel>() ?? new BusFormViewModel();
                ViewModel = vm;
            }
            catch
            {
                ViewModel = new BusFormViewModel();
            }
            DataContext = ViewModel;
            HookVm();
        }

        private void HookVm()
        {
            if (ViewModel == null) return;
            ViewModel.RequestClose -= OnVmRequestClose;
            ViewModel.RequestClose += OnVmRequestClose;
        }

        private void OnVmRequestClose(object? sender, bool? e)
        {
            Logger.Information("BusForm ViewModel requested close. Result={Result}", e);
            DialogResult = e;
            RequestCloseByHost?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyTheme()
        {
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);
        }

        public void DisposeResources()
        {
            try { if (ViewModel != null) ViewModel.RequestClose -= OnVmRequestClose; } catch { }
            try { SfSkinManager.Dispose(this); } catch { }
        }
    }
}
