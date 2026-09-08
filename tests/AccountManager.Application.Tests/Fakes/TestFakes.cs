using AccountManager.Application.Interfaces.Coordination;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using AccountManager.Domain.Entities;

namespace AccountManager.Application.Tests.Fakes;

internal sealed class PassthroughCircuitBreaker : ICircuitBreakerRegistry
{
    private readonly HashSet<CircuitTarget> _open = [];

    public void Open(CircuitTarget target) => _open.Add(target);

    public bool IsOpen(CircuitTarget target) => _open.Contains(target);

    public string GetState(CircuitTarget target) => IsOpen(target) ? "open" : "closed";

    public Task<T> ExecuteAsync<T>(
        CircuitTarget target,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (IsOpen(target))
        {
            throw new BrokenCircuitException($"Circuit open for {target}");
        }

        return action(cancellationToken);
    }

    public Task ExecuteAsync(
        CircuitTarget target,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken) =>
        ExecuteAsync<object?>(target, async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);
}

internal sealed class FakeCoordinationGate : ICoordinationGate
{
    public bool ReplicaHealthy { get; set; } = true;
    public IdempotencyReservation Reservation { get; set; } = new(true, false, null);
    public string? CompletedPayload { get; private set; }

    public Task<IAsyncDisposable?> TryAcquireAccountLockAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(null);

    public Task<IdempotencyReservation> ReserveIdempotencyAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken) =>
        Task.FromResult(Reservation);

    public Task CompleteIdempotencyAsync(string key, string responsePayload, CancellationToken cancellationToken)
    {
        CompletedPayload = responsePayload;
        return Task.CompletedTask;
    }

    public Task<bool> IsReadReplicaHealthyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ReplicaHealthy);

    public Task SetReadReplicaHealthyAsync(bool healthy, CancellationToken cancellationToken)
    {
        ReplicaHealthy = healthy;
        return Task.CompletedTask;
    }
}

internal sealed class FakeWriteRepository : IAccountWriteRepository
{
    private readonly Dictionary<Guid, Account> _accounts = new();
    private readonly Dictionary<(Guid, string), IdempotentRecord> _idempotency = new();
    private readonly List<TransactionReadModel> _transactions = [];

    public MutationPersistResult? NextPersistResult { get; set; }
    public IdempotentRecord? FindResult { get; set; }
    public Exception? PersistException { get; set; }

    public Task EnsureAccountExistsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (!_accounts.ContainsKey(accountId))
        {
            _accounts[accountId] = new Account(accountId);
        }

        return Task.CompletedTask;
    }

    public Task<Account> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.TryGetValue(accountId, out var account)
            ? account
            : new Account(accountId));

    public Task<IReadOnlyList<TransactionReadModel>> GetTransactionsAsync(
        Guid accountId,
        int take,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransactionReadModel>>(
            _transactions.Where(x => x.AccountId == accountId).Take(take).ToList());

    public void SeedTransactions(params TransactionReadModel[] items) => _transactions.AddRange(items);

    public Task<IdempotentRecord?> FindIdempotentAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (FindResult is not null)
        {
            return Task.FromResult<IdempotentRecord?>(FindResult);
        }

        _idempotency.TryGetValue((accountId, idempotencyKey), out var record);
        return Task.FromResult(record);
    }

    public Task<MutationPersistResult> PersistMutationAsync(
        MutationPersistRequest request,
        CancellationToken cancellationToken)
    {
        if (PersistException is not null)
        {
            throw PersistException;
        }

        if (NextPersistResult is not null)
        {
            return Task.FromResult(NextPersistResult);
        }

        var account = _accounts.GetValueOrDefault(request.AccountId) ?? new Account(request.AccountId);
        var domain = request.Type == Domain.Enums.LedgerEntryType.Credit
            ? account.ApplyCredit(request.Amount)
            : account.ApplyDebit(request.Amount);

        _accounts[request.AccountId] = account;
        var txId = Guid.NewGuid();
        return Task.FromResult(new MutationPersistResult(
            txId,
            domain.Succeeded,
            domain.Succeeded ? "success" : "rejected",
            domain.ErrorCode,
            request.Amount,
            domain.BalanceBefore,
            domain.BalanceAfter));
    }
}

internal sealed class FakeReadRepository : IAccountReadRepository
{
    public BalanceReadModel? Balance { get; set; }
    public IReadOnlyList<TransactionReadModel> Transactions { get; set; } = [];
    public Exception? Exception { get; set; }

    public Task<BalanceReadModel?> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult(Balance);
    }

    public Task<IReadOnlyList<TransactionReadModel>> GetTransactionsAsync(
        Guid accountId,
        int take,
        DateTimeOffset? before,
        CancellationToken cancellationToken)
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        return Task.FromResult<IReadOnlyList<TransactionReadModel>>(Transactions.Take(take).ToList());
    }
}
