using System;
using System.Windows;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Interaction logic for BusEditDialog.xaml - MVP Version
    /// </summary>
    public partial class BusEditDialog : Window
    {
        public BusBuddy.Core.Models.Bus Bus { get; set; }

        public BusEditDialog(BusBuddy.Core.Models.Bus? bus = null)
        {
            InitializeComponent();
            Bus = bus != null ? bus : new BusBuddy.Core.Models.Bus();
            // Commented out missing UI controls for MVP
            // LoadBusData();

            // Configure button clicks
            // SaveButton.Click += SaveButton_Click;
            // CancelButton.Click += CancelButton_Click;

            // Set focus to first field
            // BusNumberTextBox.Focus();
        }

        public BusEditDialog()
        {
            InitializeComponent();
            Bus = new BusBuddy.Core.Models.Bus(); // Initialize Bus property to fix CS8618
        }

        // Commented out missing UI controls for MVP
        // private void LoadBusData()
        // {
        //     if (Bus != null)
        //     {
        //         BusNumberTextBox.Text = Bus.BusNumber ?? string.Empty;
        //         MakeTextBox.Text = Bus.Make ?? string.Empty;
        //         ModelTextBox.Text = Bus.Model ?? string.Empty;
        //         YearTextBox.Text = Bus.Year.ToString();
        //         CapacityTextBox.Text = Bus.SeatingCapacity.ToString();
        //         LicenseNumberTextBox.Text = Bus.LicenseNumber ?? string.Empty;
        //     }
        // }

        // Commented out missing UI controls for MVP
        // private void SaveButton_Click(object sender, RoutedEventArgs e)
        // {
        //     try
        //     {
        //         // Validate required fields
        //         if (string.IsNullOrWhiteSpace(BusNumberTextBox.Text))
        //         {
        //             MessageBox.Show("Bus Number is required.", "Validation Error",
        //                           MessageBoxButton.OK, MessageBoxImage.Warning);
        //             BusNumberTextBox.Focus();
        //             return;
        //         }

        //         if (!int.TryParse(CapacityTextBox.Text, out int capacity) || capacity <= 0)
        //         {
        //             MessageBox.Show("Please enter a valid seating capacity.", "Validation Error",
        //                           MessageBoxButton.OK, MessageBoxImage.Warning);
        //             CapacityTextBox.Focus();
        //             return;
        //         }

        //         if (!int.TryParse(YearTextBox.Text, out int year) || year < 1900 || year > DateTime.Now.Year + 1)
        //         {
        //             MessageBox.Show("Please enter a valid year.", "Validation Error",
        //                           MessageBoxButton.OK, MessageBoxImage.Warning);
        //             YearTextBox.Focus();
        //             return;
        //         }

        //         // Update bus properties
        //         Bus.BusNumber = BusNumberTextBox.Text.Trim();
        //         Bus.Make = MakeTextBox.Text.Trim();
        //         Bus.Model = ModelTextBox.Text.Trim();
        //         Bus.Year = year;
        //         Bus.SeatingCapacity = capacity;
        //         Bus.LicenseNumber = LicenseNumberTextBox.Text.Trim();

        //         DialogResult = true;
        //         Close();
        //     }
        //     catch (Exception ex)
        //     {
        //         MessageBox.Show($"Error saving bus data: {ex.Message}", "Error",
        //                       MessageBoxButton.OK, MessageBoxImage.Error);
        //     }
        // }

        // private void CancelButton_Click(object sender, RoutedEventArgs e)
        // {
        //     DialogResult = false;
        //     Close();
        // }
    }
}
