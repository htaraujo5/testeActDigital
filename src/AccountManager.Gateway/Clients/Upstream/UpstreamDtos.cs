namespace AccountManager.Gateway.Clients.Upstream;

public sealed record UpstreamTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string Username,
    string Role);

public sealed record UpstreamMoneyResponse(
    Guid TransactionId,
    Guid AccountId,
    string Operation,
    decimal Amount,
    decimal Balance,
    string Outcome,
    string? ReasonCode,
    bool IdempotentReplay);

public sealed record UpstreamBalanceResponse(
    Guid AccountId,
    decimal Balance,
    DateTimeOffset AsOfUtc,
    string Source);

public sealed record UpstreamTransactionItem(
    Guid Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    DateTimeOffset OccurredAtUtc,
    decimal BalanceAfter);

public sealed record UpstreamTransactionListResponse(
    Guid AccountId,
    IReadOnlyList<UpstreamTransactionItem> Items,
    string Source);

public sealed record UpstreamErrorResponse(string? Error, string? Message);
