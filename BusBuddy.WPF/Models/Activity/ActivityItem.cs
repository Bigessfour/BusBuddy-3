using System;

namespace BusBuddy.WPF.Models.Activity
{
    // Lightweight UI model for Activity grid
    public class ActivityItem
    {
        public string ActivityName { get; set; } = string.Empty;
        public DateTime ActivityDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
