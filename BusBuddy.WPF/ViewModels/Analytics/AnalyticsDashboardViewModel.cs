using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BusBuddy.WPF.ViewModels.Analytics
{
    /// <summary>
    /// ViewModel for the Analytics Dashboard — Phase 2 foundation
    /// </summary>
    public class AnalyticsDashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Add properties and logic for analytics here
    }
}
