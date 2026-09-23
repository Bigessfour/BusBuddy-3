using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// The single explicit input describing whether the student form skips required-field checks.
/// Phone format is always checked on the form and again by <c>StudentService</c>. There is no
/// environment switch that lets a malformed number through.
/// </summary>
public sealed record StudentFormValidationPolicy(bool SkipFieldValidation)
{
    /// <summary>Relaxes required-field checks for bulk data entry / import triage.</summary>
    public const string SkipFieldValidationVariable = "BUSBUDDY_SKIP_STUDENT_VALIDATION";

    /// <summary>What a clerk gets with no environment variables set: fields checked.</summary>
    public static StudentFormValidationPolicy Default { get; } = new(SkipFieldValidation: false);

    public static StudentFormValidationPolicy FromEnvironment() =>
        new(IsTruthy(Environment.GetEnvironmentVariable(SkipFieldValidationVariable)));

    /// <summary>
    /// True when field validation is relaxed, which also means service-level validation is skipped.
    /// </summary>
    public bool BypassesServiceValidation => SkipFieldValidation;

    /// <summary>Emits the resolved policy once so a surprising save path is traceable in the log.</summary>
    public void LogIfNotDefault(ILogger logger)
    {
        if (this == Default)
        {
            return;
        }

        logger.Warning(
            "Student intake validation policy is not the default: SkipFieldValidation={SkipFieldValidation}",
            SkipFieldValidation);
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
