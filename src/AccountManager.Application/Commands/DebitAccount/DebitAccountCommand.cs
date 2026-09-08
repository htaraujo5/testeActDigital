namespace AccountManager.Application.Commands.DebitAccount;

public sealed record DebitAccountCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string Actor,
    string? SourceIp,
    string? UserAgent,
    string CorrelationId,
    string ApiInstanceId);
