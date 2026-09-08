namespace AccountManager.Application.Common;

public sealed class ApplicationResult<T>
{
    public bool Succeeded { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private ApplicationResult(bool succeeded, T? value, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static ApplicationResult<T> Ok(T value) =>
        new(true, value, null, null);

    public static ApplicationResult<T> Fail(string code, string message, T? value = default) =>
        new(false, value, code, message);
}
