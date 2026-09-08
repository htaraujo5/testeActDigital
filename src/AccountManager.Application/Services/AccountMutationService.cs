using AccountManager.Application.Commands;
using AccountManager.Application.Common;
using AccountManager.Application.Interfaces.Coordination;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Application.Interfaces.Resilience;
using AccountManager.Domain.Enums;
using AccountManager.Domain.Results;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AccountManager.Application.Services;

public sealed class AccountMutationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAccountWriteRepository _writeRepository;
    private readonly ICoordinationGate _gate;
    private readonly ICircuitBreakerRegistry _circuits;
    private readonly ILogger<AccountMutationService> _logger;

    public AccountMutationService(
        IAccountWriteRepository writeRepository,
        ICoordinationGate gate,
        ICircuitBreakerRegistry circuits,
        ILogger<AccountMutationService> logger)
    {
        _writeRepository = writeRepository;
        _gate = gate;
        _circuits = circuits;
        _logger = logger;
    }

    public async Task<ApplicationResult<MoneyMutationResult>> ExecuteAsync(
        Guid accountId,
        decimal amount,
        string idempotencyKey,
        string actor,
        string? sourceIp,
        string? userAgent,
        string correlationId,
        string apiInstanceId,
        LedgerEntryType type,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return ApplicationResult<MoneyMutationResult>.Fail(
                DomainErrorCodes.InvalidAmount,
                "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ApplicationResult<MoneyMutationResult>.Fail(
                ApplicationErrorCodes.IdempotencyKeyRequired,
                "Idempotency-Key header is required.");
        }

        if (_circuits.IsOpen(CircuitTarget.DbWrite))
        {
            return ApplicationResult<MoneyMutationResult>.Fail(
                ApplicationErrorCodes.CircuitOpenDbWrite,
                "Write database circuit is open.");
        }

        var operation = type == LedgerEntryType.Credit ? "credit" : "debit";
        var requestHash = ComputeHash(accountId, operation, amount, idempotencyKey);
        var gateDecision = "redis";
        var redisIdemKey = $"{accountId:N}:{idempotencyKey}";

        IdempotencyReservation reservation;
        try
        {
            reservation = await _circuits.ExecuteAsync(
                CircuitTarget.Redis,
                ct => _gate.ReserveIdempotencyAsync(redisIdemKey, requestHash, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis idempotency unavailable; falling back to database unique constraint.");
            reservation = new IdempotencyReservation(true, false, null);
            gateDecision = "db-fallback";
        }

        if (reservation.Conflict)
        {
            return ApplicationResult<MoneyMutationResult>.Fail(
                ApplicationErrorCodes.IdempotencyConflict,
                "Idempotency key was reused with a different payload.");
        }

        if (!reservation.IsNew && !string.IsNullOrWhiteSpace(reservation.ExistingPayload))
        {
            var replay = JsonSerializer.Deserialize<MoneyMutationResult>(reservation.ExistingPayload, JsonOptions);
            if (replay is not null)
            {
                return ApplicationResult<MoneyMutationResult>.Ok(replay with { IdempotentReplay = true });
            }
        }

        var existing = await _circuits.ExecuteAsync(
            CircuitTarget.DbWrite,
            ct => _writeRepository.FindIdempotentAsync(accountId, idempotencyKey, ct),
            cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return ApplicationResult<MoneyMutationResult>.Fail(
                    ApplicationErrorCodes.IdempotencyConflict,
                    "Idempotency key was reused with a different payload.");
            }

            if (!string.IsNullOrWhiteSpace(existing.ResponsePayload))
            {
                var replay = JsonSerializer.Deserialize<MoneyMutationResult>(existing.ResponsePayload, JsonOptions);
                if (replay is not null)
                {
                    return ApplicationResult<MoneyMutationResult>.Ok(replay with { IdempotentReplay = true });
                }
            }
        }

        IAsyncDisposable? accountLock = null;
        try
        {
            try
            {
                accountLock = await _circuits.ExecuteAsync(
                    CircuitTarget.Redis,
                    ct => _gate.TryAcquireAccountLockAsync(accountId, ct),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis lock unavailable; relying on database row lock.");
                gateDecision = "db-fallback";
            }

            var audit = new AuditContext(
                correlationId,
                actor,
                sourceIp,
                userAgent,
                apiInstanceId,
                operation,
                "write",
                gateDecision);

            await _circuits.ExecuteAsync(
                CircuitTarget.DbWrite,
                ct => _writeRepository.EnsureAccountExistsAsync(accountId, ct),
                cancellationToken);

            var persisted = await _circuits.ExecuteAsync(
                CircuitTarget.DbWrite,
                ct => _writeRepository.PersistMutationAsync(
                    new MutationPersistRequest(
                        accountId,
                        type,
                        amount,
                        idempotencyKey,
                        requestHash,
                        audit),
                    ct),
                cancellationToken);

            var response = new MoneyMutationResult(
                persisted.TransactionId,
                accountId,
                operation,
                amount,
                persisted.BalanceAfter,
                persisted.Outcome,
                persisted.ReasonCode,
                false);

            var payload = JsonSerializer.Serialize(response, JsonOptions);

            try
            {
                await _gate.CompleteIdempotencyAsync(redisIdemKey, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to complete idempotency key in Redis.");
            }

            if (persisted.Succeeded)
            {
                return ApplicationResult<MoneyMutationResult>.Ok(response);
            }

            return ApplicationResult<MoneyMutationResult>.Fail(
                persisted.ReasonCode ?? "REJECTED",
                persisted.Outcome,
                response);
        }
        catch (BrokenCircuitException)
        {
            return ApplicationResult<MoneyMutationResult>.Fail(
                ApplicationErrorCodes.CircuitOpenDbWrite,
                "Write database circuit is open.");
        }
        finally
        {
            if (accountLock is not null)
            {
                await accountLock.DisposeAsync();
            }
        }
    }

    private static string ComputeHash(Guid accountId, string operation, decimal amount, string idempotencyKey)
    {
        var raw = $"{accountId:N}|{operation}|{amount:0.####}|{idempotencyKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
