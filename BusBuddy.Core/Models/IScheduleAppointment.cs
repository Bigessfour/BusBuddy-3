namespace BusBuddy.Core.Models
{
    public interface IScheduleAppointment
    {
        DateTime StartTime { get; set; }
        DateTime EndTime { get; set; }
        string Subject { get; set; }
    }
}
