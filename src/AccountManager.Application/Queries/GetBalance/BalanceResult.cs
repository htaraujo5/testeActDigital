namespace AccountManager.Application.Queries.GetBalance;

public sealed record BalanceResult(
    Guid AccountId,
    decimal Balance,
    DateTimeOffset AsOfUtc,
    string Source);
