#if DEBUG
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>DEBUG-only driver record metrics. Not license CRUD.</summary>
public static class DriverDiagnostics
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DriverDiagnostics));

    public static async Task<Dictionary<string, object>> GetDriverDiagnosticsAsync(
        IBusBuddyDbContextFactory contextFactory,
        int driverId)
    {
        try
        {
            Logger.Debug("Retrieving diagnostic information for driver {DriverId}", driverId);

            using var context = contextFactory.CreateDbContext();
            var driver = await context.Drivers
                .AsNoTracking()
                .Include(d => d.AMRoutes)
                .Include(d => d.PMRoutes)
                .FirstOrDefaultAsync(d => d.DriverId == driverId);

            if (driver == null)
            {
                Logger.Warning("Driver with ID {DriverId} not found for diagnostics", driverId);
                return new Dictionary<string, object> { { "Error", "Driver not found" } };
            }

            var diagnostics = new Dictionary<string, object>
            {
                { "DriverId", driver.DriverId },
                { "DriverName", driver.DriverName },
                { "FullName", driver.FullName },
                { "RecordCreationTime", driver.CreatedDate },
                { "LastUpdateTime", driver.UpdatedDate ?? DateTime.MinValue },
                { "RecordAgeInDays", (DateTime.UtcNow - driver.CreatedDate).TotalDays },
                { "QualificationStatus", driver.QualificationStatus },
                { "LicenseStatus", driver.LicenseStatus },
                { "IsAvailable", driver.IsAvailable },
                { "Status", driver.Status },
                { "TrainingComplete", driver.TrainingComplete },
                { "AMRouteCount", driver.AMRoutes.Count },
                { "PMRouteCount", driver.PMRoutes.Count },
                { "LicenseExpiryDate", driver.LicenseExpiryDate as object ?? "Not Set" },
                { "DaysUntilLicenseExpiry", driver.LicenseExpiryDate.HasValue
                    ? (object)(driver.LicenseExpiryDate.Value - DateTime.Today).TotalDays
                    : "Not Applicable" },
                { "BackgroundCheckExpiryDate", driver.BackgroundCheckExpiry as object ?? "Not Set" },
                { "DaysUntilBackgroundCheckExpiry", driver.BackgroundCheckExpiry.HasValue
                    ? (object)(driver.BackgroundCheckExpiry.Value - DateTime.Today).TotalDays
                    : "Not Applicable" },
                { "DrugTestExpiryDate", driver.DrugTestExpiry as object ?? "Not Set" },
                { "DaysUntilDrugTestExpiry", driver.DrugTestExpiry.HasValue
                    ? (object)(driver.DrugTestExpiry.Value - DateTime.Today).TotalDays
                    : "Not Applicable" },
                { "PhysicalExamExpiryDate", driver.PhysicalExamExpiry as object ?? "Not Set" },
                { "DaysUntilPhysicalExamExpiry", driver.PhysicalExamExpiry.HasValue
                    ? (object)(driver.PhysicalExamExpiry.Value - DateTime.Today).TotalDays
                    : "Not Applicable" },
                { "NeedsAttention", driver.NeedsAttention },
                { "ModelState", SerializeDriverForDiagnostics(driver) }
            };

            var upcomingRoutes = await context.Routes
                .AsNoTracking()
                .Where(r => r.Date >= DateTime.Today && (r.AMDriverId == driverId || r.PMDriverId == driverId))
                .OrderBy(r => r.Date)
                .Take(5)
                .ToListAsync();

            diagnostics.Add("UpcomingRoutes", upcomingRoutes.Select(r => new
            {
                r.RouteId,
                r.Date,
                r.RouteName,
                IsAMRoute = r.AMDriverId == driverId,
                IsPMRoute = r.PMDriverId == driverId,
                BusNumber = BusNumberFor(r, driverId)
            }).ToList());

            return diagnostics;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error generating diagnostics for driver {DriverId}", driverId);
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }

    public static async Task<Dictionary<string, object>> GetDriverOperationMetricsAsync(
        IBusBuddyDbContextFactory contextFactory,
        Func<bool, Task<int>> availableDriversToday)
    {
        try
        {
            Logger.Debug("Retrieving driver operation metrics");

            var metrics = new Dictionary<string, object>();
            using var context = contextFactory.CreateDbContext();

            metrics["TotalDriverCount"] = await context.Drivers.CountAsync();
            metrics["ActiveDriverCount"] = await context.Drivers.CountAsync(d => d.Status == "Active");
            metrics["InactiveDriverCount"] = await context.Drivers.CountAsync(d => d.Status != "Active");
            metrics["QualifiedDriverCount"] = await context.Drivers.CountAsync(d => d.Status == "Active" && d.TrainingComplete);
            metrics["UnqualifiedDriverCount"] = await context.Drivers.CountAsync(d => !d.TrainingComplete);

            var drivers = await context.Drivers.AsNoTracking().ToListAsync();
            metrics["ExpiredLicenseCount"] = drivers.Count(d => d.LicenseStatus == "Expired");
            metrics["ExpiringLicenseCount"] = drivers.Count(d => d.LicenseStatus == "Expiring Soon");
            metrics["CurrentLicenseCount"] = drivers.Count(d => d.LicenseStatus == "Current");

            var today = DateTime.Today;
            metrics["DriversWithActiveAssignments"] = await context.Routes
                .Where(r => r.Date >= today)
                .Select(r => r.AMDriverId)
                .Union(context.Routes.Where(r => r.Date >= today).Select(r => r.PMDriverId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .CountAsync();

            var sw = new System.Diagnostics.Stopwatch();

            sw.Start();
            await context.Drivers.AsNoTracking().ToListAsync();
            sw.Stop();
            metrics["AllDriversQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            await context.Drivers.AsNoTracking().Where(d => d.Status == "Active").ToListAsync();
            sw.Stop();
            metrics["ActiveDriversQueryTimeMs"] = sw.ElapsedMilliseconds;

            sw.Restart();
            var activeDriverIds = await context.Drivers
                .Where(d => d.Status == "Active")
                .Select(d => d.DriverId)
                .ToListAsync();

            await context.Routes
                .Where(r => activeDriverIds.Contains(r.AMDriverId ?? -1) || activeDriverIds.Contains(r.PMDriverId ?? -1))
                .ToListAsync();
            sw.Stop();
            metrics["DriverRoutesQueryTimeMs"] = sw.ElapsedMilliseconds;

            metrics["AvailableAMDriversToday"] = await availableDriversToday(true);
            metrics["AvailablePMDriversToday"] = await availableDriversToday(false);

            return metrics;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error generating driver operation metrics");
            return new Dictionary<string, object> { { "Error", ex.Message } };
        }
    }

    private static object SerializeDriverForDiagnostics(Driver driver) => new
    {
        driver.DriverId,
        driver.DriverName,
        driver.FirstName,
        driver.LastName,
        driver.FullName,
        ContactInfo = new
        {
            driver.DriverPhone,
            driver.DriverEmail,
            driver.Address,
            driver.City,
            driver.State,
            driver.Zip,
            driver.FullAddress,
            driver.EmergencyContactName,
            driver.EmergencyContactPhone
        },
        LicenseInfo = new
        {
            driver.DriversLicenceType,
            driver.LicenseNumber,
            driver.LicenseClass,
            driver.LicenseIssueDate,
            driver.LicenseExpiryDate,
            driver.LicenseStatus,
            driver.Endorsements
        },
        QualificationInfo = new
        {
            driver.TrainingComplete,
            driver.QualificationStatus,
            driver.BackgroundCheckDate,
            driver.BackgroundCheckExpiry,
            driver.DrugTestDate,
            driver.DrugTestExpiry,
            driver.PhysicalExamDate,
            driver.PhysicalExamExpiry
        },
        StatusInfo = new
        {
            driver.Status,
            driver.IsAvailable,
            driver.NeedsAttention,
            driver.HireDate,
            driver.CreatedDate,
            driver.UpdatedDate,
            driver.CreatedBy,
            driver.UpdatedBy,
            driver.Notes,
            driver.MedicalRestrictions
        }
    };

    private static string BusNumberFor(Route route, int driverId)
    {
        if (route.AMDriverId == driverId)
        {
            return route.AMVehicle?.BusNumber ?? "Unknown";
        }

        return route.PMVehicle?.BusNumber ?? "Unknown";
    }
}
#endif
