namespace Hive.Core;

public enum ErrorCategory
{
    Validation,
    Conflict,
    NotFound,
    Unauthorized,
    Forbidden,
    Concurrency,
    Timeout,
    Cancelled,
    Serialization,
    Unsupported,
    External,
    Internal
}

public sealed record Error
{
    public Error(string code, ErrorCategory category, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Error code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message is required.", nameof(message));

        Code = code.Trim();
        Category = category;
        Message = message.Trim();
    }

    public string Code { get; }

    public ErrorCategory Category { get; }

    public string Message { get; }

    public static Error Validation(string code, string message) =>
        new(code, ErrorCategory.Validation, message);

    public static Error Conflict(string code, string message) =>
        new(code, ErrorCategory.Conflict, message);

    public static Error Unsupported(string code, string message) =>
        new(code, ErrorCategory.Unsupported, message);

    public static Error Cancelled(string code, string message) =>
        new(code, ErrorCategory.Cancelled, message);
}

public readonly record struct Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) =>
        error is null
            ? throw new ArgumentNullException(nameof(error))
            : new(false, error);
}

public readonly record struct Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(Error error) =>
        error is null
            ? throw new ArgumentNullException(nameof(error))
            : new(false, default, error);
}
