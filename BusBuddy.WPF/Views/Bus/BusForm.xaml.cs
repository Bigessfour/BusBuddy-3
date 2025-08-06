using System.Windows;
using BusBuddy.WPF.ViewModels.Bus;

namespace BusBuddy.WPF.Views.Bus
{
    /// <summary>
    /// Interaction logic for BusForm.xaml
    /// MVP-ready bus entry form with Syncfusion controls
    /// </summary>
    public partial class BusForm : Window
    {
        public BusForm()
        {
            InitializeComponent();
            DataContext = new BusFormViewModel();
        }

        public BusForm(BusBuddy.Core.Models.Bus bus)
        {
            InitializeComponent();
            DataContext = new BusFormViewModel(bus);
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
