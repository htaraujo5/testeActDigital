namespace AccountManager.Application.Common;

public static class ApplicationErrorCodes
{
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string CircuitOpenDbWrite = "CIRCUIT_OPEN_DB_WRITE";
    public const string CircuitOpen = "CIRCUIT_OPEN";
    public const string DbUnavailable = "DB_UNAVAILABLE";
}
