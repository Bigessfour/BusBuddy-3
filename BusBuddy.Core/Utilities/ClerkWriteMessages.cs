using Serilog;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Clerk-facing write outcomes. Ribbon/status surfaces show these sentences instead of
/// swallowed bools or raw EF constraint names.
/// </summary>
public static class ClerkWriteMessages
{
    public static string Retired(string subject, params (int Count, string Singular, string Plural)[] blockers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var parts = new List<string>();
        foreach (var (count, singular, plural) in blockers)
        {
            if (count <= 0)
            {
                continue;
            }

            parts.Add($"{count} {(count == 1 ? singular : plural)}");
        }

        return parts.Count == 0
            ? $"{subject} retired."
            : $"{subject} retired; {string.Join(", ", parts)} still reference it.";
    }

    public static string Deleted(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return $"{subject} deleted.";
    }

    public static string NotFound(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return $"{subject} was not found.";
    }

    public static Result<T> FailureFromException<T>(
        Exception exception,
        string operation,
        ILogger logger,
        string logTemplate,
        params object[] args)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logger);

        DatabaseUserMessage.LogFailure(logger, exception, logTemplate, args);
        if (exception is ArgumentException or InvalidOperationException)
        {
            return Result.FailureResult<T>(exception.Message);
        }

        return Result.FailureResult<T>(DatabaseUserMessage.ForOperation(exception, operation));
    }
}
