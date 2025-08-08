using System.Windows;
using BusBuddy.WPF.ViewModels.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Interaction logic for DriverForm.xaml
    /// MVP-ready driver entry form
    /// </summary>
    public partial class DriverForm : Window
    {
        public DriverForm()
        {
            InitializeComponent();

            // Set DataContext to DriverFormViewModel using DI
            if (App.ServiceProvider != null)
            {
                DataContext = App.ServiceProvider.GetRequiredService<DriverFormViewModel>();
            }
        }
    }
}
