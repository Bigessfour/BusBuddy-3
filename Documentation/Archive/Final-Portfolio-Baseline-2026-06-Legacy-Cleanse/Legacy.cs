namespace BusBuddy.Core.Models
{
    public class BusInfo
    {
        public int BusId { get; set; }
        public string BusNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastMaintenance { get; set; }
    }

    public class RouteInfo
    {
        public int RouteId { get; set; }
        public string RouteName { get; set; } = string.Empty;
    }

    public class ScheduleInfo
    {
        public int ScheduleId { get; set; }
        public string ScheduleName { get; set; } = string.Empty;
    }
}
