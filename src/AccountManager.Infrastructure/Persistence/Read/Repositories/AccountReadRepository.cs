using AccountManager.Application.Interfaces.Persistence;
using AccountManager.Infrastructure.Persistence.Read.Models;
using Dapper;
using Npgsql;

namespace AccountManager.Infrastructure.Persistence.Read.Repositories;

public sealed class AccountReadRepository : IAccountReadRepository
{
    private readonly string _connectionString;

    public AccountReadRepository(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<BalanceReadModel?> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT "Id" AS AccountId,
                   "Balance",
                   "UpdatedAtUtc" AS AsOfUtc,
                   "Version"
            FROM accounts
            WHERE "Id" = @AccountId
            """;

        var row = await connection.QuerySingleOrDefaultAsync<AccountBalanceReadModel>(
            new CommandDefinition(sql, new { AccountId = accountId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new BalanceReadModel(row.AccountId, row.Balance, row.AsOfUtc, row.Version);
    }

    public async Task<IReadOnlyList<TransactionReadModel>> GetTransactionsAsync(
        Guid accountId,
        int take,
        DateTimeOffset? before,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT "Id",
                   "AccountId",
                   "Type",
                   "Amount",
                   "OccurredAtUtc",
                   "BalanceAfter"
            FROM ledger_entries
            WHERE "AccountId" = @AccountId
              AND (@Before IS NULL OR "OccurredAtUtc" < @Before)
            ORDER BY "OccurredAtUtc" DESC, "Id" DESC
            LIMIT @Take
            """;

        var rows = await connection.QueryAsync<TransactionReadRow>(
            new CommandDefinition(
                sql,
                new { AccountId = accountId, Before = before, Take = take },
                cancellationToken: cancellationToken));

        return rows
            .Select(x => new TransactionReadModel(
                x.Id,
                x.AccountId,
                x.Type,
                x.Amount,
                x.OccurredAtUtc,
                x.BalanceAfter))
            .ToList();
    }
}
