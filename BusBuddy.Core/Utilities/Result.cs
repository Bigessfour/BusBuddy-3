using System.Diagnostics.CodeAnalysis;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Result pattern implementation for robust error handling
/// Based on Microsoft.Extensions.Http.Resilience patterns
/// </summary>
public class Result<T>
{
    internal Result(T value, bool isSuccess, string error, Exception exception)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
        Exception = exception;
    }

    internal Result(T value, bool isSuccess, string error) : this(value, isSuccess, error, new ArgumentException(error))
    {
    }

    public T Value { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public Exception? Exception { get; }

    public bool HasValue => IsSuccess && Value != null;

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess && Value != null
            ? onSuccess(Value)
            : onFailure(Error ?? "Unknown error");
    }

    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess,
        Func<string, Task<TResult>> onFailure)
    {
        return IsSuccess && Value != null
            ? await onSuccess(Value)
            : await onFailure(Error ?? "Unknown error");
    }

}

/// <summary>
/// Result pattern for operations without return values
/// </summary>
public class Result
{
    internal Result(bool isSuccess, string error, Exception exception)
    {
        IsSuccess = isSuccess;
        Error = error;
        Exception = exception;
    }

    internal Result(bool isSuccess, string error) : this(isSuccess, error, new ArgumentException(error))
    {
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public Exception? Exception { get; }

    public static Result Success() => new Result(true, string.Empty);
    public static Result Failure(string error) => new Result(false, error);
    public static Result Failure(string error, Exception exception) => new Result(false, error, exception);

    /// <summary>
    /// Creates a successful generic result with a value
    /// </summary>
    /// <summary>
    /// Creates a successful generic result with a value
    /// </summary>
    public static Result<T> SuccessResult<T>(T value) => new Result<T>(value, true, string.Empty);

    /// <summary>
    /// Creates a failed generic result with an error message
    /// </summary>
    public static Result<T> FailureResult<T>(string error) => new Result<T>(default!, false, error);

    /// <summary>
    /// Creates a failed generic result with an error message and exception
    /// </summary>
    public static Result<T> FailureResult<T>(string error, Exception exception) => new Result<T>(default!, false, error, exception);

    public static Result<T> Success<T>(T value) => new Result<T>(value, true, string.Empty);
    public static Result<T> Failure<T>(string error) => new Result<T>(default!, false, error);
    public static Result<T> Failure<T>(string error, Exception exception) => new Result<T>(default!, false, error, exception);
}
