using AccountManager.Application.Interfaces.Coordination;
using StackExchange.Redis;

namespace AccountManager.Infrastructure.Coordination;

public sealed class RedisCoordinationGate : ICoordinationGate
{
    private readonly IConnectionMultiplexer _mux;
    private readonly IDatabase _db;

    public RedisCoordinationGate(IConnectionMultiplexer mux)
    {
        _mux = mux;
        _db = mux.GetDatabase();
    }

    public async Task<IAsyncDisposable?> TryAcquireAccountLockAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var key = $"lock:account:{accountId:N}";
        var token = Guid.NewGuid().ToString("N");
        var acquired = await _db.StringSetAsync(key, token, TimeSpan.FromSeconds(30), When.NotExists);
        if (!acquired)
        {
            // Best-effort wait/retry once
            await Task.Delay(50, cancellationToken);
            acquired = await _db.StringSetAsync(key, token, TimeSpan.FromSeconds(30), When.NotExists);
            if (!acquired)
            {
                return null;
            }
        }

        return new RedisLock(key, token, _db);
    }

    public async Task<IdempotencyReservation> ReserveIdempotencyAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var redisKey = $"idem:{key}";
        var existing = await _db.HashGetAllAsync(redisKey);
        if (existing.Length > 0)
        {
            var map = existing.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            var hash = map.GetValueOrDefault("hash");
            var payload = map.GetValueOrDefault("payload");
            var status = map.GetValueOrDefault("status");

            if (!string.Equals(hash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyReservation(false, true, null);
            }

            if (status == "completed" && !string.IsNullOrWhiteSpace(payload))
            {
                return new IdempotencyReservation(false, false, payload);
            }

            return new IdempotencyReservation(false, false, payload);
        }

        await _db.HashSetAsync(
            redisKey,
            [
                new HashEntry("hash", requestHash),
                new HashEntry("status", "inflight"),
                new HashEntry("payload", string.Empty)
            ]);

        await _db.KeyExpireAsync(redisKey, TimeSpan.FromHours(24));

        var after = await _db.HashGetAsync(redisKey, "hash");
        if (!after.IsNullOrEmpty && after.ToString() != requestHash)
        {
            return new IdempotencyReservation(false, true, null);
        }

        return new IdempotencyReservation(true, false, null);
    }

    public async Task CompleteIdempotencyAsync(string key, string responsePayload, CancellationToken cancellationToken)
    {
        var redisKey = $"idem:{key}";
        await _db.HashSetAsync(
            redisKey,
            new HashEntry[]
            {
                new("status", "completed"),
                new("payload", responsePayload)
            });
        await _db.KeyExpireAsync(redisKey, TimeSpan.FromHours(24));
    }

    public async Task<bool> IsReadReplicaHealthyAsync(CancellationToken cancellationToken)
    {
        var value = await _db.StringGetAsync("gate:read:lag_ok");
        if (value.IsNullOrEmpty)
        {
            // Default open for reads until monitor says otherwise? Prefer allow when unknown in single-node demos.
            return true;
        }

        return value == "1";
    }

    public Task SetReadReplicaHealthyAsync(bool healthy, CancellationToken cancellationToken) =>
        _db.StringSetAsync("gate:read:lag_ok", healthy ? "1" : "0", TimeSpan.FromSeconds(30));

    private sealed class RedisLock : IAsyncDisposable
    {
        private const string ReleaseLua =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        private readonly string _key;
        private readonly string _token;
        private readonly IDatabase _db;
        private bool _disposed;

        public RedisLock(string key, string token, IDatabase db)
        {
            _key = key;
            _token = token;
            _db = db;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _db.ScriptEvaluateAsync(ReleaseLua, new RedisKey[] { _key }, new RedisValue[] { _token });
        }
    }
}

public sealed class NoOpCoordinationGate : ICoordinationGate
{
    public Task<IAsyncDisposable?> TryAcquireAccountLockAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(null);

    public Task<IdempotencyReservation> ReserveIdempotencyAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken) =>
        Task.FromResult(new IdempotencyReservation(true, false, null));

    public Task CompleteIdempotencyAsync(string key, string responsePayload, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<bool> IsReadReplicaHealthyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task SetReadReplicaHealthyAsync(bool healthy, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
