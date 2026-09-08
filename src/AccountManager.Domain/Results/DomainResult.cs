namespace AccountManager.Domain.Results;

public static class DomainErrorCodes
{
    public const string InvalidAmount = "INVALID_AMOUNT";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
}

public sealed class DomainResult
{
    public bool Succeeded { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public decimal BalanceBefore { get; }
    public decimal BalanceAfter { get; }

    private DomainResult(bool succeeded, string? errorCode, string? errorMessage, decimal balanceBefore, decimal balanceAfter)
    {
        Succeeded = succeeded;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        BalanceBefore = balanceBefore;
        BalanceAfter = balanceAfter;
    }

    public static DomainResult Ok(decimal balanceBefore, decimal balanceAfter) =>
        new(true, null, null, balanceBefore, balanceAfter);

    public static DomainResult Fail(string code, string message, decimal balanceBefore = 0, decimal balanceAfter = 0) =>
        new(false, code, message, balanceBefore, balanceAfter);
}
