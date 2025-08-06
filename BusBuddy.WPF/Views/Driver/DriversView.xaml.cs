using System.Windows.Controls;
using BusBuddy.WPF.ViewModels;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.WPF.Views.Driver
{
    /// <summary>
    /// Phase 2 Enhanced Drivers View - Temporary context creation for Phase 2
    /// </summary>
    public partial class DriversView : UserControl
    {
        public DriversView()
        {
            InitializeComponent();

            // Phase 2: Create context with proper options (temporary solution)
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseSqlite("Data Source=BusBuddy.db")
                .Options;

            var context = new BusBuddyDbContext(options);
            var viewModel = new DriversViewModel(context);
            DataContext = viewModel;

            // Load data when view is created
            _ = viewModel.LoadDriversAsync();
        }
    }
}
