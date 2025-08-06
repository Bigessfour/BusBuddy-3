
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BusBuddy.WPF.ViewModels.Panels
{
    /// <summary>
    /// Base class for all panel view models, providing INotifyPropertyChanged implementation.
    /// </summary>
    public abstract class PanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
