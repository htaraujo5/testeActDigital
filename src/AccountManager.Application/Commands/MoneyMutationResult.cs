namespace AccountManager.Application.Commands;

public sealed record MoneyMutationResult(
    Guid TransactionId,
    Guid AccountId,
    string Operation,
    decimal Amount,
    decimal Balance,
    string Outcome,
    string? ReasonCode,
    bool IdempotentReplay);
