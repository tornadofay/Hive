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

public sealed record Error(
    string Code,
    ErrorCategory Category,
    string Message)
{
    public Error
    {
        if (string.IsNullOrWhiteSpace(Code))
            throw new ArgumentException("Error code is required.", nameof(Code));

        if (string.IsNullOrWhiteSpace(Message))
            throw new ArgumentException("Error message is required.", nameof(Message));

        Code = Code.Trim();
        Message = Message.Trim();
    }

    public static Error Validation(string code, string message) =>
        new(code, ErrorCategory.Validation, message);

    public static Error Conflict(string code, string message) =>
        new(code, ErrorCategory.Conflict, message);

    public static Error Unsupported(string code, string message) =>
        new(code, ErrorCategory.Unsupported, message);
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
