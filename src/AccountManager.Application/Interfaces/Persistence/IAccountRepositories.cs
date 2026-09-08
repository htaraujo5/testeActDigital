using AccountManager.Domain.Enums;

namespace AccountManager.Application.Interfaces.Persistence;

public interface IAccountWriteRepository
{
    Task EnsureAccountExistsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<Domain.Entities.Account> GetAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransactionReadModel>> GetTransactionsAsync(
        Guid accountId,
        int take,
        CancellationToken cancellationToken);
    Task<IdempotentRecord?> FindIdempotentAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken);
    Task<MutationPersistResult> PersistMutationAsync(MutationPersistRequest request, CancellationToken cancellationToken);
}

public interface IAccountReadRepository
{
    Task<BalanceReadModel?> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransactionReadModel>> GetTransactionsAsync(
        Guid accountId,
        int take,
        DateTimeOffset? before,
        CancellationToken cancellationToken);
}

public sealed record MutationPersistRequest(
    Guid AccountId,
    LedgerEntryType Type,
    decimal Amount,
    string IdempotencyKey,
    string RequestHash,
    AuditContext Audit);

public sealed record MutationPersistResult(
    Guid TransactionId,
    bool Succeeded,
    string Outcome,
    string? ReasonCode,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter);

public sealed record IdempotentRecord(
    Guid TransactionId,
    bool Succeeded,
    string Outcome,
    string? ReasonCode,
    string RequestHash,
    decimal? BalanceAfter,
    string? ResponsePayload);

public sealed record AuditContext(
    string CorrelationId,
    string Actor,
    string? SourceIp,
    string? UserAgent,
    string ApiInstanceId,
    string Operation,
    string DbTarget,
    string GateDecision);

public sealed record BalanceReadModel(Guid AccountId, decimal Balance, DateTimeOffset AsOfUtc, long Version);

public sealed record TransactionReadModel(
    Guid Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    DateTimeOffset OccurredAtUtc,
    decimal BalanceAfter);
