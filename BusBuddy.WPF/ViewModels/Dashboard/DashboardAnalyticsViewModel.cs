using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusBuddy.WPF.ViewModels
{
    public class DashboardAnalyticsViewModel : BaseViewModel
    {
        public ObservableCollection<RouteUsage> RouteUsages { get; set; } = new();
        public ObservableCollection<ActivityTypeStat> ActivityTypes { get; set; } = new();
    }

    public partial class RouteUsage : ObservableObject
    {
        [ObservableProperty]
        private string _routeName = string.Empty;

        [ObservableProperty]
        private double _miles;
    }

    public partial class ActivityTypeStat : ObservableObject
    {
        [ObservableProperty]
        private string _type = string.Empty;

        [ObservableProperty]
        private int _count;
    }
}
