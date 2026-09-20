using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using BusBuddy.WPF.Controls;
using BusBuddy.WPF.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.Windows.Controls.Input;

namespace BusBuddy.WPF.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsView>();

        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += OnSettingsDataContextChanged;
            WireCapture(DataContext as SettingsViewModel);
            try
            {
                if (DataContext == null && App.ServiceProvider != null)
                {
                    DataContext = App.ServiceProvider.GetService<SettingsViewModel>()
                        ?? App.ServiceProvider.GetRequiredService<SettingsViewModel>();
                }

                if (DataContext is SettingsViewModel vm)
                {
                    WireCapture(vm);
                    Logger.Information("SettingsView DataContext set {DataContext}", DataContext.GetType().Name);
                }
                else
                {
                    Logger.Warning("SettingsView opened without SettingsViewModel DataContext={DataContext}", DataContext?.GetType().Name ?? "(null)");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "SettingsView failed to resolve SettingsViewModel");
            }
        }

        private void OnSettingsDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            WireCapture(e.NewValue as SettingsViewModel);

        private void WireCapture(SettingsViewModel? vm)
        {
            if (vm is not null)
            {
                vm.CaptureViewFields = CaptureViewFields;
            }
        }

        private void CaptureViewFields()
        {
            if (DataContext is not SettingsViewModel vm)
            {
                return;
            }

            vm.DepotName = TextOrEmpty(DepotNameBox);
            vm.DepotAddress = DepotAddressBox.AddressText?.Trim() ?? string.Empty;
            vm.DepotCity = TextOrEmpty(DepotCityBox);
            vm.DepotState = TextOrEmpty(DepotStateBox);
            vm.DepotZipCode = TextOrEmpty(DepotZipBox);
            vm.DepotLatitudeText = TextOrEmpty(DepotLatBox);
            vm.DepotLongitudeText = TextOrEmpty(DepotLonBox);
            vm.BoundingBoxMinLatText = TextOrEmpty(BBoxSouthBox);
            vm.BoundingBoxMinLonText = TextOrEmpty(BBoxWestBox);
            vm.BoundingBoxMaxLatText = TextOrEmpty(BBoxNorthBox);
            vm.BoundingBoxMaxLonText = TextOrEmpty(BBoxEastBox);
        }

        private static string TextOrEmpty(SfTextBoxExt box) => box.Text?.Trim() ?? string.Empty;

        private void DepotAddress_Applied(object sender, PlaceAddressAppliedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.ApplyDepotAddress(e.Applied);
            }
        }

        private void OnMapsLegalLinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                Logger.Information("Opened Maps legal URL {Uri}", e.Uri);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "Failed to open Maps legal URL {Uri}", e.Uri);
            }

            e.Handled = true;
        }
    }
}
