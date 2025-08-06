using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Services;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Phase 2 Route Management ViewModel
    /// Enhanced route planning and management functionality
    /// </summary>
    public class RouteManagementViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<BusBuddy.Core.Models.Route> Routes { get; set; } = new();

        // Entity Framework context for data access
        private readonly IBusBuddyDbContextFactory _contextFactory;

        private BusBuddy.Core.Models.Route? _selectedRoute;
        public BusBuddy.Core.Models.Route? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                _selectedRoute = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRouteSelected));
            }
        }

        public bool IsRouteSelected => SelectedRoute is not null;

        public RouteManagementViewModel()
        {
            // Initialize EF context factory
            _contextFactory = new BusBuddyDbContextFactory();
            LoadRoutes();
        }

        private void LoadRoutes()
        {
            // TODO: Load routes from service in Phase 2
            // For now, add sample data
            Routes.Add(new BusBuddy.Core.Models.Route
            {
                RouteId = 1,
                RouteName = "Route 1 - Elementary",
                Description = "Elementary school morning route",
                School = "Elementary School"
            });
            Routes.Add(new BusBuddy.Core.Models.Route
            {
                RouteId = 2,
                RouteName = "Route 2 - High School",
                Description = "High school afternoon route",
                School = "High School"
            });
        }

        private void AddRoute()
        {
            var newRoute = new BusBuddy.Core.Models.Route
            {
                RouteName = "New Route",
                School = SelectedRoute?.School ?? "Default School"
            };
            // TODO: Add to service in Phase 2
            LoadRoutes();
        }

        private void CopyRoute()
        {
            if (SelectedRoute is not null)
            {
                var copiedRoute = new BusBuddy.Core.Models.Route
                {
                    RouteName = $"Copy of {SelectedRoute.RouteName}",
                    School = SelectedRoute.School
                };
                // TODO: Add to service in Phase 2
                LoadRoutes();
            }
        }

        public void Dispose()
        {
            // No-op: context is now always local and disposed via using
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
