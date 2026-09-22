using System.Diagnostics;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using Serilog;

namespace BusBuddy.WPF.Services
{
    public class DriverAvailabilityService : IDriverAvailabilityService
    {
        private static readonly ILogger Logger = Log.ForContext<DriverAvailabilityService>();
        private readonly IDriverService _driverService;
        private readonly IScheduleService _scheduleService;
        private readonly ITripEventService? _tripEventService;

        public DriverAvailabilityService(
            IDriverService driverService,
            IScheduleService scheduleService,
            ITripEventService? tripEventService = null)
        {
            _driverService = driverService;
            _scheduleService = scheduleService;
            _tripEventService = tripEventService;
            Logger.Debug("DriverAvailabilityService constructed");
        }

        public async Task<List<DriverAvailabilityInfo>> GetDriverAvailabilitiesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Logger.Information("Starting driver availability calculation (14-day window from {From:yyyy-MM-dd})", DateTime.Today);

            var drivers = await _driverService.GetAllDriversAsync();
            var schedules = (await _scheduleService.GetSchedulesAsync()).ToList();
            var trips = _tripEventService is null
                ? new List<TripEvent>()
                : (await _tripEventService.GetTripsByDateRangeAsync(DateTime.Today, DateTime.Today.AddDays(14))).ToList();
            var today = DateTime.Today;
            var result = new List<DriverAvailabilityInfo>();
            var inactiveSkipped = 0;
            var withOpenDays = 0;

            foreach (var driver in drivers)
            {
                if (!string.Equals(driver.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    inactiveSkipped++;
                    result.Add(new DriverAvailabilityInfo
                    {
                        DriverId = driver.DriverId,
                        DriverName = driver.DriverName,
                        AvailableDates = new List<DateTime>()
                    });
                    continue;
                }

                var dates = DriverAvailabilityCalculator
                    .AvailableDates(schedules, trips, driver.DriverId, today, 14)
                    .ToList();
                if (dates.Count > 0)
                {
                    withOpenDays++;
                }

                result.Add(new DriverAvailabilityInfo
                {
                    DriverId = driver.DriverId,
                    DriverName = driver.DriverName,
                    AvailableDates = dates
                });
            }

            stopwatch.Stop();
            Logger.Information(
                "Driver availability calculated Drivers={DriverCount} InactiveSkipped={Inactive} WithOpenDays={WithOpenDays} Schedules={ScheduleCount} Trips={TripCount} ElapsedMs={ElapsedMs}",
                result.Count, inactiveSkipped, withOpenDays, schedules.Count, trips.Count, stopwatch.ElapsedMilliseconds);

            return result;
        }
    }
}
