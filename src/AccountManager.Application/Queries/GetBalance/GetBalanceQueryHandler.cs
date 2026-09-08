using AccountManager.Application.Common;
using AccountManager.Application.Interfaces.Coordination;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using Microsoft.Extensions.Logging;

namespace AccountManager.Application.Queries.GetBalance;

public sealed class GetBalanceQueryHandler
    : IQueryHandler<GetBalanceQuery, ApplicationResult<BalanceResult>>
{
    private readonly IAccountReadRepository _readRepository;
    private readonly IAccountWriteRepository _writeRepository;
    private readonly ICoordinationGate _gate;
    private readonly ICircuitBreakerRegistry _circuits;
    private readonly ILogger<GetBalanceQueryHandler> _logger;

    public GetBalanceQueryHandler(
        IAccountReadRepository readRepository,
        IAccountWriteRepository writeRepository,
        ICoordinationGate gate,
        ICircuitBreakerRegistry circuits,
        ILogger<GetBalanceQueryHandler> logger)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _gate = gate;
        _circuits = circuits;
        _logger = logger;
    }

    public async Task<ApplicationResult<BalanceResult>> HandleAsync(
        GetBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var canUseRead = !_circuits.IsOpen(CircuitTarget.DbRead);
        var lagOk = false;

        if (canUseRead)
        {
            try
            {
                lagOk = await _circuits.ExecuteAsync(
                    CircuitTarget.Redis,
                    ct => _gate.IsReadReplicaHealthyAsync(ct),
                    cancellationToken);
            }
            catch
            {
                lagOk = false;
            }
        }

        if (canUseRead && lagOk)
        {
            try
            {
                var read = await _circuits.ExecuteAsync(
                    CircuitTarget.DbRead,
                    ct => _readRepository.GetBalanceAsync(query.AccountId, ct),
                    cancellationToken);

                if (read is not null)
                {
                    return ApplicationResult<BalanceResult>.Ok(
                        new BalanceResult(read.AccountId, read.Balance, read.AsOfUtc, "read"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Read repository failed for balance; falling back to write.");
            }
        }

        if (_circuits.IsOpen(CircuitTarget.DbWrite))
        {
            return ApplicationResult<BalanceResult>.Fail(
                ApplicationErrorCodes.CircuitOpen,
                "No healthy database available for balance query.");
        }

        try
        {
            await _writeRepository.EnsureAccountExistsAsync(query.AccountId, cancellationToken);
            var account = await _circuits.ExecuteAsync(
                CircuitTarget.DbWrite,
                ct => _writeRepository.GetAsync(query.AccountId, ct),
                cancellationToken);

            return ApplicationResult<BalanceResult>.Ok(
                new BalanceResult(account.Id, account.Balance, DateTimeOffset.UtcNow, "write"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Write repository failed for balance fallback.");
            return ApplicationResult<BalanceResult>.Fail(
                ApplicationErrorCodes.DbUnavailable,
                "Unable to read balance from durable stores.");
        }
    }
}
