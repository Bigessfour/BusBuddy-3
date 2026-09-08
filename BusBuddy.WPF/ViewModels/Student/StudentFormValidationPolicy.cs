using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>How hard a phone-number format problem pushes back on the clerk.</summary>
public enum StudentPhoneValidationMode
{
    /// <summary>Phone format is not checked at all.</summary>
    Off,

    /// <summary>Phone format is logged but never blocks a save. This is the shipping default.</summary>
    Warn,

    /// <summary>A malformed phone number blocks the save.</summary>
    Strict,
}

/// <summary>
/// The single explicit input describing how strictly the student form validates an intake record.
/// Three separate environment variables used to be read from three separate places; this resolves
/// all of them once, so the effective policy is inspectable and loggable instead of implicit.
/// <para>
/// <see cref="SkipFieldValidation"/> is enforced here in the WPF layer. <see cref="PhoneValidation"/>
/// is enforced by <c>BusBuddy.Core.Services.StudentService</c>; this record mirrors that service's
/// resolution rules so the form can report the effective mode, not so it can override it.
/// </para>
/// </summary>
public sealed record StudentFormValidationPolicy(
    bool SkipFieldValidation,
    StudentPhoneValidationMode PhoneValidation)
{
    /// <summary>Relaxes required-field checks for bulk data entry / import triage.</summary>
    public const string SkipFieldValidationVariable = "BUSBUDDY_SKIP_STUDENT_VALIDATION";

    /// <summary>Legacy on/off switch for phone format checks.</summary>
    public const string SkipPhoneValidationVariable = "BUSBUDDY_SKIP_PHONE_VALIDATION";

    /// <summary>Three-state phone policy: <c>off</c>, <c>warn</c>, or <c>strict</c>.</summary>
    public const string PhoneValidationModeVariable = "BUSBUDDY_PHONE_VALIDATION_MODE";

    /// <summary>What a clerk gets with no environment variables set: fields checked, phones warn-only.</summary>
    public static StudentFormValidationPolicy Default { get; } =
        new(SkipFieldValidation: false, PhoneValidation: StudentPhoneValidationMode.Warn);

    public static StudentFormValidationPolicy FromEnvironment() =>
        new(IsTruthy(Environment.GetEnvironmentVariable(SkipFieldValidationVariable)),
            ResolvePhoneMode());

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
            "Student intake validation policy is not the default: SkipFieldValidation={SkipFieldValidation} PhoneValidation={PhoneValidation}",
            SkipFieldValidation,
            PhoneValidation);
    }

    private static StudentPhoneValidationMode ResolvePhoneMode()
    {
        var mode = Environment.GetEnvironmentVariable(PhoneValidationModeVariable);
        if (IsTruthy(Environment.GetEnvironmentVariable(SkipPhoneValidationVariable))
            || string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
        {
            return StudentPhoneValidationMode.Off;
        }

        // StudentService treats an unset or unrecognized mode as warn-only, and anything else
        // (in practice "strict") as blocking. Mirror that rather than inventing a stricter default.
        return string.IsNullOrEmpty(mode) || string.Equals(mode, "warn", StringComparison.OrdinalIgnoreCase)
            ? StudentPhoneValidationMode.Warn
            : StudentPhoneValidationMode.Strict;
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
