using System;
using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.Views.Common;

namespace BusBuddy.WPF.Views.Student
{
    public partial class QuickActionsDialog : UserControl, IDialogHostable
    {
        public event EventHandler? RequestCloseByHost;

        public QuickActionsDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            AssignRouteButton.Click += AssignRouteButton_Click;
            FilterActiveButton.Click += FilterActiveButton_Click;
            ClearFiltersButton.Click += ClearFiltersButton_Click;
            CloseButton.Click += (_, __) => RequestCloseByHost?.Invoke(this, EventArgs.Empty);
        }

        private void AssignRouteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FindStudentsViewModel() is { } vm && vm.SelectedStudent != null)
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
            if (FindStudentsViewModel() is { } vm)
            {
                vm.QuickSearchText = string.Empty;
                vm.StatusMessage = "Showing active students (quick action)";
            }
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (FindStudentsViewModel() is { } vm)
            {
                vm.QuickSearchText = string.Empty;
                vm.StatusMessage = "Cleared filters";
            }
        }

        private BusBuddy.WPF.ViewModels.Student.StudentsViewModel? FindStudentsViewModel()
        {
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is BusBuddy.WPF.ViewModels.Student.StudentsViewModel vm)
                {
                    return vm;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public void DisposeResources()
        {
            AssignRouteButton.Click -= AssignRouteButton_Click;
            FilterActiveButton.Click -= FilterActiveButton_Click;
            ClearFiltersButton.Click -= ClearFiltersButton_Click;
        }
    }
}
