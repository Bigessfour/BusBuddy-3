using System.Windows;

namespace BusBuddy.WPF.Views.Student
{
    public partial class QuickActionsDialog : Window
    {
        public QuickActionsDialog()
        {
            InitializeComponent();
            Loaded += QuickActionsDialog_Loaded;
            CloseButton.Click += (_, __) => Close();
        }

        private void QuickActionsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            AssignRouteButton.Click += AssignRouteButton_Click;
            FilterActiveButton.Click += FilterActiveButton_Click;
            ClearFiltersButton.Click += ClearFiltersButton_Click;
        }

        private void AssignRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner?.DataContext is ViewModels.Student.StudentsViewModel vm && vm.SelectedStudent != null)
            {
                // Placeholder: assign first available route
                if (vm.AvailableRoutes.Count > 0 && string.IsNullOrWhiteSpace(vm.SelectedStudent.AMRoute))
                {
                    var route = vm.AvailableRoutes[0];
                    vm.SelectedStudent.AMRoute = route.RouteName;
                    vm.StatusMessage = $"Assigned route {route.RouteName} to {vm.SelectedStudent.StudentName}";
                }
            }
        }

        private void FilterActiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner?.DataContext is ViewModels.Student.StudentsViewModel vm)
            {
                vm.QuickSearchText = string.Empty;
                vm.StatusMessage = "Showing active students (quick action)";
            }
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner?.DataContext is ViewModels.Student.StudentsViewModel vm)
            {
                vm.QuickSearchText = string.Empty;
                vm.StatusMessage = "Cleared filters";
            }
        }
    }
}
