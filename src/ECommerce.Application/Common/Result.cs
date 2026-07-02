namespace ECommerce.Application.Common;

public class Result
{
    public bool Success { get; }
    public string? Error { get; }
    public ErrorType ErrorType { get; }

    protected Result(bool success, string? error, ErrorType errorType)
    {
        Success = success;
        Error = error;
        ErrorType = errorType;
    }

    public static Result Ok() => new(true, null, ErrorType.None);
    public static Result Fail(string error, ErrorType type = ErrorType.Validation) => new(false, error, type);
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Fail<T>(string error, ErrorType type = ErrorType.Validation) => Result<T>.Fail(error, type);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool success, T? value, string? error, ErrorType errorType)
        : base(success, error, errorType)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(true, value, null, ErrorType.None);
    public static new Result<T> Fail(string error, ErrorType type = ErrorType.Validation) =>
        new(false, default, error, type);
}

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}
