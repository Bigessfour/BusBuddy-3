using BusBuddy.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.SqlClient;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage;

namespace BusBuddy.Core.Utilities
{
    /// <summary>
    /// Utility to validate and patch database schema when migrations cannot be used
    /// Provides a holistic approach to ensure all required columns exist in the database
    /// </summary>
    public static class DatabaseSchemaValidator
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(DatabaseSchemaValidator));

        public static void ValidateAndPatchSchema(IServiceProvider serviceProvider)
        {
            try
            {
                var context = serviceProvider.GetRequiredService<BusBuddy.Core.Data.BusBuddyDbContext>();
                var connectionString = context.Database.GetConnectionString();

                if (string.IsNullOrEmpty(connectionString))
                {
                    Debug.WriteLine("[DEBUG] DatabaseSchemaValidator: Connection string is null or empty");
                    Logger.Error("Connection string is null or empty");
                    return;
                }

                Debug.WriteLine("[DEBUG] DatabaseSchemaValidator: Validating database schema");
                Logger.Information("Validating database schema");

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Validate Routes table schema
                    ValidateRoutesTable(connection);

                    // Validate Activities table
                    ValidateActivitiesTable(connection);

                    // Validate Drivers table
                    ValidateDriversTable(connection);

                    // Validate Vehicles (Buses) table
                    ValidateVehiclesTable(connection);

                    Debug.WriteLine("[DEBUG] DatabaseSchemaValidator: Schema validation complete");
                    Logger.Information("Database schema validation complete");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Error in DatabaseSchemaValidator: {ex.Message}");
                Logger.Error(ex, "Error validating and patching database schema");
            }
        }

        /// <summary>
        /// Comprehensive validation of the Routes table
        /// </summary>
        private static void ValidateRoutesTable(SqlConnection connection)
        {
            Debug.WriteLine("[DEBUG] Validating Routes table schema");

            // Define all required columns with their SQL types
            var requiredColumns = new Dictionary<string, string>
            {
                // Basic properties
                { "RouteId", "int" },
                { "RouteName", "nvarchar(50)" },
                { "Date", "datetime2" },
                { "Description", "nvarchar(500)" },
                { "IsActive", "bit" },

                // AM information
                { "AMVehicleId", "int" },
                { "AMDriverId", "int" },
                { "AMBeginMiles", "decimal(10,2)" },
                { "AMEndMiles", "decimal(10,2)" },
                { "AMRiders", "int" },
                { "AMBeginTime", "time" },

                // PM information
                { "PMVehicleId", "int" },
                { "PMDriverId", "int" },
                { "PMBeginMiles", "decimal(10,2)" },
                { "PMEndMiles", "decimal(10,2)" },
                { "PMRiders", "int" },
                { "PMBeginTime", "time" },

                // Additional properties
                { "Distance", "decimal(10,2)" },
                { "EstimatedDuration", "int" },
                { "StudentCount", "int" },
                { "StopCount", "int" },
                { "DriverName", "nvarchar(100)" },
                { "BusNumber", "nvarchar(20)" },
                { "VehicleId", "int" }
            };

            // Check each column and add if missing
            foreach (var column in requiredColumns)
            {
                if (!ColumnExists(connection, "Routes", column.Key))
                {
                    Debug.WriteLine($"[DEBUG] Missing column '{column.Key}' in Routes table, adding it");
                    Logger.Warning("Missing column '{ColumnName}' in Routes table, adding it", column.Key);

                    string sql;
                    if (column.Key == "IsActive")
                    {
                        // For IsActive, we set a default value of true
                        sql = $"ALTER TABLE dbo.Routes ADD {column.Key} {column.Value} NOT NULL DEFAULT(1)";
                    }
                    else if (column.Value.Contains("nvarchar") || column.Value.Contains("datetime") || column.Value.Contains("time"))
                    {
                        // String and date columns can be nullable
                        sql = $"ALTER TABLE dbo.Routes ADD {column.Key} {column.Value} NULL";
                    }
                    else
                    {
                        // Numeric columns can be nullable
                        sql = $"ALTER TABLE dbo.Routes ADD {column.Key} {column.Value} NULL";
                    }

                    ExecuteNonQuery(connection, sql);
                    Debug.WriteLine($"[DEBUG] Added column '{column.Key}' to Routes table");
                }
            }

            Debug.WriteLine("[DEBUG] Routes table validation complete");
        }

        private static bool ColumnExists(SqlConnection connection, string tableName, string columnName)
        {
            using (var command = new SqlCommand(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@tableName) AND name = @columnName",
                connection))
            {
                command.Parameters.AddWithValue("@tableName", $"dbo.{tableName}");
                command.Parameters.AddWithValue("@columnName", columnName);

                var result = Convert.ToInt32(command.ExecuteScalar());
                return result > 0;
            }
        }

        /// <summary>
        /// Validates the Activities table schema
        /// </summary>
        private static void ValidateActivitiesTable(SqlConnection connection)
        {
            Debug.WriteLine("[DEBUG] Validating Activities table schema");

            // Define required columns for Activities table
            var requiredColumns = new Dictionary<string, string>
            {
                { "ActivityId", "int" },
                { "ActivityType", "nvarchar(50)" },
                { "ActivityCategory", "nvarchar(100)" },
                { "Date", "datetime2" },
                { "Destination", "nvarchar(200)" },
                { "RequestedBy", "nvarchar(100)" },
                { "AssignedVehicleId", "int" },
                { "DriverId", "int" },
                { "LeaveTime", "time" },
                { "EventTime", "time" },
                { "Status", "nvarchar(20)" },
                { "ApprovalRequired", "bit" },
                { "Approved", "bit" },
                { "ApprovedBy", "nvarchar(100)" },
                { "EstimatedCost", "decimal(10,2)" },
                { "ActualCost", "decimal(10,2)" },
                { "CreatedBy", "nvarchar(100)" },
                { "CreatedDate", "datetime2" },
                { "UpdatedBy", "nvarchar(100)" },
                { "UpdatedDate", "datetime2" },
                { "Notes", "nvarchar(500)" },
                { "StudentsCount", "int" },
                { "DistanceMiles", "decimal(8,2)" },
                { "EstimatedTravelTime", "time" },
                { "RouteId", "int" }
            };

            // Check each column and add if missing
            foreach (var column in requiredColumns)
            {
                if (!ColumnExists(connection, "Activities", column.Key))
                {
                    Debug.WriteLine($"[DEBUG] Missing column '{column.Key}' in Activities table, adding it");
                    Logger.Warning("Missing column '{ColumnName}' in Activities table, adding it", column.Key);

                    string sql;
                    if (column.Key == "ApprovalRequired" || column.Key == "Approved")
                    {
                        // For boolean fields, we set a default value of false
                        sql = $"ALTER TABLE dbo.Activities ADD {column.Key} {column.Value} NOT NULL DEFAULT(0)";
                    }
                    else if (column.Value.Contains("nvarchar") || column.Value.Contains("datetime") || column.Value.Contains("time"))
                    {
                        // String and date columns can be nullable
                        sql = $"ALTER TABLE dbo.Activities ADD {column.Key} {column.Value} NULL";
                    }
                    else
                    {
                        // Numeric columns can be nullable
                        sql = $"ALTER TABLE dbo.Activities ADD {column.Key} {column.Value} NULL";
                    }

                    ExecuteNonQuery(connection, sql);
                    Debug.WriteLine($"[DEBUG] Added column '{column.Key}' to Activities table");
                }
            }

            Debug.WriteLine("[DEBUG] Activities table validation complete");
        }

        /// <summary>
        /// Validates the Drivers table schema
        /// </summary>
        private static void ValidateDriversTable(SqlConnection connection)
        {
            Debug.WriteLine("[DEBUG] Validating Drivers table schema");

            // Define required columns for Drivers table
            var requiredColumns = new Dictionary<string, string>
            {
                { "DriverId", "int" },
                { "DriverName", "nvarchar(100)" },
                { "DriverPhone", "nvarchar(20)" },
                { "DriverEmail", "nvarchar(100)" },
                { "Status", "nvarchar(20)" },
                { "DriversLicenceType", "nvarchar(20)" },
                { "LicenseNumber", "nvarchar(20)" },
                { "LicenseClass", "nvarchar(10)" },
                { "LicenseIssueDate", "datetime2" },
                { "LicenseExpiryDate", "datetime2" },
                { "TrainingComplete", "bit" },
                { "HireDate", "datetime2" },
                { "FirstName", "nvarchar(50)" },
                { "LastName", "nvarchar(50)" },
                { "Address", "nvarchar(200)" },
                { "City", "nvarchar(50)" },
                { "State", "nvarchar(20)" },
                { "Zip", "nvarchar(10)" },
                { "EmergencyContactName", "nvarchar(100)" },
                { "EmergencyContactPhone", "nvarchar(20)" },
                { "Endorsements", "nvarchar(100)" },
                { "MedicalRestrictions", "nvarchar(100)" },
                { "PhysicalExamDate", "datetime2" },
                { "PhysicalExamExpiry", "datetime2" },
                { "BackgroundCheckDate", "datetime2" },
                { "BackgroundCheckExpiry", "datetime2" },
                { "DrugTestDate", "datetime2" },
                { "DrugTestExpiry", "datetime2" },
                { "Notes", "nvarchar(1000)" },
                { "CreatedBy", "nvarchar(100)" },
                { "CreatedDate", "datetime2" },
                { "UpdatedBy", "nvarchar(100)" },
                { "UpdatedDate", "datetime2" }
            };

            // Check each column and add if missing
            foreach (var column in requiredColumns)
            {
                if (!ColumnExists(connection, "Drivers", column.Key))
                {
                    Debug.WriteLine($"[DEBUG] Missing column '{column.Key}' in Drivers table, adding it");
                    Logger.Warning("Missing column '{ColumnName}' in Drivers table, adding it", column.Key);

                    string sql;
                    if (column.Key == "TrainingComplete")
                    {
                        // For boolean fields, we set a default value of false
                        sql = $"ALTER TABLE dbo.Drivers ADD {column.Key} {column.Value} NOT NULL DEFAULT(0)";
                    }
                    else if (column.Value.Contains("nvarchar") || column.Value.Contains("datetime") || column.Value.Contains("time"))
                    {
                        // String and date columns can be nullable
                        sql = $"ALTER TABLE dbo.Drivers ADD {column.Key} {column.Value} NULL";
                    }
                    else
                    {
                        // Numeric columns can be nullable
                        sql = $"ALTER TABLE dbo.Drivers ADD {column.Key} {column.Value} NULL";
                    }

                    ExecuteNonQuery(connection, sql);
                    Debug.WriteLine($"[DEBUG] Added column '{column.Key}' to Drivers table");
                }
            }

            Debug.WriteLine("[DEBUG] Drivers table validation complete");
        }

        /// <summary>
        /// Validates the Vehicles (Buses) table schema
        /// </summary>
        private static void ValidateVehiclesTable(SqlConnection connection)
        {
            Debug.WriteLine("[DEBUG] Validating Vehicles table schema");

            // Define required columns for Vehicles table
            var requiredColumns = new Dictionary<string, string>
            {
                { "VehicleId", "int" },
                { "BusNumber", "nvarchar(20)" },
                { "VINNumber", "nvarchar(17)" },
                { "LicenseNumber", "nvarchar(20)" },
                { "Make", "nvarchar(50)" },
                { "Model", "nvarchar(50)" },
                { "Year", "int" },
                { "Status", "nvarchar(20)" },
                { "SeatingCapacity", "int" },
                { "CurrentOdometer", "int" },
                { "FleetType", "nvarchar(20)" },
                { "FuelType", "nvarchar(20)" },
                { "FuelCapacity", "decimal(8,2)" },
                { "MilesPerGallon", "decimal(6,2)" },
                { "PurchaseDate", "datetime2" },
                { "PurchasePrice", "decimal(10,2)" },
                { "DateLastInspection", "datetime2" },
                { "LastServiceDate", "datetime2" },
                { "NextMaintenanceDue", "datetime2" },
                { "NextMaintenanceMileage", "int" },
                { "Department", "nvarchar(50)" },
                { "InsurancePolicyNumber", "nvarchar(100)" },
                { "InsuranceExpiryDate", "datetime2" },
                { "GPSTracking", "bit" },
                { "GPSDeviceId", "nvarchar(100)" },
                { "SpecialEquipment", "nvarchar(1000)" },
                { "Notes", "nvarchar(1000)" },
                { "CreatedBy", "nvarchar(100)" },
                { "CreatedDate", "datetime2" },
                { "UpdatedBy", "nvarchar(100)" },
                { "UpdatedDate", "datetime2" }
            };

            // Check each column and add if missing
            foreach (var column in requiredColumns)
            {
                if (!ColumnExists(connection, "Vehicles", column.Key))
                {
                    Debug.WriteLine($"[DEBUG] Missing column '{column.Key}' in Vehicles table, adding it");
                    Logger.Warning("Missing column '{ColumnName}' in Vehicles table, adding it", column.Key);

                    string sql;
                    if (column.Key == "GPSTracking")
                    {
                        // For boolean fields, we set a default value of false
                        sql = $"ALTER TABLE dbo.Vehicles ADD {column.Key} {column.Value} NOT NULL DEFAULT(0)";
                    }
                    else if (column.Value.Contains("nvarchar") || column.Value.Contains("datetime") || column.Value.Contains("time"))
                    {
                        // String and date columns can be nullable
                        sql = $"ALTER TABLE dbo.Vehicles ADD {column.Key} {column.Value} NULL";
                    }
                    else
                    {
                        // Numeric columns can be nullable
                        sql = $"ALTER TABLE dbo.Vehicles ADD {column.Key} {column.Value} NULL";
                    }

                    ExecuteNonQuery(connection, sql);
                    Debug.WriteLine($"[DEBUG] Added column '{column.Key}' to Vehicles table");
                }
            }

            Debug.WriteLine("[DEBUG] Vehicles table validation complete");
        }

        private static void ExecuteNonQuery(SqlConnection connection, string sql)
        {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities - This method is used for schema validation with predefined SQL
            using (var command = new SqlCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
#pragma warning restore CA2100
        }
    }
}
