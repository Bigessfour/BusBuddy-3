using System.Windows;
using System.Windows.Controls;
using Serilog;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Interaction logic for VehicleForm.xaml
    /// Step 3: Vehicle form with Syncfusion ChromelessWindow, SkinManager theming, and Core service integration
    /// </summary>
    public partial class VehicleForm : UserControl
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<VehicleForm>();
        public VehicleForm()
        {
            InitializeComponent();
            ApplyTheme();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Apply Syncfusion theme with FluentDark default and FluentLight fallback
        /// </summary>
        private void ApplyTheme()
        {
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);
        }

        public void DisposeResources()
        {
            try { SfSkinManager.Dispose(this); } catch { }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("Loaded {ViewName} with theme resource {ResourceKey}", GetType().Name, "BusBuddy.Brush.Primary");
            }
            catch
            {
                // no-op for MVP
            }
        }
    }
}
