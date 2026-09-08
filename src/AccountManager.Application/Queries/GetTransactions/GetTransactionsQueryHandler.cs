using AccountManager.Application.Common;
using AccountManager.Application.Interfaces.Coordination;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using Microsoft.Extensions.Logging;

namespace AccountManager.Application.Queries.GetTransactions;

public sealed class GetTransactionsQueryHandler
    : IQueryHandler<GetTransactionsQuery, ApplicationResult<TransactionListResult>>
{
    private readonly IAccountReadRepository _readRepository;
    private readonly IAccountWriteRepository _writeRepository;
    private readonly ICoordinationGate _gate;
    private readonly ICircuitBreakerRegistry _circuits;
    private readonly ILogger<GetTransactionsQueryHandler> _logger;

    public GetTransactionsQueryHandler(
        IAccountReadRepository readRepository,
        IAccountWriteRepository writeRepository,
        ICoordinationGate gate,
        ICircuitBreakerRegistry circuits,
        ILogger<GetTransactionsQueryHandler> logger)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _gate = gate;
        _circuits = circuits;
        _logger = logger;
    }

    public async Task<ApplicationResult<TransactionListResult>> HandleAsync(
        GetTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(query.Take, 1, 100);
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
                var items = await _circuits.ExecuteAsync(
                    CircuitTarget.DbRead,
                    ct => _readRepository.GetTransactionsAsync(query.AccountId, take, null, ct),
                    cancellationToken);

                return ApplicationResult<TransactionListResult>.Ok(
                    new TransactionListResult(query.AccountId, items, "read"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Read repository failed for transactions; falling back to write.");
            }
        }

        if (_circuits.IsOpen(CircuitTarget.DbWrite))
        {
            return ApplicationResult<TransactionListResult>.Fail(
                ApplicationErrorCodes.CircuitOpen,
                "No healthy database available for transaction query.");
        }

        try
        {
            var items = await _circuits.ExecuteAsync(
                CircuitTarget.DbWrite,
                ct => _writeRepository.GetTransactionsAsync(query.AccountId, take, ct),
                cancellationToken);

            return ApplicationResult<TransactionListResult>.Ok(
                new TransactionListResult(query.AccountId, items, "write"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to list transactions.");
            return ApplicationResult<TransactionListResult>.Fail(
                ApplicationErrorCodes.DbUnavailable,
                "Unable to read transactions from durable stores.");
        }
    }
}
