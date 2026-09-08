namespace AccountManager.Application.Interfaces.Coordination;

public interface ICoordinationGate
{
    Task<IAsyncDisposable?> TryAcquireAccountLockAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IdempotencyReservation> ReserveIdempotencyAsync(string key, string requestHash, CancellationToken cancellationToken);
    Task CompleteIdempotencyAsync(string key, string responsePayload, CancellationToken cancellationToken);
    Task<bool> IsReadReplicaHealthyAsync(CancellationToken cancellationToken);
    Task SetReadReplicaHealthyAsync(bool healthy, CancellationToken cancellationToken);
}

public sealed record IdempotencyReservation(bool IsNew, bool Conflict, string? ExistingPayload);
