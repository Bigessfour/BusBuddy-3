using System;
using System.Windows;
using BusBuddy.Core.Models;
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
            // Load existing data into form controls
            LoadBusData();
            BusNumberTextBox.Focus();
        }

        public BusEditDialog()
        {
            InitializeComponent();
            Bus = new BusBuddy.Core.Models.Bus(); // Initialize Bus property to fix CS8618
            BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            try { SfSkinManager.Dispose(this); } catch { }
            base.OnClosed(e);
        }

        private void LoadBusData()
        {
            if (Bus != null)
            {
                BusNumberTextBox.Text = Bus.BusNumber ?? string.Empty;
                MakeTextBox.Text = Bus.Make ?? string.Empty;
                ModelTextBox.Text = Bus.Model ?? string.Empty;
                YearTextBox.Text = Bus.Year == 0 ? string.Empty : Bus.Year.ToString();
                CapacityTextBox.Text = Bus.SeatingCapacity == 0 ? string.Empty : Bus.SeatingCapacity.ToString();
                LicenseNumberTextBox.Text = Bus.LicenseNumber ?? string.Empty;
            }
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(BusNumberTextBox.Text))
                {
                    MessageBox.Show("Bus Number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BusNumberTextBox.Focus();
                    return;
                }
                if (!int.TryParse(CapacityTextBox.Text, out var capacity) || capacity <= 0)
                {
                    MessageBox.Show("Enter valid seating capacity.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CapacityTextBox.Focus();
                    return;
                }
                if (!int.TryParse(YearTextBox.Text, out var year) || year < 1990 || year > DateTime.Now.Year + 1)
                {
                    MessageBox.Show("Enter valid year (1990+).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    YearTextBox.Focus();
                    return;
                }

                // Persist values back
                Bus.BusNumber = BusNumberTextBox.Text.Trim();
                Bus.Make = MakeTextBox.Text.Trim();
                Bus.Model = ModelTextBox.Text.Trim();
                Bus.Year = year;
                Bus.SeatingCapacity = capacity;
                Bus.LicenseNumber = LicenseNumberTextBox.Text.Trim();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving bus data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
