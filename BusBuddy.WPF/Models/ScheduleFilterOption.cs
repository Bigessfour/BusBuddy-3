namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents a filter option for schedules
    /// </summary>
    public class ScheduleFilterOption
    {
        public string Name { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
