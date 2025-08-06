namespace BusBuddy.WPF.Services
{
    public interface IDriverAvailabilityService
    {
        Task<List<DriverAvailabilityInfo>> GetDriverAvailabilitiesAsync();
    }

    public class DriverAvailabilityInfo
    {
        public int DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public List<System.DateTime> AvailableDates { get; set; } = new();
    }
}
