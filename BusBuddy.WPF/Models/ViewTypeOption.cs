using Syncfusion.UI.Xaml.Scheduler;

namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents a view type option for the scheduler
    /// </summary>
    public class ViewTypeOption
    {
        public string Name { get; set; } = string.Empty;
        public SchedulerViewType ViewType { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
