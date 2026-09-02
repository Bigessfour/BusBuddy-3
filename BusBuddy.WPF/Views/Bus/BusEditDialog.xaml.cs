using System;
using System.Windows;
using BusBuddy.WPF.ViewModels.Bus;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Interaction logic for BusEditDialog.xaml
    /// </summary>
    public partial class BusEditDialog : Window
    {
        public BusBuddy.Core.Models.Bus Bus { get; set; }

        public BusEditDialog(BusBuddy.Core.Models.Bus? bus = null)
        {
            InitializeComponent();
            Bus = bus != null ? bus : new BusBuddy.Core.Models.Bus();
            BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);
            WireViewModel();
        }

        public BusEditDialog()
        {
            InitializeComponent();
            Bus = new BusBuddy.Core.Models.Bus();
            BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);
            WireViewModel();
        }

        private void WireViewModel()
        {
            var vm = new BusEditDialogViewModel(Bus);
            DataContext = vm;
            vm.CloseRequested += accepted =>
            {
                DialogResult = accepted;
                Close();
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            try { SfSkinManager.Dispose(this); } catch { }
            base.OnClosed(e);
        }
    }
}
