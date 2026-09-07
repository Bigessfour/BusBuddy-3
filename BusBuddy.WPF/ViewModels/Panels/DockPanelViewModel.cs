using BusBuddy.WPF.ViewModels.Panels;
using Syncfusion.Windows.Tools.Controls; // Required for DockState and DockSide enums

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Represents a dockable panel within the dashboard.
    /// </summary>
    public class DockPanelViewModel : PanelViewModel
    {
        private string? _header;
        public string? Header
        {
            get => _header;
            set { _header = value; OnPropertyChanged(); }
        }

        private object? _content;
        public object? Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        private DockState _state = DockState.Dock;
        public DockState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        private DockSide _side = DockSide.Left;
        public DockSide Side
        {
            get => _side;
            set { _side = value; OnPropertyChanged(); }
        }
    }
}
