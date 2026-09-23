using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Routes a free-text message to the student-form field the clerk has to fix, so validation and
/// persistence failures land on a highlighted control instead of only in the global banner.
/// Service rules and database failures phrase things differently, hence the two entry points.
/// </summary>
internal static class StudentFormFieldErrorMap
{
    /// <summary>Map a message from <c>IStudentService.ValidateStudentAsync</c> to a field key.</summary>
    public static (string FieldKey, string Message) ForServiceMessage(string message)
    {
        if (Mentions(message, "AM Route"))
        {
            return (StudentFormFields.AMRoute, message);
        }

        if (Mentions(message, "PM Route"))
        {
            return (StudentFormFields.PMRoute, message);
        }

        if (Mentions(message, "ZIP") || Mentions(message, "zip code"))
        {
            return (StudentFormFields.Zip, message);
        }

        if (Mentions(message, "cell phone"))
        {
            return (StudentFormFields.CellPhone, message);
        }

        if (Mentions(message, "home phone"))
        {
            return (StudentFormFields.HomePhone, message);
        }

        if (Mentions(message, "emergency phone"))
        {
            return (StudentFormFields.EmergencyPhone, message);
        }

        if (Mentions(message, "grade"))
        {
            return (StudentFormFields.Grade, message);
        }

        if (Mentions(message, "name"))
        {
            return (StudentFormFields.StudentName, message);
        }

        return (StudentFormFields.HomeAddress, message);
    }

    /// <summary>
    /// Map a persistence failure to a field key, or return null when nothing specific is implicated
    /// and the caller should raise a global error instead.
    /// </summary>
    public static (string FieldKey, string Message)? ForPersistenceFailure(string message)
    {
        if (Mentions(message, "grade"))
        {
            return (StudentFormFields.Grade, message);
        }

        if (Mentions(message, "DateTime") || Mentions(message, "timestamp"))
        {
            return (StudentFormFields.DateOfBirth,
                "A date field could not be saved. Clear Date of Birth or pick the date again, then retry.");
        }

        if (Mentions(message, "route"))
        {
            return (StudentFormFields.AMRoute, message);
        }

        if (Mentions(message, "ZIP"))
        {
            return (StudentFormFields.Zip, message);
        }

        return null;
    }

    private static bool Mentions(string message, string term) =>
        message.Contains(term, StringComparison.OrdinalIgnoreCase);
}
