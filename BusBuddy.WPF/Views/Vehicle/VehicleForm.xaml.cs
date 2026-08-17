using System.Windows;
using Serilog;
using Syncfusion.Windows.Shared;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Interaction logic for VehicleForm.xaml
    /// Step 3: Vehicle form with Syncfusion ChromelessWindow, SkinManager theming, and Core service integration
    /// </summary>
    public partial class VehicleForm : ChromelessWindow
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<VehicleForm>();
        public VehicleForm()
        {
            InitializeComponent();
            ApplySyncfusionTheme();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Apply Syncfusion theme with FluentDark default and FluentLight fallback
        /// </summary>
        private void ApplySyncfusionTheme()
        {
            BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try
            {
                SfSkinManager.Dispose(this);
                Log.Information("SfSkinManager resources disposed for {ViewName}", GetType().Name);
            }
            catch (System.Exception ex)
            {
                Log.Error("Error disposing SfSkinManager for {ViewName}: {Error}", GetType().Name, ex.Message);
            }
            base.OnClosed(e);
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
