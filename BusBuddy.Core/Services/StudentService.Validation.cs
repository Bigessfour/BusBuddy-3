using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services;

public partial class StudentService
{
    #region Validation and Business Logic

    public async Task<List<string>> ValidateStudentAsync(Student student)
    {
        var errors = new List<string>();

        try
        {
            // Required field validation
            if (string.IsNullOrWhiteSpace(student.StudentName))
            {
                errors.Add("Student name is required");
            }

            // Create a context that will be disposed at the end of this method,
            // since the validation results are returned as a new list (not dependent on the context)
            var (context, dispose) = GetReadContext();
            try
            {

                // Student number uniqueness check (if provided)
                if (!string.IsNullOrWhiteSpace(student.StudentNumber))
                {
                    var existingStudent = await context.Students
                        .Where(s => s.StudentNumber == student.StudentNumber && s.StudentId != student.StudentId)
                        .FirstOrDefaultAsync();

                    if (existingStudent != null)
                    {
                        errors.Add($"Student number '{student.StudentNumber}' is already in use");
                    }
                }

                // Grade validation
                if (!string.IsNullOrWhiteSpace(student.Grade))
                {
                    if (!StudentGradeCatalog.IsValid(student.Grade))
                    {
                        errors.Add("Invalid grade level");
                    }
                }

                AddPhoneError(errors, student.HomePhone, "Invalid home phone number format");
                AddPhoneError(errors, student.CellPhone, "Invalid cell phone number format");
                AddPhoneError(errors, student.EmergencyPhone, "Invalid emergency phone number format");

                // State validation
                if (!string.IsNullOrWhiteSpace(student.State))
                {
                    if (student.State.Length != 2)
                    {
                        errors.Add("State must be a 2-letter abbreviation");
                    }
                }

                // ZIP code validation
                if (!string.IsNullOrWhiteSpace(student.Zip))
                {
                    var zipPattern = @"^\d{5}(-\d{4})?$";
                    if (!System.Text.RegularExpressions.Regex.IsMatch(student.Zip, zipPattern))
                    {
                        errors.Add("Invalid ZIP code format");
                    }
                }

                var amRouteError = await StudentDisplayMirror.DescribeRouteSlotAsync(
                    context, student.AmRouteId, student.AMRoute, "AM").ConfigureAwait(false);
                if (amRouteError is not null)
                {
                    errors.Add(amRouteError);
                }

                var pmRouteError = await StudentDisplayMirror.DescribeRouteSlotAsync(
                    context, student.PmRouteId, student.PMRoute, "PM").ConfigureAwait(false);
                if (pmRouteError is not null)
                {
                    errors.Add(pmRouteError);
                }
            }
            finally
            {
                if (dispose)
                {
                    await context.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error during basic student validation");
            errors.Add("Validation error occurred");
        }

        foreach (var warning in GetIntakeWarnings(student))
        {
            Logger.Information("Intake incomplete StudentId={StudentId}: {Warning}", student.StudentId, warning);
        }

        return errors;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetIntakeWarnings(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);
        var warnings = new List<string>();
        if (student.DestinationId is null)
        {
            warnings.Add("School is not assigned.");
        }

        if (!student.RidesAm && !student.RidesPm)
        {
            warnings.Add("Ride eligibility is unset. Choose AM, PM, or both.");
        }

        if (student.PickupStopId is null && !student.HasValidatedHomeCoordinates)
        {
            warnings.Add("Home pickup has no validated coordinates.");
        }

        if (student.PickupStopId is > 0 && student.RequiresSpecialNeedsBus)
        {
            warnings.Add("Special needs uses home pickup. The catalog stop will be cleared on save.");
        }

        return warnings;
    }

    private static void AddPhoneError(List<string> errors, string? phone, string message)
    {
        if (!StudentPhone.TryNormalize(phone, out _))
        {
            errors.Add(message);
        }
    }

    #endregion

    public (bool IsValid, string? ErrorMessage) ValidateAddress(string address, string city, string state, string zip)
    {
        // Implement basic address validation
        if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Address cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return (false, "City cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(state) || state.Length != 2)
        {
            return (false, "State must be a 2-letter abbreviation");
        }

        var zipPattern = @"^\d{5}(-\d{4})?$";
        if (string.IsNullOrWhiteSpace(zip) || !System.Text.RegularExpressions.Regex.IsMatch(zip, zipPattern))
        {
            return (false, "Invalid ZIP code format");
        }

        // In a real implementation, you might also validate against an address verification service
        // For now, we'll just return valid if basic checks pass
        return (true, null);
    }
}
