using System.Text;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>Driver roster CSV. Not driver CRUD or qualification writes.</summary>
public static class DriverCsvExporter
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DriverCsvExporter));

    public static string ToCsv(IReadOnlyList<Driver> drivers)
    {
        Logger.Information("Exporting drivers to CSV format");

        var csv = new StringBuilder();
        csv.AppendLine("Driver ID,Driver Name,Phone,Email,Address,City,State,ZIP," +
                      "License Type,License Number,License Class,License Expiry," +
                      "Training Complete,Status,Hire Date,Notes");

        foreach (var driver in drivers)
        {
            csv.AppendLine(CsvLine.Join(
                driver.DriverId,
                driver.DriverName,
                driver.DriverPhone,
                driver.DriverEmail,
                driver.Address,
                driver.City,
                driver.State,
                driver.Zip,
                driver.DriversLicenceType,
                driver.LicenseNumber,
                driver.LicenseClass,
                driver.LicenseExpiryDate,
                driver.TrainingComplete,
                driver.Status,
                driver.HireDate,
                driver.Notes));
        }

        Logger.Information("Successfully exported {Count} drivers to CSV", drivers.Count);
        return csv.ToString();
    }
}
