using System.Windows;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Vehicle;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Vehicle
{
    /// <summary>
    /// Modal host for fleet CRUD. Content is always <see cref="VehicleManagementView"/>.
    /// </summary>
    public partial class VehicleForm : ChromelessWindow
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<VehicleForm>();

        public VehicleForm() : this(VehicleManagementStartup.None)
        {
        }

        public VehicleForm(VehicleManagementStartup startup)
        {
            InitializeComponent();
            Content = new VehicleManagementView(startup);
            ApplySyncfusionTheme();
            Loaded += OnLoaded;
        }

        private void ApplySyncfusionTheme()
        {
            SyncfusionThemeManager.ApplyTheme(this);
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
                Log.Error(ex, "Error disposing SfSkinManager for {ViewName}", GetType().Name);
            }

            base.OnClosed(e);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Log.Information("Loaded {ViewName}", GetType().Name);
        }
    }
}
