using System.Windows;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Bus;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Modal host for fleet CRUD. Content is always <see cref="BusesView"/> (hosts <see cref="BusManagementView"/>).
    /// Not <c>BusForm</c> — that surface was removed; do not resurrect it.
    /// </summary>
    public partial class BusFleetWindow : ChromelessWindow
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<BusFleetWindow>();

        public BusFleetWindow() : this(BusManagementStartup.None)
        {
        }

        public BusFleetWindow(BusManagementStartup startup)
        {
            InitializeComponent();
            Content = new BusesView(startup);
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
