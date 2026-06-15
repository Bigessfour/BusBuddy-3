namespace BusBuddy.Core.Models
{
    public class BusBuddyScheduleAppointment : IScheduleAppointment
    {
        public int Id { get; set; }
        public int ActivityId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool IsAllDay { get; set; }
        public object? AppointmentBackground { get; set; }
        public string Location { get; set; } = string.Empty;
    }
}
