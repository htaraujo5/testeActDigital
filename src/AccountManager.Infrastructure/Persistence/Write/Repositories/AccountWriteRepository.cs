using System.Text.Json;
using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Domain.Entities;
using AccountManager.Domain.Enums;
using AccountManager.Domain.Results;
using AccountManager.Infrastructure.Persistence.Write.Context;
using AccountManager.Infrastructure.Persistence.Write.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountManager.Infrastructure.Persistence.Write.Repositories;

public sealed class AccountWriteRepository : IAccountWriteRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WriteDbContext _db;

    public AccountWriteRepository(WriteDbContext db) => _db = db;

    public async Task EnsureAccountExistsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var exists = await _db.Accounts.AnyAsync(x => x.Id == accountId, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.Accounts.Add(new AccountModel
        {
            Id = accountId,
            Balance = 0m,
            Version = 0,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
        }
    }

    public async Task<Account> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var model = await _db.Accounts.AsNoTracking()
            .SingleAsync(x => x.Id == accountId, cancellationToken);
        return new Account(model.Id, model.Balance, model.Version);
    }

    public async Task<IReadOnlyList<TransactionReadModel>> GetTransactionsAsync(
        Guid accountId,
        int take,
        CancellationToken cancellationToken)
    {
        return await _db.LedgerEntries.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new TransactionReadModel(
                x.Id,
                x.AccountId,
                x.Type,
                x.Amount,
                x.OccurredAtUtc,
                x.BalanceAfter))
            .ToListAsync(cancellationToken);
    }

    public async Task<IdempotentRecord?> FindIdempotentAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var model = await _db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (model is null)
        {
            return null;
        }

        return new IdempotentRecord(
            model.TransactionId,
            model.Succeeded,
            model.Outcome,
            model.ReasonCode,
            model.RequestHash,
            model.BalanceAfter,
            model.ResponsePayload);
    }

    public async Task<MutationPersistResult> PersistMutationAsync(
        MutationPersistRequest request,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM accounts WHERE \"Id\" = {request.AccountId} FOR UPDATE",
            cancellationToken);

        var accountModel = await _db.Accounts
            .SingleAsync(x => x.Id == request.AccountId, cancellationToken);

        var account = new Account(accountModel.Id, accountModel.Balance, accountModel.Version);
        var domainResult = request.Type == LedgerEntryType.Credit
            ? account.ApplyCredit(request.Amount)
            : account.ApplyDebit(request.Amount);

        var transactionId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var succeeded = domainResult.Succeeded;
        var outcome = succeeded ? "success" : "rejected";
        var httpStatus = succeeded
            ? 201
            : domainResult.ErrorCode == DomainErrorCodes.InsufficientFunds ? 422 : 400;

        _db.Entry(accountModel).State = EntityState.Detached;

        if (succeeded)
        {
            if (request.Type == LedgerEntryType.Debit)
            {
                var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE accounts
                     SET "Balance" = "Balance" - {request.Amount},
                         "Version" = "Version" + 1,
                         "UpdatedAtUtc" = {occurredAt}
                     WHERE "Id" = {request.AccountId}
                       AND "Balance" >= {request.Amount}
                     """,
                    cancellationToken);

                if (affected != 1)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new MutationPersistResult(
                        transactionId,
                        false,
                        "rejected",
                        DomainErrorCodes.InsufficientFunds,
                        request.Amount,
                        domainResult.BalanceBefore,
                        domainResult.BalanceBefore);
                }
            }
            else
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE accounts
                     SET "Balance" = "Balance" + {request.Amount},
                         "Version" = "Version" + 1,
                         "UpdatedAtUtc" = {occurredAt}
                     WHERE "Id" = {request.AccountId}
                     """,
                    cancellationToken);
            }

            _db.LedgerEntries.Add(new LedgerEntryModel
            {
                Id = transactionId,
                AccountId = request.AccountId,
                Type = request.Type.ToString().ToLowerInvariant(),
                Amount = request.Amount,
                OccurredAtUtc = occurredAt,
                IdempotencyKey = request.IdempotencyKey,
                BalanceAfter = domainResult.BalanceAfter
            });
        }

        var responsePayload = JsonSerializer.Serialize(
            new
            {
                transactionId,
                accountId = request.AccountId,
                operation = request.Audit.Operation,
                amount = request.Amount,
                balance = domainResult.BalanceAfter,
                outcome,
                reasonCode = domainResult.ErrorCode,
                idempotentReplay = false
            },
            JsonOptions);

        _db.IdempotencyRecords.Add(new IdempotencyModel
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            IdempotencyKey = request.IdempotencyKey,
            RequestHash = request.RequestHash,
            TransactionId = transactionId,
            Succeeded = succeeded,
            Outcome = outcome,
            ReasonCode = domainResult.ErrorCode,
            BalanceAfter = domainResult.BalanceAfter,
            ResponsePayload = responsePayload,
            CreatedAtUtc = occurredAt
        });

        _db.AuditEvents.Add(new AuditEventModel
        {
            EventId = Guid.NewGuid(),
            CorrelationId = request.Audit.CorrelationId,
            OccurredAtUtc = occurredAt,
            Actor = request.Audit.Actor,
            SourceIp = request.Audit.SourceIp,
            UserAgent = request.Audit.UserAgent,
            ApiInstanceId = request.Audit.ApiInstanceId,
            AccountId = request.AccountId,
            Operation = request.Audit.Operation,
            Amount = request.Amount,
            BalanceBefore = domainResult.BalanceBefore,
            BalanceAfter = domainResult.BalanceAfter,
            Outcome = outcome,
            ReasonCode = domainResult.ErrorCode,
            HttpStatus = httpStatus,
            IdempotencyKey = request.IdempotencyKey,
            RequestHash = request.RequestHash,
            DbTarget = request.Audit.DbTarget,
            GateDecision = request.Audit.GateDecision
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new MutationPersistResult(
            transactionId,
            succeeded,
            outcome,
            domainResult.ErrorCode,
            request.Amount,
            domainResult.BalanceBefore,
            domainResult.BalanceAfter);
    }
}
