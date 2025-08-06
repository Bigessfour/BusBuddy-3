using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.WPF.Services
{
    public class DriverAvailabilityService : IDriverAvailabilityService
    {
        private readonly IDriverService _driverService;

        public DriverAvailabilityService(IDriverService driverService)
        {
            _driverService = driverService;
        }

        public async Task<List<DriverAvailabilityInfo>> GetDriverAvailabilitiesAsync()
        {
            // For demo: Simulate availability for each driver for the next 7 days
            var drivers = await _driverService.GetAllDriversAsync();
            var today = DateTime.Today;
            var result = new List<DriverAvailabilityInfo>();
            foreach (var driver in drivers)
            {
                // TODO: Replace with real availability logic from DB or scheduling module
                var availableDates = new List<DateTime>();
                for (int i = 0; i < 7; i++)
                {
                    if (driver.Status == "Active")
                    {
                        availableDates.Add(today.AddDays(i));
                    }

                }
                result.Add(new DriverAvailabilityInfo
                {
                    DriverId = driver.DriverId,
                    DriverName = driver.DriverName,
                    AvailableDates = availableDates
                });
            }
            return result;
        }
    }
}
