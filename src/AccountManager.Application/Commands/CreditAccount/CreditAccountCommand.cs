namespace AccountManager.Application.Commands.CreditAccount;

public sealed record CreditAccountCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string Actor,
    string? SourceIp,
    string? UserAgent,
    string CorrelationId,
    string ApiInstanceId);
