using BusBuddy.WPF.ViewModels.Panels;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Represents a document panel within the dashboard's central document area.
    /// </summary>
    public class DocumentPanelViewModel : PanelViewModel
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
    }
}
