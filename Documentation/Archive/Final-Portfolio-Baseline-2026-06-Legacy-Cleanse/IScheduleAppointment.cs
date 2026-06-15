namespace BusBuddy.Core.Models
{
    // Stub for IScheduleAppointment
    public interface IScheduleAppointment
    {
        DateTime StartTime { get; set; }
        DateTime EndTime { get; set; }
        string Subject { get; set; }
    }
}
