namespace AccountManager.Gateway.Dtos.Responses;

public sealed record GatewayTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string Username,
    string Role);

public sealed record GatewayMoneyResponse(
    Guid TransactionId,
    Guid AccountId,
    string Operation,
    decimal Amount,
    decimal Balance,
    string Outcome,
    string? ReasonCode,
    bool IdempotentReplay);

public sealed record GatewayBalanceResponse(
    Guid AccountId,
    decimal Balance,
    DateTimeOffset AsOfUtc,
    string Source);

public sealed record GatewayTransactionItem(
    Guid Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    DateTimeOffset OccurredAtUtc,
    decimal BalanceAfter);

public sealed record GatewayTransactionListResponse(
    Guid AccountId,
    IReadOnlyList<GatewayTransactionItem> Items,
    string Source);
